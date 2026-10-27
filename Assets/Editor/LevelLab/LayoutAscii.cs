using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// ASCII-схема уровня (шаг 0.25): «-» и «|» — видимая палочка, «o» — головка, «$» — золото, «#» — иммунная,
/// «P» — спичка игрока на старте, «D» — сопло крана и «:» столб капель, «U» — зонтик, «F» — вентилятор и «^» зона ветра,
/// «t» — подсказка. Источник: LAYOUT_SRC=design (таблица), prefab (каталог) или папка с префабами (ASCII_DIR).
/// Builds/layout-ascii-{метка}.txt
/// </summary>
public static class LayoutAscii
{
    const float Step = 0.25f, HW = 2.25f, Top = 3.25f, Bottom = -4.25f;

    [MenuItem("Tools/Match & Flame/Level Lab/Layout ASCII (17-21)")]
    public static void Run()
    {
        string src = System.Environment.GetEnvironmentVariable("LAYOUT_SRC") ?? "design";
        string dir = System.Environment.GetEnvironmentVariable("ASCII_DIR");
        string tag = System.Environment.GetEnvironmentVariable("ASCII_TAG") ?? src;
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Resources/LevelCatalog.asset");
        var sb = new StringBuilder();
        foreach (LevelDesigns.Level spec in LevelDesigns.All())
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = !string.IsNullOrEmpty(dir)
                ? (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{dir}/{spec.name}.prefab"))
                : src == "prefab" ? (GameObject)PrefabUtility.InstantiatePrefab(catalog.levels[spec.index]) : LevelBuilder.Build(spec);
            root.transform.position = Vector3.zero;
            Physics2D.SyncTransforms();
            sb.AppendLine($"L{spec.index} ({spec.name}, экран «Уровень {spec.index + 1}»):");
            sb.Append(Draw(root));
            Object.DestroyImmediate(root);
        }
        Directory.CreateDirectory("Builds");
        File.WriteAllText($"Builds/layout-ascii-{tag}.txt", sb.ToString());
        LevelLab.Log($"LayoutAscii {tag}\n" + sb);
    }

    public static string Draw(GameObject root)
    {
        int w = Mathf.RoundToInt(2 * HW / Step) + 1, h = Mathf.RoundToInt((Top - Bottom) / Step) + 1;
        var c = new char[w, h];
        for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) c[x, y] = ' ';
        int Cx(float v) => Mathf.RoundToInt((v + HW) / Step);
        int Cy(float v) => Mathf.RoundToInt((v - Bottom) / Step);
        void Put(Vector2 p, char ch) { int x = Cx(p.x), y = Cy(p.y); if (x >= 0 && y >= 0 && x < w && y < h) c[x, y] = ch; }

        foreach (Fan f in root.GetComponentsInChildren<Fan>())
            for (float a = 0.25f; a < f.ZoneLength; a += Step) Put(f.Nozzle + f.Direction * a, '^');
        foreach (WaterDripper d in root.GetComponentsInChildren<WaterDripper>())
            for (float y = d.NozzleWorld.y - Step; y > Bottom; y -= Step) Put(new Vector2(d.NozzleWorld.x, y), ':');
        foreach (WorldHintBacking hint in root.GetComponentsInChildren<WorldHintBacking>())
        {
            hint.Refit();
            Bounds b = hint.WorldRect;
            for (float x = b.min.x; x <= b.max.x; x += Step) for (float y = b.min.y; y <= b.max.y; y += Step) Put(new Vector2(x, y), 't');
        }
        foreach (MatchLogic m in root.GetComponentsInChildren<MatchLogic>())
        {
            LayoutCheck.Stick s = LayoutCheck.StickOf(m);
            bool horizontal = Mathf.Abs(s.b.x - s.a.x) > Mathf.Abs(s.b.y - s.a.y);
            char ch = m.GetComponent<DraggableMatch>() != null ? 'P' : m.isGolden ? '$' : m.ignorePlayerMatch ? '#' : horizontal ? '-' : '|';
            Vector2 a = s.a, b = s.b;
            for (float t = 0f; t <= 1.001f; t += 0.05f) Put(Vector2.Lerp(a, b, t), ch);
            Put(m.headPoint.position, 'o');
        }
        foreach (WaterDripper d in root.GetComponentsInChildren<WaterDripper>()) Put(d.NozzleWorld, 'D');
        foreach (WaterShelter u in root.GetComponentsInChildren<WaterShelter>()) Put(u.transform.position, 'U');
        foreach (Fan f in root.GetComponentsInChildren<Fan>()) Put(f.transform.position, 'F');

        var sb = new StringBuilder("    +" + new string('-', w) + "+\n");
        for (int y = h - 1; y >= 0; y--)
        {
            float vy = Bottom + y * Step;
            sb.Append(Mathf.Abs(vy - Mathf.Round(vy)) < 0.01f ? $"{vy,3:0} |" : "    |");
            for (int x = 0; x < w; x++) sb.Append(c[x, y]);
            sb.AppendLine("|");
        }
        sb.AppendLine("    +" + new string('-', w) + "+   x: −2.25 … +2.25, шаг 0.25");
        return sb.ToString();
    }
}
