using System.Collections;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Проверки в Play Mode (batchmode без -nographics и без -quit; Unity закрывается сам):
///  • PlayChecks.Doors — переходы дверями: сцена включается только при полностью закрытых створках, после загрузки
///    створки открываются до конца; двойной вызов загрузки не запускает вторую; при timeScale = 0 переход тоже доходит
///    до конца. Отчёт — Builds/door-play.txt.
/// </summary>
[InitializeOnLoad]
public static class PlayChecks
{
    private const string Key = "PlayChecks.Mode";

    static PlayChecks()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetString(Key, "") != "")
            {
                var go = new GameObject("PlayChecksRunner");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Runner>().mode = SessionState.GetString(Key, "");
            }
        };
    }

    public static void Doors() => Start("doors", "Assets/Scenes/LevelSelectionScene.unity");
    /// <summary>Огонёк меню в движении: кадры в Play Mode (MENU_RES, по умолчанию 1080x1920) → Builds/ui-shots/menuflame/*.png.</summary>
    public static void Menu() => Start("menu", "Assets/Scenes/MainScene.unity");
    /// <summary>Звук итога для 0/1/2/3★ (отладочная победа, без сохранения): что и когда прозвучало → Builds/win-sound.txt.</summary>
    public static void WinSound() => Start("win", "Assets/Scenes/GameScene.unity");
    /// <summary>Нагрузка меню за 4 с: частицы, батчи/SetPass (UnityStats), память текстур огонька → Builds/menu-stats.txt.</summary>
    public static void MenuStats() => Start("menustats", "Assets/Scenes/MainScene.unity");

    private static void Start(string mode, string scene)
    {
        SessionState.SetString(Key, mode);
        EditorSceneManager.OpenScene(scene);
        EditorApplication.isPlaying = true;
    }

    private class Runner : MonoBehaviour
    {
        public string mode;
        private readonly StringBuilder log = new StringBuilder();
        private int loads;
        private float closedAtLoad = -1f;

        private IEnumerator Start()
        {
            if (mode == "menu") { yield return MenuShots(); yield break; }
            if (mode == "win") { yield return WinSounds(); yield break; }
            if (mode == "menustats") { yield return MenuStatsRun(); yield break; }
            SceneManager.sceneLoaded += (s, m) => { loads++; closedAtLoad = SceneTransition.Closed; };
            yield return new WaitForSecondsRealtime(1f);
            yield return Transition("MainScene", false, "обычный переход");
            yield return Transition("LevelSelectionScene", true, "двойной вызов загрузки");
            Time.timeScale = 0f;
            yield return Transition("MainScene", false, "timeScale = 0");
            Time.timeScale = 1f;
            File.WriteAllText("Builds/door-play.txt", log.ToString());
            Debug.Log("[Polish] PlayChecks.Doors\n" + log);
            SessionState.EraseString(Key);
            EditorApplication.Exit(0);
        }

        private IEnumerator MenuShots()
        {
            string resName = System.Environment.GetEnvironmentVariable("MENU_RES") ?? "1080x1920";
            UiAudit.Res res = System.Array.Find(UiAudit.Resolutions, r => r.name == resName);
            yield return new WaitForSecondsRealtime(1.5f);
            Scene scene = SceneManager.GetActiveScene();
            var anim = Object.FindFirstObjectByType<UISkinSequenceAnimator>();
            for (int i = 0; i < 4; i++) { Shot(scene, res, $"menu_{i}"); yield return new WaitForSecondsRealtime(0.35f); }
            yield return new WaitForSecondsRealtime(0.6f);
            if (anim != null)
            {
                anim.SetSkin(1);
                yield return new WaitForSecondsRealtime(0.15f); Shot(scene, res, "skin_mid");
                yield return new WaitForSecondsRealtime(0.6f); Shot(scene, res, "skin_green");
                anim.SetSkin(2); yield return new WaitForSecondsRealtime(0.8f); Shot(scene, res, "skin_blue");
                anim.SetSkin(3); yield return new WaitForSecondsRealtime(0.8f); Shot(scene, res, "skin_purple");
            }
            File.WriteAllText("Builds/menuflame-play.txt", log.ToString());
            Debug.Log("[Polish] PlayChecks.Menu " + log);
            SessionState.EraseString(Key);
            EditorApplication.Exit(0);
        }

        private IEnumerator MenuStatsRun()
        {
            yield return new WaitForSecondsRealtime(2f);
            int maxParticles = 0, maxSystems = 0, maxBatches = 0, maxSetPass = 0, maxTris = 0;
            float end = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < end)
            {
                int p = 0, sys = 0;
                foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) { if (ps.particleCount > 0) sys++; p += ps.particleCount; }
                maxParticles = Mathf.Max(maxParticles, p); maxSystems = Mathf.Max(maxSystems, sys);
                maxBatches = Mathf.Max(maxBatches, UnityStats.batches); maxSetPass = Mathf.Max(maxSetPass, UnityStats.setPassCalls);
                maxTris = Mathf.Max(maxTris, UnityStats.triangles);
                yield return null;
            }
            var anim = Object.FindFirstObjectByType<UISkinSequenceAnimator>();
            long frameBytes = 0; int frameCount = 0;
            var seen = new System.Collections.Generic.HashSet<Texture2D>();
            if (anim != null)
                foreach (var skin in anim.skins)
                    if (skin.frames != null)
                        foreach (Sprite sp in skin.frames)
                            if (sp != null && seen.Add(sp.texture)) { frameCount++; frameBytes += AndroidBytes(sp.texture); }
            log.AppendLine($"Частицы (ParticleSystem): до {maxParticles} в {maxSystems} системах");
            log.AppendLine($"Батчи: до {maxBatches}, SetPass: до {maxSetPass}, треугольников: до {maxTris} (UnityStats в редакторе; 0 — если Game View не рисовался)");
            log.AppendLine($"Кадры огонька: {frameCount} текстур, на Android ~{frameBytes / 1048576f:0.0} МБ (по размеру и формату импорта)");
            File.WriteAllText("Builds/menu-stats.txt", log.ToString());
            Debug.Log("[Polish] PlayChecks.MenuStats\n" + log);
            SessionState.EraseString(Key);
            EditorApplication.Exit(0);
        }

        /// <summary>Байты текстуры в сборке Android: размер после maxTextureSize, бит на пиксель по формату импорта.</summary>
        private static long AndroidBytes(Texture2D t)
        {
            var imp = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t)) as TextureImporter;
            if (imp == null) return UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t);
            imp.GetSourceTextureWidthAndHeight(out int w, out int h);
            TextureImporterPlatformSettings ps = imp.GetPlatformTextureSettings("Android");
            int max = ps.overridden ? ps.maxTextureSize : imp.maxTextureSize;
            float k = Mathf.Min(1f, (float)max / Mathf.Max(w, h));
            w = Mathf.Max(1, Mathf.RoundToInt(w * k)); h = Mathf.Max(1, Mathf.RoundToInt(h * k));
            TextureImporterFormat f = ps.overridden ? ps.format : TextureImporterFormat.Automatic;
            if (f == TextureImporterFormat.Automatic) f = imp.GetAutomaticFormat("Android");
            float bpp = f.ToString().Contains("ASTC_4x4") ? 8f : f.ToString().Contains("ASTC_5x5") ? 5.12f : f.ToString().Contains("ASTC_6x6") ? 3.56f
                      : f.ToString().Contains("ASTC_8x8") ? 2f : f.ToString().Contains("ASTC_10x10") ? 1.28f : f.ToString().Contains("ASTC_12x12") ? 0.89f
                      : f.ToString().Contains("ETC2_RGBA8") ? 8f : f.ToString().Contains("ETC") ? 4f : f.ToString().Contains("RGBA32") ? 32f : f.ToString().Contains("RGB24") ? 24f : 8f;
            return (long)(w * h * bpp / 8f * (imp.mipmapEnabled ? 1.33f : 1f));
        }

        private IEnumerator WinSounds()
        {
            float t0 = 0f;
            WinAudio.Played += e => log.AppendLine($"    {Time.unscaledTime - t0:0.00} с — {e}");
            foreach (int stars in new[] { 1, 2, 3, 0 })
            {
                yield return new WaitForSecondsRealtime(1.2f);
                var gm = Object.FindFirstObjectByType<GameManager>();
                var wa = Object.FindFirstObjectByType<WinAudio>();
                var winAnim = GameObject.Find("WinAnimation");
                log.AppendLine($"Итог {stars}★: WinAudio {(wa != null ? "есть" : "НЕТ")}");
                t0 = Time.unscaledTime;
                gm.DebugWin(stars);
                gm.DebugWin(stars); // двойной вызов — второй игнорируется
                var src = winAnim != null ? winAnim.GetComponent<AudioSource>() : null;
                yield return new WaitForSecondsRealtime(5f);
                log.AppendLine($"    AudioSource WinAnimation: playOnAwake {(src != null && src.playOnAwake)}, играл {(src != null && src.time > 0f)}");
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
            yield return new WaitForSecondsRealtime(1f);
            File.WriteAllText("Builds/win-sound.txt", log.ToString());
            Debug.Log("[Polish] PlayChecks.WinSound " + log);
            SessionState.EraseString(Key);
            EditorApplication.Exit(0);
        }

        private void Shot(Scene scene, UiAudit.Res res, string name)
        {
            UiAudit.Layout lay = UiAudit.Apply(scene, res);
            foreach (Canvas c in lay.roots) c.sortingOrder = Mathf.Min(c.sortingOrder, 20000);
            // AspectRatioFitter фона пересчитать под эмулируемый экран (в Play Mode он держит размер от экрана batchmode)
            foreach (var f in Object.FindObjectsByType<UnityEngine.UI.AspectRatioFitter>(FindObjectsSortMode.None)) { f.enabled = false; f.enabled = true; }
            Canvas.ForceUpdateCanvases();
            UiAudit.Render(scene, lay, $"Builds/ui-shots/menuflame/{name}_{res.name}.png");
            log.AppendLine("кадр " + name);
        }

        private IEnumerator Transition(string scene, bool doubleTap, string label)
        {
            int before = loads;
            closedAtLoad = -1f;
            float maxClosed = 0f, start = Time.realtimeSinceStartup;
            bool blockedWhileMoving = true;
            SceneLoadGuard.Load(scene);
            if (doubleTap) SceneLoadGuard.Load(scene);
            yield return null;
            while ((SceneTransition.Busy || loads == before) && Time.realtimeSinceStartup - start < 15f)
            {
                maxClosed = Mathf.Max(maxClosed, SceneTransition.Closed);
                var blocker = GameObject.Find("SceneTransition/Blocker");
                if (blocker != null && !blocker.GetComponent<UnityEngine.UI.Image>().raycastTarget) blockedWhileMoving = false;
                yield return null;
            }
            float secs = Time.realtimeSinceStartup - start;
            var b = GameObject.Find("SceneTransition/Blocker");
            bool blockerOffAfter = b != null && !b.GetComponent<UnityEngine.UI.Image>().raycastTarget;
            bool ok = loads - before == 1 && closedAtLoad >= 0.999f && SceneTransition.Closed <= 0.001f && !SceneTransition.Busy && blockedWhileMoving && blockerOffAfter;
            log.AppendLine($"{label}: → {SceneManager.GetActiveScene().name}; загрузок {loads - before}; створки при включении сцены {closedAtLoad:0.000} " +
                           $"(макс. {maxClosed:0.000}); в конце {SceneTransition.Closed:0.000}; касания блокировались всё время перехода: {(blockedWhileMoving ? "да" : "нет")}; " +
                           $"после — блокер снят: {(blockerOffAfter ? "да" : "нет")}; {secs:0.00} с — {(ok ? "OK" : "ОШИБКА")}");
            yield return new WaitForSecondsRealtime(0.3f);
        }
    }
}
