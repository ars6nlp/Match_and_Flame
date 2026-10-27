using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Лаборатория уровней: проверка решаемости симулятором, поиск решений для подсказки,
/// сборка новых уровней (LevelDesigns) и отчёт LEVELS_DESIGN.md.
/// Запуск: Tools → Match &amp; Flame → … или batchmode -executeMethod LevelLab.&lt;метод&gt;.
/// </summary>
public static class LevelLab
{
    public static void Log(string message) => Debug.Log("[Polish] " + message);

    public static GameObject[] CatalogLevels()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Resources/LevelCatalog.asset");
        return catalog != null ? catalog.levels : new GameObject[0];
    }

    public struct Placement
    {
        public Vector2 center, root;
        public float rotation, phase;
        public LevelSimulator.Result result;
    }

    public struct SearchStats
    {
        public int tried, valid, threeStars;
        public List<Placement> solutions;
        public Placement best;      // 3★ с наибольшим числом соседей-3★ (устойчивое решение для подсказки)
        public bool hasBest;
        public float seconds;
        public float SolutionShare => valid > 0 ? (float)threeStars / valid : 0f;
    }

    /// <summary>
    /// Перебор позиций спички игрока: центр по сетке step в пределах поля, 4 поворота (как тапы по 90°),
    /// фазы Oscillator. Для уровней со слотом — только позиция слота.
    /// </summary>
    public static SearchStats Search(LevelSimulator sim, float step = 0.2f, float halfWidth = 2.4f, float halfHeight = 4.6f, float[] phases = null)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var stats = new SearchStats { solutions = new List<Placement>() };
        phases = phases ?? new[] { 0f };
        var grid = new Dictionary<(int, int, int), int>();

        if (!sim.FreePlacement)
        {
            foreach (Collider2D slot in sim.Slots)
            {
                float rot = slot.transform.eulerAngles.z;
                foreach (float ph in phases)
                {
                    var p = new Placement { center = slot.transform.position, rotation = rot, phase = ph, root = sim.PlayerRootFor(slot.transform.position, rot) };
                    p.result = sim.Run(p.root, rot, ph);
                    stats.tried++;
                    if (p.result.valid) stats.valid++;
                    if (p.result.valid && p.result.stars == 3) { stats.threeStars++; stats.solutions.Add(p); }
                }
            }
        }
        else
        {
            float start = sim.PlayerStartRotation;
            for (int r = 0; r < 4; r++)
            {
                float rot = Mathf.Repeat(start - 90f * r, 360f);
                for (int ix = 0; ix * step <= 2f * halfWidth + 0.001f; ix++)
                    for (int iy = 0; iy * step <= 2f * halfHeight + 0.001f; iy++)
                    {
                        var center = new Vector2(-halfWidth + ix * step, -halfHeight + iy * step);
                        int okPhases = 0;
                        foreach (float ph in phases)
                        {
                            var p = new Placement { center = center, rotation = rot, phase = ph, root = sim.PlayerRootFor(center, rot) };
                            p.result = sim.Run(p.root, rot, ph);
                            stats.tried++;
                            if (!p.result.valid) break; // наложение не зависит от фазы (кроме Oscillator — редкость)
                            stats.valid++;
                            if (p.result.stars == 3) { stats.threeStars++; stats.solutions.Add(p); okPhases++; }
                        }
                        if (okPhases > 0) grid[(r, ix, iy)] = okPhases;
                    }
            }
            // Устойчивое решение: больше всего соседей-3★ на сетке. Для подсказки берём его в «удобной» зоне —
            // целиком на экране 20:9 и не под кнопками; при равенстве — ближе к центру.
            int bestScore = -1;
            bool InComfortZone(Placement p) => Mathf.Abs(p.center.x) <= 1.3f && p.center.y >= -3.4f && p.center.y <= 1.6f;
            List<Placement> candidates = stats.solutions.Any(InComfortZone) ? stats.solutions.Where(InComfortZone).ToList() : stats.solutions;
            float bestDistance = float.MaxValue;
            foreach (Placement p in candidates)
            {
                int r = Mathf.RoundToInt(Mathf.Repeat(start - p.rotation, 360f) / 90f) % 4;
                int ix = Mathf.RoundToInt((p.center.x + halfWidth) / step), iy = Mathf.RoundToInt((p.center.y + halfHeight) / step);
                int score = 0;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        if (grid.TryGetValue((r, ix + dx, iy + dy), out int n)) score += n;
                float distance = p.center.magnitude;
                if (score > bestScore || (score == bestScore && distance < bestDistance))
                {
                    bestScore = score; bestDistance = distance; stats.best = p; stats.hasBest = true;
                }
            }
        }
        if (!stats.hasBest && stats.solutions.Count > 0) { stats.best = stats.solutions[0]; stats.hasBest = true; }
        stats.seconds = (float)watch.Elapsed.TotalSeconds;
        return stats;
    }

    public static float[] PhasesFor(LevelSimulator sim)
    {
        // Старые уровни (без капель и вентиляторов) — прежние 6 фаз маятника, чтобы регрессия сравнивалась 1:1
        if (!sim.HasMechanics)
            return sim.Root.GetComponentsInChildren<Oscillator>(false).Length > 0 ? new[] { 0f, 0.8f, 1.6f, 2.4f, 3.2f, 4f } : new[] { 0f };
        float period = sim.TimingPeriod();
        if (period <= 0f) return new[] { 0f };
        int n = Mathf.Clamp(Mathf.CeilToInt(period / 0.25f), 1, 32);
        var phases = new float[n];
        for (int i = 0; i < n; i++) phases[i] = period * i / n;
        return phases;
    }

    [MenuItem("Tools/Match & Flame/Level Lab/Search solutions (one level)")]
    public static void SearchTiming()
    {
        GameObject[] levels = CatalogLevels();
        foreach (int i in new[] { 5, 12 })
            using (var sim = new LevelSimulator(levels[i]))
            {
                SearchStats s = Search(sim, 0.2f, phases: PhasesFor(sim));
                Log($"L{i}: tried {s.tried}, valid {s.valid}, 3* {s.threeStars} ({s.SolutionShare:P1}), {s.seconds:0.0}s; best {(s.hasBest ? $"center ({s.best.center.x:0.00},{s.best.center.y:0.00}) rot {s.best.rotation:0} -> {s.best.result}" : "none")}");
            }
    }

    /// <summary>
    /// Регрессия уровней 0–16: полный перебор (шаг 0.2, 4 поворота, фазы маятников) и хеш всех исходов.
    /// Файл Builds/regression_{REG_TAG}.txt; два файла должны совпасть построчно до и после правок механик.
    /// </summary>
    [MenuItem("Tools/Match & Flame/Level Lab/Regression fingerprint (levels 0-16)")]
    public static void RegressionFingerprint()
    {
        string tag = System.Environment.GetEnvironmentVariable("REG_TAG") ?? "current";
        GameObject[] levels = CatalogLevels();
        var sb = new StringBuilder();
        for (int i = 0; i < levels.Length && i <= 16; i++)
            using (var sim = new LevelSimulator(levels[i]))
            {
                SearchStats s = Search(sim, 0.2f, phases: PhasesFor(sim));
                ulong hash = 1469598103934665603UL;
                float start = sim.PlayerStartRotation;
                float[] phases = PhasesFor(sim);
                for (int r = 0; r < 4; r++)
                    for (float x = -2.4f; x <= 2.401f; x += 0.4f)
                        for (float y = -4.6f; y <= 4.601f; y += 0.4f)
                            foreach (float ph in phases)
                            {
                                float rot = Mathf.Repeat(start - 90f * r, 360f);
                                LevelSimulator.Result res = sim.Run(sim.PlayerRootFor(new Vector2(x, y), rot), rot, ph);
                                int v = res.valid ? res.burnt * 4 + res.stars + 1 : 0;
                                hash = (hash ^ (ulong)v) * 1099511628211UL;
                            }
                sb.AppendLine($"L{i} valid {s.valid} threeStars {s.threeStars} hash {hash:X16}");
            }
        System.IO.Directory.CreateDirectory("Builds");
        System.IO.File.WriteAllText($"Builds/regression_{tag}.txt", sb.ToString());
        Log("RegressionFingerprint " + tag + "\n" + sb);
    }

    /// <summary>Missing Script во всех сценах сборки, префабах уровней, механик и эффектов.</summary>
    [MenuItem("Tools/Match & Flame/Level Lab/Verify missing scripts")]
    public static void VerifyMissingScripts()
    {
        var sb = new StringBuilder("VerifyMissingScripts\n");
        int total = 0;
        foreach (string path in new[] { "Assets/Scenes/MainScene.unity", "Assets/Scenes/LevelSelectionScene.unity", "Assets/Scenes/GameScene.unity" })
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);
            int n = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            sb.AppendLine($"  {path}: {n}");
            total += n;
        }
        int prefabs = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Levels", "Assets/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int n = root.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            PrefabUtility.UnloadPrefabContents(root);
            prefabs++;
            if (n > 0) sb.AppendLine($"  {path}: {n}");
            total += n;
        }
        sb.AppendLine($"  prefabs checked: {prefabs}; TOTAL missing scripts = {total}");
        Log(sb.ToString());
    }

    public const string MatchPrefabPath = "Assets/Prefabs/Match_Normal.prefab";

    /// <summary>
    /// Экземпляр Match_Normal (как в существующих уровнях), центр коллайдера в center, поворот rotZ.
    /// Не-игроку удаляется DraggableMatch (так же устроены все уровни).
    /// </summary>
    public static MatchLogic NewMatch(Transform parent, Vector2 center, float rotZ, bool player, string name = null)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MatchPrefabPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        if (name != null) go.name = name;
        var col = go.GetComponent<Collider2D>();
        Quaternion rot = Quaternion.Euler(0, 0, rotZ);
        Vector3 offset = rot * Vector3.Scale(col.offset, go.transform.lossyScale);
        go.transform.SetPositionAndRotation((Vector3)center - offset, rot);
        if (!player) Object.DestroyImmediate(go.GetComponent<DraggableMatch>());
        return go.GetComponent<MatchLogic>();
    }

    /// <summary>Карта связей: где спичка B загорается от горящей спички A (A в центре, огонь слева направо).</summary>
    [MenuItem("Tools/Match & Flame/Level Lab/Probe link map")]
    public static void ProbeLinks()
    {
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        var root = new GameObject("Probe");
        MatchLogic a = NewMatch(root.transform, Vector2.zero, 0f, player: true, name: "A");
        a.GetComponent<DraggableMatch>().freePlacementMode = true; // в пробе нет слота
        MatchLogic b = NewMatch(root.transform, new Vector2(5f, 5f), 0f, player: false, name: "B");
        var sb = new StringBuilder("ProbeLinks: A at (0,0) rot 0; head " + (Vector2)a.headPoint.position + " tail " + (Vector2)a.tailPoint.position + "\n");
        using (var sim = new LevelSimulator(root, owned: false))
        {
            Vector2 aRoot = sim.PlayerRootFor(Vector2.zero, 0f);
            Vector2 head = a.headPoint.position, tail = a.tailPoint.position;
            foreach (float theta in new[] { 0f, 90f, 180f, 270f })
            {
                sb.AppendLine($"  B rot {theta}: x -2.0..2.4, y +1.4 (top) .. -1.4 (bottom), step 0.1");
                for (int iy = 14; iy >= -14; iy--)
                {
                    var row = new StringBuilder("    ");
                    for (int ix = -20; ix <= 24; ix++)
                    {
                        var c = new Vector2(ix * 0.1f, iy * 0.1f);
                        if (Vector2.Distance(c, head) < 0.05f) { row.Append('H'); continue; }
                        if (Vector2.Distance(c, tail) < 0.05f) { row.Append('T'); continue; }
                        var bCol = b.GetComponent<Collider2D>();
                        Quaternion r = Quaternion.Euler(0, 0, theta);
                        sim.SetStartPose(b, c - (Vector2)(r * Vector3.Scale(bCol.offset, b.transform.lossyScale)), theta);
                        LevelSimulator.Result res = sim.Run(aRoot, 0f, 0f, 10f);
                        row.Append(!res.valid ? 'x' : sim.Burned(b) ? '#' : '.');
                    }
                    sb.AppendLine(row.ToString());
                }
            }
        }
        Object.DestroyImmediate(root);
        Log(sb.ToString());
    }

    public const string SolutionsPath = "Assets/Resources/LevelSolutions.asset";

    /// <summary>
    /// Поиск решений для всех уровней каталога; устойчивые решения free-уровней записываются
    /// в LevelSolutions (подсказка). Уровни, где 3★ требует двигать подвижные спички во время горения,
    /// статического решения не имеют — подсказка там скрыта.
    /// </summary>
    [MenuItem("Tools/Match & Flame/Level Lab/Solve all levels (hint data)")]
    public static void SolveAll()
    {
        GameObject[] levels = CatalogLevels();
        var solutions = new List<LevelSolutions.Solution>();
        var sb = new StringBuilder("SolveAll (step 0.1)\n");
        var designed = new HashSet<int>(LevelDesigns.All().Select(d => d.index)); // у них решение задано в LevelDesigns
        for (int i = 0; i < levels.Length; i++)
        {
            if (designed.Contains(i)) continue;
            using (var sim = new LevelSimulator(levels[i]))
            {
                SearchStats s = Search(sim, 0.1f, phases: PhasesFor(sim));
                int movable = sim.Root.GetComponentsInChildren<MatchLogic>(false).Count(m => m.isDraggable);
                sb.AppendLine($"  L{i} {(sim.FreePlacement ? "free" : "slot")} matches={sim.MatchCount} movable={movable} osc={sim.Root.GetComponentsInChildren<Oscillator>(false).Length}: " +
                              $"valid {s.valid}, 3* {s.threeStars} ({s.SolutionShare:P1}), {s.seconds:0.0}s" +
                              (s.hasBest ? $"; best c=({s.best.center.x:0.00},{s.best.center.y:0.00}) rot {s.best.rotation:0} ph {s.best.phase:0.0} -> {s.best.result}" : "; NO static 3* solution"));
                if (sim.FreePlacement && s.hasBest)
                    solutions.Add(new LevelSolutions.Solution { level = i, position = s.best.root, rotationZ = s.best.rotation });
            }
        }
        SaveSolutions(solutions, level => !designed.Contains(level));
        Log(sb.ToString());
    }

    /// <summary>Записать решения: записи уровней, для которых replace(level) == true, заменяются свежими.</summary>
    public static void SaveSolutions(List<LevelSolutions.Solution> fresh, System.Func<int, bool> replace)
    {
        var asset = AssetDatabase.LoadAssetAtPath<LevelSolutions>(SolutionsPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<LevelSolutions>();
            AssetDatabase.CreateAsset(asset, SolutionsPath);
        }
        var kept = (asset.solutions ?? new LevelSolutions.Solution[0]).Where(s => !replace(s.level) && fresh.All(f => f.level != s.level));
        asset.solutions = kept.Concat(fresh).OrderBy(s => s.level).ToArray();
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        Log($"LevelSolutions: {asset.solutions.Length} entries [{string.Join(", ", asset.solutions.Select(s => s.level))}]");
    }

    /// <summary>Геометрия существующих уровней и проверка уровней со слотом.</summary>
    [MenuItem("Tools/Match & Flame/Level Lab/Validate existing levels")]
    public static void ValidateExisting()
    {
        GameObject[] levels = CatalogLevels();
        var sb = new StringBuilder("ValidateExisting\n");
        for (int i = 0; i < levels.Length; i++)
        {
            using (var sim = new LevelSimulator(levels[i]))
            {
                sb.AppendLine($"L{i} {levels[i].name}: matches={sim.MatchCount} golden={sim.GoldenCount} free={sim.FreePlacement} slots={sim.Slots.Count()}");
                foreach (MatchLogic m in sim.Root.GetComponentsInChildren<MatchLogic>(false))
                {
                    var col = m.GetComponent<Collider2D>();
                    Vector2 c = col != null ? (Vector2)m.transform.TransformPoint(col.offset) : (Vector2)m.transform.position;
                    sb.AppendLine($"    {(m.GetComponent<DraggableMatch>() ? "P" : m.isGolden ? "G" : m.ignorePlayerMatch ? "I" : "-")} c=({c.x:0.00},{c.y:0.00}) rot={m.transform.eulerAngles.z:0} head=({m.headPoint.position.x:0.00},{m.headPoint.position.y:0.00}) tail=({m.tailPoint.position.x:0.00},{m.tailPoint.position.y:0.00}) burn={m.burnTime} fr={m.flashRange} fR={m.flashRadius} sr={m.spreadRadius} sd={m.sideSpreadDelay}");
                }
                if (!sim.FreePlacement)
                {
                    foreach (Collider2D slot in sim.Slots)
                    {
                        float rot = slot.transform.eulerAngles.z;
                        Vector2 root = sim.PlayerRootFor(slot.transform.position, rot);
                        sb.AppendLine($"    slot ({slot.transform.position.x:0.00},{slot.transform.position.y:0.00}) rot={rot:0} -> {sim.Run(root, rot)}");
                    }
                }
            }
        }
        Log(sb.ToString());
    }
}
