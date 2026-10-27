using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Кадр уровней 0–21 на 16:9, 20:9, 4:3: камера как GameManager.FitCameraToLevel (по коллайдерам, отъезд ≤ 1.5),
/// содержимое уровня (коллайдеры спичек и слотов, спрайты механик, размах маятников, тексты подсказок) должно быть
/// в кадре, ниже HUD (вырез 100 px + верхняя панель) и выше жестовой панели (60 px); тексты не должны лежать на спичках.
/// LevelFraming.Report → Builds/level-framing.md; LevelFraming.Shots (нужен GPU) → Builds/ui-shots/after/levels/.
/// </summary>
public static class LevelFraming
{
    private static readonly UiAudit.Res[] Aspects = { UiAudit.Resolutions[0], UiAudit.Resolutions[1], UiAudit.Resolutions[4] };
    private const float HudCanvasUnits = 20f + 140f; // отступ и высота верхней панели HUD (без зазора), единицы Canvas
    private const float MaxZoom = 1.5f, SideMargin = 0.5f, BaseSize = 5f; // SideMargin — как GameManager.cameraSideMargin

    /// <summary>Размер камеры как в GameManager.FitCameraToLevel для эмулируемого экрана (вырез и жестовая панель как в UiAudit).</summary>
    public static float FitCameraSize(GameObject level, UiAudit.Res r)
    {
        CameraFit.LevelContent(level, out Bounds content);
        VisualTheme theme = AssetDatabase.LoadAssetAtPath<VisualTheme>("Assets/Resources/VisualTheme.asset");
        CameraFit.Reserves(theme, r.w, r.h, UiAudit.NotchPx, UiAudit.GesturePx, r.density, out float top, out float bottom);
        return CameraFit.Size(content, Vector2.zero, r.w / (float)r.h, BaseSize, SideMargin, theme.levelVerticalMargin, top, bottom, MaxZoom);
    }

    private struct Item { public string name; public Bounds b; public bool text, match, swing; }

