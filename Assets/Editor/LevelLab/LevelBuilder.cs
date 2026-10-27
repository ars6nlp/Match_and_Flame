using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Сборка и проверка новых уровней из LevelDesigns.
///  • EvaluateDesigns — только оценка (ничего не сохраняет);
///  • BuildNewLevels — проверка + сохранение Level_17…21, LevelCatalog, GameManager.levelPrefabs,
///    LevelSolutions и отчёт LEVELS_DESIGN.md.
/// Уровень принимается, если задуманное решение даёт 3★ (и собирает всё золото), а ни одна из 20 случайных
/// неправильных расстановок не даёт 3★.
/// </summary>
public static class LevelBuilder
{
    public static readonly Color ImmuneTint = new Color(0.45f, 0.68f, 1f, 1f);
    private const string LevelsFolder = "Assets/Levels";

    [MenuItem("Tools/Match & Flame/Level Lab/Evaluate new level designs")]
    public static void EvaluateDesigns()
    {
        var sb = new StringBuilder("EvaluateDesigns\n");
        string filter = System.Environment.GetEnvironmentVariable("LEVELS");
        foreach (LevelDesigns.Level spec in LevelDesigns.All())
        {
            if (!string.IsNullOrEmpty(filter) && !filter.Split(',').Contains(spec.index.ToString())) continue;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = Build(spec);
            Evaluation e = Evaluate(spec, root);
            sb.AppendLine(Describe(e));
            Object.DestroyImmediate(root);
        }
        LevelLab.Log(sb.ToString());
    }

    /// <summary>
    /// Карта уровня (шаг 0.2): спички — цифры/буквы по их пути огня (G — золотая, I — иммунная),
    /// поверх — клетки, где спичка игрока даёт 3★: маска поворотов 1=→ 2=↓ 4=← 8=↑ (шестнадцатеричная).
    /// Переменная окружения LEVELS=18,19 ограничивает набор уровней.
    /// </summary>
    [MenuItem("Tools/Match & Flame/Level Lab/Map new level designs")]
    public static void MapDesigns()
    {
        string filter = System.Environment.GetEnvironmentVariable("LEVELS");
        var sb = new StringBuilder("MapDesigns\n");
        foreach (LevelDesigns.Level spec in LevelDesigns.All())
        {
            if (!string.IsNullOrEmpty(filter) && !filter.Split(',').Contains(spec.index.ToString())) continue;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = Build(spec);
            Evaluation e = Evaluate(spec, root);
            sb.AppendLine(Describe(e));
            if (e.search.hasBest) sb.AppendLine($"    best: center ({e.search.best.center.x:0.00}, {e.search.best.center.y:0.00}) rot {e.search.best.rotation:0} phase {e.search.best.phase:0.0} -> {e.search.best.result}");
            if (root.GetComponentsInChildren<Oscillator>().Length > 0)
                using (var sim = new LevelSimulator(root, owned: false))
                {
                    string Phases(Vector2 c, float rot)
                    {
                        var p = new StringBuilder();
                        for (float ph = 0f; ph < 6.3f; ph += 0.2f)
                        {
                            LevelSimulator.Result r = sim.Run(sim.PlayerRootFor(c, rot), rot, ph);
                            p.Append(!r.valid ? 'x' : r.stars == 3 ? '#' : "0123456789abcdefghijk"[r.burnt]);
                        }
                        return p.ToString();
                    }
                    sb.AppendLine($"    phases 0..6.2 step 0.2 — intended: {Phases(spec.solutionCenter, spec.solutionRot)}");
                    if (System.Environment.GetEnvironmentVariable("VERBOSE") == "1")
                        for (float ph = 0f; ph < Mathf.Max(0.2f, sim.TimingPeriod()); ph += 0.2f)
                        {
                            sim.Run(sim.PlayerRootFor(spec.solutionCenter, spec.solutionRot), spec.solutionRot, ph);
                            var lost = root.GetComponentsInChildren<MatchLogic>().Where(m => !sim.Burned(m)).Select(m => m.name.Replace("Match_", "") + (sim.Extinguished(m) ? "(out)" : ""));
                            sb.AppendLine($"      t={ph:0.0}: unburnt [{string.Join(" ", lost)}]");
                        }
                    if (e.search.hasBest) sb.AppendLine($"                              best:     {Phases(e.search.best.center, e.search.best.rotation)}");
                }
            sb.Append(Map(root, e));
            // PNG-превью дизайна: спичка игрока — в задуманном решении
            Transform pl = root.GetComponentsInChildren<DraggableMatch>()[0].transform;
            Vector3 pp = pl.position; Quaternion pr = pl.rotation;
            pl.SetPositionAndRotation(e.solutionRoot, Quaternion.Euler(0f, 0f, spec.solutionRot));
            LevelPreview.ExportRoot(root, $"design_{spec.index}");
            pl.SetPositionAndRotation(pp, pr);
            if (System.Environment.GetEnvironmentVariable("VERBOSE") == "1")
                foreach (MatchLogic m in root.GetComponentsInChildren<MatchLogic>(false))
                    sb.AppendLine($"      {m.name}: head ({m.headPoint.position.x:0.00},{m.headPoint.position.y:0.00}) tail ({m.tailPoint.position.x:0.00},{m.tailPoint.position.y:0.00}) rot {m.transform.eulerAngles.z:0}{(m.isGolden ? " gold" : "")}{(m.ignorePlayerMatch ? " immune" : "")}");
            sb.Append(Links(root, System.Environment.GetEnvironmentVariable("VERBOSE") == "1"));
            Object.DestroyImmediate(root);
        }
        LevelLab.Log(sb.ToString());
    }

