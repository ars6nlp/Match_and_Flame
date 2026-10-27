using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Кадры горения и замер нагрузки в Play Mode (batchmode без -nographics, без -quit):
///  • BurnShots.Preview — сцена Dev/BurnFxPreview: поджог цепочки, кадры каждые SHOT_STEP с (Builds/preview/burn_*.png);
///  • BurnShots.Level — GameScene с уровнем BURN_LEVEL (по умолчанию 7): спичка игрока в решении из LevelSolutions,
///    кадры и пик нагрузки (частицы, системы, огни со светом) — Builds/preview/burn_L{n}_*.png и burn-load.txt.
/// По окончании Unity закрывается сам.
/// </summary>
[InitializeOnLoad]
public static class BurnShots
{
    private const string Key = "BurnShots.Mode";
    static BurnShots()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetString(Key, "") != "")
            {
                var go = new GameObject("BurnShotsRunner");
                go.AddComponent<Runner>().mode = SessionState.GetString(Key, "");
            }
        };
    }

    public static void Preview()
    {
        PlayerPrefs.SetInt("EquippedFireSkin", int.Parse(System.Environment.GetEnvironmentVariable("BURN_SKIN") ?? "0"));
        PlayerPrefs.Save();
        Start("preview", "Assets/Scenes/Dev/BurnFxPreview.unity");
    }

    public static void Level()
    {
        int level = int.Parse(System.Environment.GetEnvironmentVariable("BURN_LEVEL") ?? "7");
        PlayerPrefs.SetInt("CurrentLevel", level);
        PlayerPrefs.SetInt("EquippedFireSkin", int.Parse(System.Environment.GetEnvironmentVariable("BURN_SKIN") ?? "0")); // обычный огонь
        PlayerPrefs.Save();
        Start("level", "Assets/Scenes/GameScene.unity");
    }

    private static void Start(string mode, string scene)
    {
        SessionState.SetString(Key, mode);
        EditorSceneManager.OpenScene(scene);
        EditorApplication.isPlaying = true;
    }

    private class Runner : MonoBehaviour
    {
        public string mode;
        private float t;
        private int shot;
        private int peakParticles, peakSystems, peakLights, peakBurning, peakBatches, peakSetPass;
        private long peakMemory;
        private readonly StringBuilder log = new StringBuilder();

        private System.Collections.IEnumerator Start()
        {
            float step = float.Parse(System.Environment.GetEnvironmentVariable("SHOT_STEP") ?? "0.5", System.Globalization.CultureInfo.InvariantCulture);
            int count = int.Parse(System.Environment.GetEnvironmentVariable("SHOT_COUNT") ?? "3");
            string tag = mode == "preview" ? "preview" : "L" + PlayerPrefs.GetInt("CurrentLevel", 0);
            yield return new WaitForSeconds(1.2f); // двери/вход уровня
            if (mode == "preview") FindFirstObjectByType<BurnFxPreview>().Ignite();
            else
            {
                DraggableMatch player = FindFirstObjectByType<DraggableMatch>();
                int level = PlayerPrefs.GetInt("CurrentLevel", 0);
                if (LevelSolutions.Current != null && LevelSolutions.Current.TryGet(level, out LevelSolutions.Solution sol))
                    player.transform.SetPositionAndRotation(sol.position, Quaternion.Euler(0f, 0f, sol.rotationZ));
                Physics2D.SyncTransforms();
                player.GetComponent<MatchLogic>().Ignite();
            }
            float start = Time.time;
            float delay = float.Parse(System.Environment.GetEnvironmentVariable("SHOT_DELAY") ?? "0.3", System.Globalization.CultureInfo.InvariantCulture);
            yield return new WaitForSeconds(delay);
            for (shot = 0; shot < count; shot++)
            {
                Capture($"Builds/preview/burn_{tag}_{shot}.png");
                // SHOT_WATER=1: после первого кадра потушить «водой» спичку в сцене превью
                if (shot == 0 && mode == "preview" && System.Environment.GetEnvironmentVariable("SHOT_WATER") == "1")
                    FindFirstObjectByType<BurnFxPreview>().Water();
                yield return new WaitForSeconds(step);
            }
            while (Time.time - start < 25f && FindObjectsByType<MatchLogic>(FindObjectsSortMode.None).Any(m => m.isBurning)) yield return null;
            yield return new WaitForSeconds(1f);
            log.AppendLine($"{tag}: пик частиц {peakParticles}, систем частиц {peakSystems}, огней со светом {peakLights}, одновременно горящих спичек {peakBurning}, батчей {peakBatches} (SetPass {peakSetPass}), память выделено {peakMemory / (1024 * 1024)} МБ");
            File.AppendAllText("Builds/burn-load.txt", log.ToString());
            Debug.Log("[Polish] BurnShots " + log);
            SessionState.EraseString(Key);
            EditorApplication.Exit(0);
        }

        private void Update()
        {
            int particles = 0, systems = 0;
            foreach (ParticleSystem ps in FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                if (ps.isPlaying || ps.particleCount > 0) { systems++; particles += ps.particleCount; }
            peakParticles = Mathf.Max(peakParticles, particles);
            peakSystems = Mathf.Max(peakSystems, systems);
            peakLights = Mathf.Max(peakLights, FireLight.ActiveCount);
            peakBurning = Mathf.Max(peakBurning, FindObjectsByType<MatchLogic>(FindObjectsSortMode.None).Count(m => m.isBurning));
            peakBatches = Mathf.Max(peakBatches, UnityStats.batches);
            peakSetPass = Mathf.Max(peakSetPass, UnityStats.setPassCalls);
            peakMemory = System.Math.Max(peakMemory, UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong());
        }

        private static void Capture(string path)
        {
            Camera cam = Camera.main;
            int w = int.Parse(System.Environment.GetEnvironmentVariable("SHOT_W") ?? "1080"), h = int.Parse(System.Environment.GetEnvironmentVariable("SHOT_H") ?? "1920");
            var rt = new RenderTexture(w, h, 24);
            RenderTexture prev = cam.targetTexture;
            float aspect = cam.aspect;
            cam.aspect = w / (float)h;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = prev;
            cam.aspect = aspect;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
            rt.Release();
        }
    }
}