    private static List<Item> Content(GameObject level)
    {
        var items = new List<Item>();
        foreach (Collider2D c in level.GetComponentsInChildren<Collider2D>())
            items.Add(new Item { name = c.name, b = c.bounds, match = c.GetComponent<MatchLogic>() != null });
        foreach (SpriteRenderer sr in level.GetComponentsInChildren<SpriteRenderer>())
            if (sr.GetComponentInParent<MatchLogic>() == null && sr.sprite != null) items.Add(new Item { name = sr.name, b = sr.bounds });
        foreach (TextMeshPro t in level.GetComponentsInChildren<TextMeshPro>())
        {
            t.ForceMeshUpdate();
            Bounds lb = t.textBounds;
            if (lb.size == Vector3.zero) continue;
            Vector3 min = t.transform.TransformPoint(lb.min), max = t.transform.TransformPoint(lb.max);
            var b = new Bounds((min + max) / 2f, new Vector3(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y), 1f));
            items.Add(new Item { name = "text \"" + t.text.Replace("\n", " ") + "\"", b = b, text = true });
        }
        // Маятники: весь размах
        foreach (Oscillator o in level.GetComponentsInChildren<Oscillator>())
        {
            Vector3 d = o.movementAxis.normalized * o.distance;
            foreach (Collider2D c in o.GetComponentsInChildren<Collider2D>())
            {
                Bounds b = c.bounds; b.Encapsulate(c.bounds.min + d); b.Encapsulate(c.bounds.max + d); b.Encapsulate(c.bounds.min - d); b.Encapsulate(c.bounds.max - d);
                items.Add(new Item { name = c.name + " (swing)", b = b, swing = true });
            }
        }
        return items;
    }

    /// <summary>Камера, фон и виньетка как в игре после загрузки уровня (FitCameraToLevel, BackgroundCover, LevelAmbience).</summary>
    public static void FrameLikeGame(GameObject level, UiAudit.Res r)
    {
        Physics2D.SyncTransforms();
        PlayfieldBacking.Build(level); // как GameManager при загрузке
        foreach (WorldHintBacking h in level.GetComponentsInChildren<WorldHintBacking>()) h.Refit(); // в игре — LateUpdate после смены языка
        Camera cam = Object.FindFirstObjectByType<Camera>();
        cam.orthographicSize = FitCameraSize(level, r);
        cam.aspect = r.w / (float)r.h;
        foreach (BackgroundCover bg in Object.FindObjectsByType<BackgroundCover>(FindObjectsSortMode.None)) bg.Fit(cam);
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        foreach (LevelAmbience amb in Object.FindObjectsByType<LevelAmbience>(FindObjectsSortMode.None))
        {
            typeof(LevelAmbience).GetField("cam", flags).SetValue(amb, cam);
            typeof(LevelAmbience).GetMethod("Fit", flags).Invoke(amb, null);
        }
    }

    [MenuItem("Tools/Match & Flame/UI Lab/Level framing report")]
    public static void Report()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Resources/LevelCatalog.asset");
        var sb = new StringBuilder("# Кадр уровней (камера как FitCameraToLevel)\n\n| Уровень | Экран | Камера (×) | Проблема |\n|---|---|---|---|\n");
        int problems = 0;
        for (int i = 0; i < catalog.levels.Length; i++)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var level = (GameObject)PrefabUtility.InstantiatePrefab(catalog.levels[i]);
            level.transform.position = Vector3.zero;
            Physics2D.SyncTransforms();
            List<Item> items = Content(level);
            foreach (UiAudit.Res r in Aspects)
            {
                float aspect = r.w / (float)r.h;
                float size = FitCameraSize(level, r);
                float pxPerUnit = r.h / (2f * size);
                float canvasScale = Mathf.Min(r.w / 1080f, r.h / 1920f);
                float hudBottom = size - (UiAudit.NotchPx + HudCanvasUnits * canvasScale) / pxPerUnit;
                float bottom = -size + UiAudit.GesturePx / pxPerUnit;
                float halfW = size * aspect;
                var issues = new List<string>();
                foreach (Item it in items)
                {
                    if (it.swing) continue; // размах маятников уходит за край и на эталонном 9:16 — так задумано (см. GameManager)
                    if (it.b.min.x < -halfW - 0.01f || it.b.max.x > halfW + 0.01f) issues.Add($"{it.name}: за боковым краем (x {it.b.min.x:0.00}…{it.b.max.x:0.00}, кадр ±{halfW:0.00})");
                    if (it.b.max.y > hudBottom) issues.Add($"{it.name}: под HUD/вырезом (верх {it.b.max.y:0.00} > {hudBottom:0.00})");
                    if (it.b.min.y < bottom) issues.Add($"{it.name}: под жестовой панелью (низ {it.b.min.y:0.00} < {bottom:0.00})");
                }
                foreach (Item t in items.Where(x => x.text))
                    foreach (Item m in items.Where(x => x.match))
                        if (t.b.Intersects(m.b))
                        {
                            Bounds a = t.b, b = m.b;
                            float w = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x), h = Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y);
                            if (w * h > 0.05f * a.size.x * a.size.y) issues.Add($"{t.name} лежит на спичке {m.name} ({w * h / (a.size.x * a.size.y):P0} текста)");
                        }
                foreach (string s in issues.Distinct()) { sb.AppendLine($"| {i} | {r.name} | {size / BaseSize:0.00} | {s} |"); problems++; }
                if (issues.Count == 0) sb.AppendLine($"| {i} | {r.name} | {size / BaseSize:0.00} | — |");
            }
            Object.DestroyImmediate(level);
        }
        Directory.CreateDirectory("Builds");
        File.WriteAllText("Builds/level-framing.md", sb.ToString());
        Debug.Log($"[Polish] LevelFraming: {problems} problems");
    }

    /// <summary>Скриншоты всех уровней с HUD (GameScene, камера как в игре). UI_RES — разрешения, по умолчанию 1080x1920.</summary>
    [MenuItem("Tools/Match & Flame/UI Lab/Level screenshots (needs GPU)")]
    public static void Shots()
    {
        string only = System.Environment.GetEnvironmentVariable("UI_RES") ?? "1080x1920";
        string lang = System.Environment.GetEnvironmentVariable("UI_LANG") ?? "en";
        string outDir = (System.Environment.GetEnvironmentVariable("UI_OUT") ?? "Builds/ui-shots/after/levels") + (lang == "ru" ? "_ru" : "");
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Resources/LevelCatalog.asset");
        var hud = UiAudit.States().First(st => st.name == "hud");
        int ok = 0;
        string levels = System.Environment.GetEnvironmentVariable("UI_LEVELS"); // например 17,18 — только эти уровни
        for (int i = 0; i < catalog.levels.Length; i++)
            if (string.IsNullOrEmpty(levels) || levels.Split(',').Contains(i.ToString()))
            foreach (UiAudit.Res r in UiAudit.Resolutions.Where(x => only.Split(',').Contains(x.name)))
            {
                Scene s = EditorSceneManager.OpenScene(UiAudit.Game, OpenSceneMode.Single);
                hud.setup(s);
                var level = (GameObject)PrefabUtility.InstantiatePrefab(catalog.levels[i], s);
                level.transform.position = Vector3.zero;
                UiAudit.EmulateRuntime(s, lang);
                FrameLikeGame(level, r);
                PlayfieldBacking.Build(level); // как GameManager при загрузке уровня
                foreach (var lbl in s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LevelNumberLabel>(true)))
                    lbl.GetComponent<TMP_Text>().text = (lang == "ru" ? "Уровень " : "Level ") + (i + 1);
                UiAudit.Layout lay = UiAudit.Apply(s, r);
                if (UiAudit.Render(s, lay, $"{outDir}/L{i:00}_{r.name}.png", level)) ok++;
            }
        Debug.Log($"[Polish] LevelFraming.Shots: {ok} PNG");
    }
}