    /// <summary>Что сгорает, если поджечь одну спичку уровня (буквы — как на карте).</summary>
    public static string Links(GameObject root, bool verbose = false)
    {
        var all = root.GetComponentsInChildren<MatchLogic>(false).Where(m => m.GetComponent<DraggableMatch>() == null).ToList();
        string Label(int i) => all[i].isGolden ? $"G{i}" : all[i].ignorePlayerMatch ? $"I{i}" : "abcdefghijklmnopqrstuvwxyz"[i % 26].ToString();
        var sb = new StringBuilder("      links (lit -> burns):");
        using (var sim = new LevelSimulator(root, owned: false))
            for (int i = 0; i < all.Count; i++)
            {
                sim.RunFrom(all[i]);
                var burnt = Enumerable.Range(0, all.Count).Where(j => j != i && sim.Burned(all[j])).Select(j => Label(j) + (verbose ? "<" + sim.LitBy(all[j]).Replace("Match_", "") : ""));
                sb.Append($" {Label(i)}->[{string.Join(" ", burnt)}]");
            }
        return sb.AppendLine().ToString();
    }

    public static string Map(GameObject root, Evaluation e, bool overlay = true)
    {
        const float step = 0.2f, hw = 2.4f, hh = 4.6f;
        int w = Mathf.RoundToInt(2 * hw / step) + 1, h = Mathf.RoundToInt(2 * hh / step) + 1;
        var cells = new char[w, h];
        for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) cells[x, y] = '.';
        int Cx(float v) => Mathf.RoundToInt((v + hw) / step);
        int Cy(float v) => Mathf.RoundToInt((v + hh) / step);
        bool In(int x, int y) => x >= 0 && y >= 0 && x < w && y < h;

        // Решения (маска поворотов)
        var masks = new Dictionary<(int, int), int>();
        float start = 0f;
        foreach (var p in overlay ? e.search.solutions : new List<LevelLab.Placement>())
        {
            int r = Mathf.RoundToInt(Mathf.Repeat(start - p.rotation, 360f) / 90f) % 4; // 0 →, 1 ↓, 2 ←, 3 ↑
            var key = (Cx(p.center.x), Cy(p.center.y));
            masks.TryGetValue(key, out int m);
            masks[key] = m | (1 << r);
        }
        foreach (var kv in masks) if (In(kv.Key.Item1, kv.Key.Item2)) cells[kv.Key.Item1, kv.Key.Item2] = "0123456789ABCDEF"[kv.Value];

        // Спички
        int idx = 0;
        foreach (MatchLogic m in root.GetComponentsInChildren<MatchLogic>(false))
        {
            bool player = m.GetComponent<DraggableMatch>() != null;
            char c = player ? 'P' : m.isGolden ? 'G' : m.ignorePlayerMatch ? 'I' : "abcdefghijklmnopqrstuvwxyz"[idx % 26];
            if (!player) idx++;
            Vector2 a = m.headPoint.position, b = m.tailPoint.position;
            for (float t = 0f; t <= 1.001f; t += 0.1f)
            {
                Vector2 q = Vector2.Lerp(a, b, t);
                int x = Cx(q.x), y = Cy(q.y);
                if (In(x, y)) cells[x, y] = c;
            }
            int hx = Cx(a.x), hy = Cy(a.y);
            if (In(hx, hy)) cells[hx, hy] = '*'; // головка
        }
        // Механики: D — кран (и столб капель «:» до первого препятствия), U — зонтик, F — вентилятор, «>» — сопло
        foreach (WaterDripper d in root.GetComponentsInChildren<WaterDripper>())
        {
            Vector2 n = d.NozzleWorld;
            for (float yy = n.y - 0.2f; yy > -4.6f; yy -= step)
            {
                int x = Cx(n.x), y = Cy(yy);
                if (!In(x, y)) break;
                if (cells[x, y] != '.' && cells[x, y] != ':') break;
                cells[x, y] = ':';
            }
            if (In(Cx(n.x), Cy(n.y))) cells[Cx(n.x), Cy(n.y)] = 'D';
        }
        foreach (WaterShelter u in root.GetComponentsInChildren<WaterShelter>())
            if (In(Cx(u.transform.position.x), Cy(u.transform.position.y))) cells[Cx(u.transform.position.x), Cy(u.transform.position.y)] = 'U';
        foreach (Fan f in root.GetComponentsInChildren<Fan>())
        {
            if (In(Cx(f.transform.position.x), Cy(f.transform.position.y))) cells[Cx(f.transform.position.x), Cy(f.transform.position.y)] = 'F';
            if (In(Cx(f.Nozzle.x), Cy(f.Nozzle.y))) cells[Cx(f.Nozzle.x), Cy(f.Nozzle.y)] = '>';
        }
        var sb = new StringBuilder();
        for (int y = h - 1; y >= 0; y--)
        {
            sb.Append(y % 5 == 0 ? $"{-hh + y * step,5:0.0} " : "      ");
            for (int x = 0; x < w; x++) sb.Append(cells[x, y]);
            sb.AppendLine();
        }
        sb.AppendLine("      x: -2.4 … 0 … +2.4 (step 0.2); '*' = head");
        return sb.ToString();
    }

    // ---------------------------------------------------------------- Сохранение

    private const string CatalogPath = "Assets/Resources/LevelCatalog.asset";
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";

    /// <summary>
    /// Проверить и сохранить Level_17…21: префабы, LevelCatalog (конец списка + огоньки сложности),
    /// GameManager.levelPrefabs в GameScene, задуманные решения в LevelSolutions (для подсказки).
    /// Если хоть один уровень не прошёл проверку — ничего не сохраняется.
    /// </summary>
    [MenuItem("Tools/Match & Flame/Level Lab/Build new levels (17-21)")]
    public static void BuildNewLevels()
    {
        var sb = new StringBuilder("BuildNewLevels\n");
        // LEVELS=17,18 — собрать и сохранить только эти уровни (остальные префабы не трогаются)
        string filter = System.Environment.GetEnvironmentVariable("LEVELS");
        var specs = LevelDesigns.All().Where(sp => string.IsNullOrEmpty(filter) || filter.Split(',').Contains(sp.index.ToString())).ToList();
        var roots = new List<GameObject>();
        var evals = new List<Evaluation>();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        bool ok = true;
        foreach (LevelDesigns.Level spec in specs)
        {
            GameObject root = Build(spec);
            root.transform.position = new Vector3(0f, 1000f * (roots.Count + 1), 0f); // не мешают друг другу
            roots.Add(root);
        }
        for (int i = 0; i < specs.Count; i++)
        {
            foreach (GameObject other in roots) other.SetActive(other == roots[i]);
            roots[i].transform.position = Vector3.zero;
            Evaluation e = Evaluate(specs[i], roots[i]);
            evals.Add(e);
            List<string> ghosts = GhostCheck(roots[i], e.missingScripts);
            sb.AppendLine(Describe(e) + (ghosts.Count > 0 ? " | GHOSTS: " + string.Join("; ", ghosts) : " | ghosts: none"));
            if (!e.Passed || ghosts.Count > 0) ok = false;
        }
        if (!ok)
        {
            foreach (GameObject r in roots) Object.DestroyImmediate(r);
            LevelLab.Log(sb.AppendLine("NOT SAVED: fix the designs above").ToString());
            return;
        }

        var prefabs = new List<GameObject>();
        for (int i = 0; i < specs.Count; i++)
        {
            roots[i].SetActive(true);
            roots[i].transform.position = Vector3.zero;
            string path = $"{LevelsFolder}/{specs[i].name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(roots[i], path, out bool saved);
            if (!saved) { LevelLab.Log(sb.AppendLine("ERROR saving " + path).ToString()); return; }
            prefabs.Add(prefab);
            sb.AppendLine($"  saved {path}");
        }
        foreach (GameObject r in roots) Object.DestroyImmediate(r);

        // Каталог — единственный источник списка уровней
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        var levels = catalog.levels.ToList();
        var difficulty = (catalog.difficulty ?? new int[0]).ToList();
        for (int i = 0; i < specs.Count; i++)
        {
            int index = specs[i].index;
            while (levels.Count <= index) levels.Add(null);
            while (difficulty.Count <= index) difficulty.Add(1);
            levels[index] = prefabs[i];
            difficulty[index] = specs[i].difficulty;
        }
        if (levels.Any(l => l == null)) { LevelLab.Log(sb.AppendLine("ERROR: gap in LevelCatalog").ToString()); return; }
        catalog.levels = levels.ToArray();
        catalog.difficulty = difficulty.ToArray();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        sb.AppendLine($"  LevelCatalog: {catalog.levels.Length} levels, difficulty [{string.Join(",", catalog.difficulty)}]");

        // Запасной список в GameManager держим равным каталогу
        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        var gm = Object.FindFirstObjectByType<GameManager>();
        var so = new SerializedObject(gm);
        SerializedProperty list = so.FindProperty("levelPrefabs");
        list.arraySize = catalog.levels.Length;
        for (int i = 0; i < catalog.levels.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = catalog.levels[i];
        so.ApplyModifiedProperties();
        EditorSceneManager.SaveScene(scene);
        sb.AppendLine($"  GameScene: GameManager.levelPrefabs = {catalog.levels.Length}");

        // Задуманные решения — для подсказки за rewarded
        var fresh = evals.Select(e => new LevelSolutions.Solution { level = e.spec.index, position = e.solutionRoot, rotationZ = e.spec.solutionRot }).ToList();
        LevelLab.SaveSolutions(fresh, level => specs.Any(s => s.index == level));
        LevelLab.Log(sb.ToString());
    }

    /// <summary>Проверка уже сохранённых префабов из каталога: решение из LevelSolutions даёт 3★, нет призраков.</summary>
    [MenuItem("Tools/Match & Flame/Level Lab/Validate saved new levels")]
    public static void ValidateSaved()
    {
        var sb = new StringBuilder("ValidateSaved\n");
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        var solutions = Resources.Load<LevelSolutions>("LevelSolutions");
        foreach (LevelDesigns.Level spec in LevelDesigns.All())
        {
            GameObject prefab = catalog.levels[spec.index];
            int missing = prefab.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            using (var sim = new LevelSimulator(prefab))
            {
                List<string> ghosts = GhostCheck(sim.Root, missing);
                solutions.TryGet(spec.index, out LevelSolutions.Solution sol);
                LevelSimulator.Result r = sim.Run(sol.position, sol.rotationZ, spec.solutionPhase);
                sb.AppendLine($"  L{spec.index} {prefab.name}: solution {r} | missing scripts {missing} | ghosts {(ghosts.Count == 0 ? "none" : string.Join("; ", ghosts))}");
            }
        }
        LevelLab.Log(sb.ToString());
        Schematics();
    }

    /// <summary>«Призраки»: Missing Script, выключенные объекты, спички без коллайдера/спрайта/точек, дубликаты в одной позе.</summary>
    public static List<string> GhostCheck(GameObject root, int missingScripts)
    {
        var issues = new List<string>();
        if (missingScripts > 0) issues.Add($"{missingScripts} missing script(s)");
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (!t.gameObject.activeSelf) issues.Add($"inactive '{t.name}'");
        MatchLogic[] all = root.GetComponentsInChildren<MatchLogic>(true);
        foreach (MatchLogic m in all)
            if (m.GetComponent<Collider2D>() == null || m.GetComponent<SpriteRenderer>() == null || m.headPoint == null || m.tailPoint == null)
                issues.Add($"incomplete match '{m.name}'");
        for (int i = 0; i < all.Length; i++)
            for (int j = i + 1; j < all.Length; j++)
                if (Vector2.Distance(all[i].transform.position, all[j].transform.position) < 0.05f &&
                    Mathf.Abs(Mathf.DeltaAngle(all[i].transform.eulerAngles.z, all[j].transform.eulerAngles.z)) < 1f)
                    issues.Add($"duplicate '{all[i].name}'/'{all[j].name}'");
        if (all.Count(m => m.GetComponent<DraggableMatch>() != null) != 1) issues.Add("player match count != 1");
        return issues;
    }

    /// <summary>Схема для LEVELS_DESIGN.md: спички уровня и спичка игрока в задуманном решении.</summary>
    [MenuItem("Tools/Match & Flame/Level Lab/Print level schematics")]
    public static void Schematics()
    {
        var sb = new StringBuilder("Schematics\n");
        foreach (LevelDesigns.Level spec in LevelDesigns.All())
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = Build(spec);
            Evaluation e = Evaluate(spec, root);
            Transform player = root.GetComponentsInChildren<DraggableMatch>()[0].transform;
            player.SetPositionAndRotation(e.solutionRoot, Quaternion.Euler(0f, 0f, spec.solutionRot));
            sb.AppendLine($"L{spec.index} {spec.titleEN} — P = intended placement");
            sb.Append(Map(root, e, overlay: false));
            sb.AppendLine(Describe(e));
            Object.DestroyImmediate(root);
        }
        LevelLab.Log(sb.ToString());
    }

    public static string Describe(Evaluation e)
    {
        var s = e.search;
        return $"  L{e.spec.index} {e.spec.titleEN}: matches={e.intended.total} golden={e.intended.golden} halfWidth={e.halfWidth:0.00} zoom={e.Zoom:0.00} | intended {e.intended}" +
               (e.phaseCount > 1 ? $" (3* in {e.intendedPhaseShare:P0} of moments)" : "") +
               $" | unburnt [{string.Join(", ", e.unburnt)}] | search: valid {s.valid}, 3* {s.threeStars} ({s.SolutionShare:P2}), intended zone {e.clusterCells} cells, zones [{string.Join(",", e.clusters.Take(8))}]" +
               $" | random 30: {e.randomWins} wins ({(float)e.randomWins / Mathf.Max(1, e.randomTried):P0}), {e.wrongWins} outside the main zones" +
               $" | passive (match left at start): {e.passiveWins} 3*" +
               $" | {(e.Passed ? "PASS" : "FAIL")}";
    }

    // ---------------------------------------------------------------- Сборка

    private const string MechanicsFolder = "Assets/Prefabs/Mechanics";
    public static readonly Color GoldenColor = new Color(1f, 0.85f, 0.2f, 1f); // как MatchLogic.Start

    public static GameObject Build(LevelDesigns.Level spec)
    {
        var root = new GameObject(spec.name);
        int i = 0;
        MatchLogic player = Make(root.transform, spec.player, true, "Match_Player");
        var drag = player.GetComponent<DraggableMatch>();
        drag.freePlacementMode = spec.free;
        drag.slotLayer = LayerMask.GetMask("Slot");
        PrefabUtility.RecordPrefabInstancePropertyModifications(drag);
        foreach (LevelDesigns.Match m in spec.matches) Make(root.transform, m, false, "Match_" + (++i).ToString("00"));
        int k = 0;
        foreach (LevelDesigns.Mech m in spec.mechs) MakeMech(root.transform, m, ++k);
        if (!spec.free) MakeSlot(root.transform, spec.slotCenter, spec.slotRot);
        if (!string.IsNullOrEmpty(spec.hintEN)) MakeHintText(root.transform, spec);
        return root;
    }

    private static MatchLogic Make(Transform parent, LevelDesigns.Match m, bool player, string name)
    {
        MatchLogic logic = LevelLab.NewMatch(parent, m.center, m.rot, player, name);
        logic.isGolden = m.golden;
        logic.ignorePlayerMatch = m.immune;
        if (m.flashRange >= 0f) logic.flashRange = m.flashRange;
        if (m.flashRadius >= 0f) logic.flashRadius = m.flashRadius;
        if (m.spreadRadius >= 0f) logic.spreadRadius = m.spreadRadius;
        if (m.sideDelay >= 0f) logic.sideSpreadDelay = m.sideDelay;
        PrefabUtility.RecordPrefabInstancePropertyModifications(logic);
        if (m.immune || m.golden)
        {
            var sr = logic.GetComponent<SpriteRenderer>();
            sr.color = m.golden ? GoldenColor : ImmuneTint; // золото видно и в редакторе (в игре цвет ставит MatchLogic)
            PrefabUtility.RecordPrefabInstancePropertyModifications(sr);
        }
        if (m.oscDistance > 0f)
        {
            var osc = logic.gameObject.AddComponent<Oscillator>();
            osc.movementAxis = m.oscAxis;
            osc.distance = m.oscDistance;
            osc.speed = m.oscSpeed;
            osc.useLevelClock = true; // в одной фазе с каплями и вентиляторами
        }
        return logic;
    }

    private static void MakeMech(Transform parent, LevelDesigns.Mech m, int index)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{MechanicsFolder}/{(m.kind == LevelDesigns.Mech.Kind.Dripper ? "WaterDripper" : m.kind == LevelDesigns.Mech.Kind.Fan ? "Fan" : "WaterShelter")}.prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = $"{m.kind}_{index:00}";
        go.transform.SetPositionAndRotation(m.pos, Quaternion.Euler(0f, 0f, m.rot));
        if (m.flip) go.transform.localScale = new Vector3(-1f, 1f, 1f);
        Component target = m.kind == LevelDesigns.Mech.Kind.Dripper ? go.GetComponent<WaterDripper>()
                         : m.kind == LevelDesigns.Mech.Kind.Fan ? go.GetComponent<Fan>()
                         : (Component)go.GetComponent<WaterShelter>();
        if (m.props.Count > 0)
        {
            var so = new SerializedObject(target);
            foreach (var kv in m.props)
            {
                SerializedProperty p = so.FindProperty(kv.Key);
                if (p == null) throw new System.ArgumentException($"{target.GetType().Name} has no field '{kv.Key}'");
                switch (kv.Value)
                {
                    case float f: p.floatValue = f; break;
                    case int n: p.intValue = n; break;
                    case bool b: p.boolValue = b; break;
                    case Vector2 v: p.vector2Value = v; break;
                    case Color c: p.colorValue = c; break;
                    case System.Enum en: p.enumValueIndex = System.Convert.ToInt32(en); break;
                    default: throw new System.ArgumentException($"unsupported value for {kv.Key}");
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
        if (m.oscDistance > 0f)
        {
            var osc = go.AddComponent<Oscillator>();
            osc.movementAxis = m.oscAxis;
            osc.distance = m.oscDistance;
            osc.speed = m.oscSpeed;
            osc.useLevelClock = true;
        }
    }

    // Слот — копия слота из Level_0 (тот же вид и коллайдер), повёрнутая под нужную позу
    private static void MakeSlot(Transform parent, Vector2 center, float rot)
    {
        GameObject level0 = PrefabUtility.LoadPrefabContents(LevelsFolder + "/Level_0.prefab");
        try
        {
            Transform template = level0.GetComponentsInChildren<Transform>(true).First(t => t.name == "MatchSlot");
            GameObject slot = Object.Instantiate(template.gameObject, parent);
            slot.name = "MatchSlot";
            slot.transform.SetPositionAndRotation(center, Quaternion.Euler(0, 0, rot));
            slot.transform.localScale = template.lossyScale;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(level0);
        }
    }

    // Подсказка в мире — копия текста из Level_2 (TextMeshPro + LocalizeText + PulsingText) в рамке по центру,
    // с мягкой подложкой (WorldHintBacking) и иконкой механики внутри подложки слева от текста
    private const float HintHeight = 0.9f;

    private static void MakeHintText(Transform parent, LevelDesigns.Level spec) =>
        MakeHint(parent, spec.hintEN, spec.hintRU, spec.hintPosition, spec.hintIcon, spec.hintWidth, spec.hintFontSize);

    /// <summary>Подсказка на поле (для любого уровня): текст RU/EN в рамке, подложка, иконка механики (необязательно).</summary>
    public static GameObject MakeHint(Transform parent, string en, string ru, Vector2 position, string iconName, float width = 3.6f, float fontSize = 2.3f)
    {
        GameObject level2 = PrefabUtility.LoadPrefabContents(LevelsFolder + "/Level_2.prefab");
        try
        {
            LocalizeText template = level2.GetComponentInChildren<LocalizeText>(true);
            GameObject text = Object.Instantiate(template.gameObject, parent);
            text.name = "HintText";
            foreach (Transform child in text.transform.Cast<Transform>().ToList()) Object.DestroyImmediate(child.gameObject);
            var loc = text.GetComponent<LocalizeText>();
            loc.textEN = en;
            loc.textRU = ru;
            var tmp = text.GetComponent<TextMeshPro>();
            tmp.text = en;
            var ruFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/LiberationSans RU SDF.asset");
            if (ruFont != null) tmp.font = ruFont;
            tmp.margin = Vector4.zero;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.fontSize = fontSize;
            var rt = (RectTransform)text.transform;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, HintHeight);
            rt.position = position;
            var pulse = text.GetComponent<PulsingText>();
            if (pulse != null) pulse.minAlpha = Mathf.Max(pulse.minAlpha, 0.6f);

            var back = new GameObject("Backing");
            back.transform.SetParent(text.transform, false);
            var bsr = back.AddComponent<SpriteRenderer>();
            bsr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/ui_pill.png");
            bsr.drawMode = SpriteDrawMode.Sliced;
            bsr.sortingLayerID = tmp.sortingLayerID;
            bsr.sortingOrder = tmp.sortingOrder - 1;
            bsr.color = VisualTheme.Current.hintBackingColor;

            Transform iconT = null;
            if (!string.IsNullOrEmpty(iconName))
            {
                var icon = new GameObject("HintIcon");
                icon.transform.SetParent(text.transform, false);
                var sr = icon.AddComponent<SpriteRenderer>();
                sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Mechanics/icon_{iconName}.png");
                sr.sortingOrder = 1;
                iconT = icon.transform;
            }
            // У шаблона из Level_2 подложка уже есть — компонент переиспользуем (его ребёнок Backing удалён выше)
            var hint = text.GetComponent<WorldHintBacking>() != null ? text.GetComponent<WorldHintBacking>() : text.AddComponent<WorldHintBacking>();
            var so = new SerializedObject(hint);
            so.FindProperty("backing").objectReferenceValue = bsr;
            so.FindProperty("icon").objectReferenceValue = iconT;
            so.ApplyModifiedPropertiesWithoutUndo();
            hint.Refit();
            return text;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(level2);
        }
    }

    // ---------------------------------------------------------------- Проверка

    public class Evaluation
    {
        public LevelDesigns.Level spec;
        public LevelSimulator.Result intended;
        public float intendedPhaseShare = 1f;
        public int phaseCount = 1;
        public LevelLab.SearchStats search;
        public int clusterCells;
        /// <summary>Размеры зон решений (связные группы клеток с 3★), по убыванию.</summary>
        public List<int> clusters = new List<int>();
        public int solutionCells;
        public int randomTried, randomWins, wrongWins, passiveWins;
        public List<string> unburnt = new List<string>();
        public float halfWidth, top, bottom;
        public int missingScripts;
        public Vector2 solutionRoot;
        /// <summary>Во сколько раз камера отъедет на 20:9 (как GameManager.FitCameraToLevel, запас 0.3).</summary>
        public float Zoom => Mathf.Max(1f, (halfWidth + 0.5f) / (5f * 9f / 20f));
        public bool InFrame => Zoom <= 1.5f && top <= 4.0f && bottom >= -4.9f;
        /// <summary>Значимые зоны решений: не меньше 3 клеток; «небольшое число» — не больше трёх.</summary>
        public int MainZones => clusters.Count(c => c >= 3 && c * 10 >= solutionCells);
        public bool Passed => intended.valid && intended.stars == 3 && intended.goldenBurnt == intended.golden
                              && randomWins * 10 <= randomTried && wrongWins == 0 && MainZones <= 3 && passiveWins == 0 && missingScripts == 0 && InFrame;
    }

    private const int RandomPlacements = 30;

    public static Evaluation Evaluate(LevelDesigns.Level spec, GameObject root)
    {
        Physics2D.SyncTransforms();
        var e = new Evaluation { spec = spec };
        var visible = root.GetComponentsInChildren<Collider2D>(false).Where(c => c.GetComponent<DraggableMatch>() == null).Select(c => c.bounds)
            .Concat(root.GetComponentsInChildren<SpriteRenderer>(false).Where(r => r.GetComponentInParent<MatchLogic>() == null && r.sprite != null).Select(r => r.bounds)).ToList();
        e.halfWidth = visible.Max(b => Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x)));
        e.top = visible.Max(b => b.max.y);
        e.bottom = visible.Min(b => b.min.y);
        e.missingScripts = root.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));

        using (var sim = new LevelSimulator(root, owned: false))
        {
            float[] phases = LevelLab.PhasesFor(sim);
            e.phaseCount = phases.Length;
            float period = sim.TimingPeriod();
            Vector2 solRoot = sim.PlayerRootFor(spec.solutionCenter, spec.solutionRot);
            e.solutionRoot = solRoot;
            e.intended = sim.Run(solRoot, spec.solutionRot, spec.solutionPhase);
            foreach (MatchLogic m in root.GetComponentsInChildren<MatchLogic>(false))
                if (!sim.Burned(m)) e.unburnt.Add(m.name);

            if (phases.Length > 1)
            {
                int ok = 0;
                const int Samples = 40;
                for (int i = 0; i < Samples; i++)
                    if (sim.Run(solRoot, spec.solutionRot, period * i / Samples).stars == 3) ok++;
                e.intendedPhaseShare = (float)ok / Samples;
            }

            float step = phases.Length > 1 ? 0.2f : 0.1f;
            e.search = LevelLab.Search(sim, step, phases: phases);

            // Зона задуманного решения: клетки сетки с 3★, связанные с клеткой решения (тот же поворот)
            float start = sim.PlayerStartRotation;
            int RotIndex(float rot) => Mathf.RoundToInt(Mathf.Repeat(start - rot, 360f) / 90f) % 4;
            (int, int, int) Cell(Vector2 c, float rot) => (RotIndex(rot), Mathf.RoundToInt((c.x + 2.4f) / step), Mathf.RoundToInt((c.y + 4.6f) / step));
            var solved = new HashSet<(int, int, int)>(e.search.solutions.Select(p => Cell(p.center, p.rotation)));
            var cluster = new HashSet<(int, int, int)>();
            var seed = Cell(spec.solutionCenter, spec.solutionRot);
            if (!solved.Contains(seed))
                foreach (var c in solved) if (c.Item1 == seed.Item1 && Mathf.Abs(c.Item2 - seed.Item2) <= 2 && Mathf.Abs(c.Item3 - seed.Item3) <= 2) { seed = c; break; }
            var queue = new Queue<(int, int, int)>();
            if (solved.Contains(seed)) { cluster.Add(seed); queue.Enqueue(seed); }
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var n = (c.Item1, c.Item2 + dx, c.Item3 + dy);
                        if (solved.Contains(n) && cluster.Add(n)) queue.Enqueue(n);
                    }
            }
            e.clusterCells = cluster.Count;
            // Зоны решений по положению (повороты объединены): связные группы клеток, где хоть один поворот даёт 3★.
            // «Главные» — зона задуманного решения и до двух самых больших других.
            var cellsXY = new HashSet<(int, int)>(solved.Select(c => (c.Item2, c.Item3)));
            var zones = new List<HashSet<(int, int)>>();
            var seen = new HashSet<(int, int)>();
            foreach (var c0 in cellsXY)
            {
                if (!seen.Add(c0)) continue;
                var zone = new HashSet<(int, int)> { c0 };
                var q = new Queue<(int, int)>(); q.Enqueue(c0);
                while (q.Count > 0)
                {
                    var c = q.Dequeue();
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            var n = (c.Item1 + dx, c.Item2 + dy);
                            if (cellsXY.Contains(n) && seen.Add(n)) { zone.Add(n); q.Enqueue(n); }
                        }
                }
                zones.Add(zone);
            }
            zones = zones.OrderByDescending(z => z.Count).ToList();
            e.clusters = zones.Select(z => z.Count).ToList();
            e.solutionCells = cellsXY.Count;
            var intendedXY = new HashSet<(int, int)>(cluster.Select(c => (c.Item2, c.Item3)));
            var mainXY = new HashSet<(int, int)>(intendedXY);
            foreach (var z in zones.Where(z => !z.Overlaps(intendedXY)).Take(2)) mainXY.UnionWith(z);
            foreach (var z in zones.Where(z => z.Overlaps(intendedXY))) mainXY.UnionWith(z);

            // 30 случайных расстановок в кадре со случайным моментом
            var rng = new System.Random(spec.index * 7919);
            int guard = 0;
            while (e.randomTried < RandomPlacements && guard++ < 20000)
            {
                var c = new Vector2((float)(rng.NextDouble() * 4.4 - 2.2), (float)(rng.NextDouble() * 7.0 - 4.4));
                float rot = Mathf.Repeat(start - 90f * rng.Next(4), 360f);
                float ph = period > 0f ? (float)(rng.NextDouble() * period) : 0f;
                LevelSimulator.Result r = sim.Run(sim.PlayerRootFor(c, rot), rot, ph);
                // Уровень со слотом: бросок мимо слота в игре возвращает спичку — это попытка без победы
                if (!r.valid) { if (!sim.FreePlacement) e.randomTried++; continue; }
                e.randomTried++;
                if (r.stars < 3) continue;
                e.randomWins++;
                var cell = Cell(new Vector2(Mathf.Round((c.x + 2.4f) / step) * step - 2.4f, Mathf.Round((c.y + 4.6f) / step) * step - 4.6f), rot);
                bool near = false;
                // ±2 клетки: зоны строятся по фазам сетки, а случайный момент — любой, край зоны размыт
                for (int dx = -2; dx <= 2 && !near; dx++)
                    for (int dy = -2; dy <= 2 && !near; dy++)
                        near = mainXY.Contains((cell.Item2 + dx, cell.Item3 + dy));
                if (!near && sim.FreePlacement) e.wrongWins++; // со слотом место одно — «чужих зон» нет
            }

            // Без вмешательства: спичка игрока поджигается там, где лежит в начале уровня
            foreach (float ph in phases)
                if (sim.Run(sim.PlayerStartRoot, start, ph).stars == 3) e.passiveWins++;
        }
        return e;
    }
}
