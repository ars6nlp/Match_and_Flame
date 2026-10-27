using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Проверка расстановки уровней 17–21 по правилам LEVELS_LAYOUT_FIX.md:
/// зазоры между спичками по ВИДИМОМУ контуру (капсула палочки, не коллайдер), связи огня (кто кого поджигает в задуманном
/// решении и в одиночном поджоге), сетка и повороты, кран/вентилятор/зонтик (перекрытия, путь капли, зона ветра и запас
/// по порогу «задувает»), подсказка, поля до краёв кадра и HUD на 16:9 и 20:9, окно по времени для задуманного решения,
/// золото вне 3★-решений.
/// Источник: LAYOUT_SRC=prefab (сохранённые префабы и LevelSolutions) или design (LevelDesigns, по умолчанию).
/// LEVELS=17,18 — ограничить набор. Отчёт: Builds/layout-check.md и лог.
/// </summary>
public static class LayoutCheck
{
    // Видимая палочка в локальных координатах спички при rot = 0 (замер по альфе спрайта): от −0.72 до 0.83 по оси,
    // ось палочки на y = −0.04, толщина 0.19 (головка 0.28). Модель — капсула с радиусом StickR.
    public const float StickFrom = -0.61f, StickTo = 0.72f, StickY = -0.04f, StickR = 0.11f;
    /// <summary>Видимый центр палочки относительно центра коллайдера (rot = 0).</summary>
    public static readonly Vector2 VisibleCenter = new Vector2((StickFrom + StickTo) / 2f, StickY);

    public const float MinGapUnlinked = 0.7f;   // 2 × spreadRadius (0.3) + 0.1
    public const float MinMargin = 0.5f;        // до краёв кадра и HUD
    public const float HudCanvas = 160f;        // низ верхней панели HUD под безопасной зоной (отступ 20 + 140)
    public const float Grid = 0.125f;     // шаг сетки (центр видимой палочки, сопла)

    public struct Stick
    {
        public Vector2 a, b;
        public string name;
        public MatchLogic logic;
    }

    public static Stick StickOf(MatchLogic m)
    {
        Collider2D col = m.GetComponent<Collider2D>();
        Vector2 c = m.transform.TransformPoint(((BoxCollider2D)col).offset);
        float rot = m.transform.eulerAngles.z;
        Vector2 d = LevelDesigns.Dir(rot), s = LevelDesigns.Side(rot);
        return new Stick { a = c + d * StickFrom + s * StickY, b = c + d * StickTo + s * StickY, name = m.name, logic = m };
    }

    public static float SegDist(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        if (Intersect(a1, a2, b1, b2)) return 0f;
        return Mathf.Min(Mathf.Min(PointSeg(a1, b1, b2), PointSeg(a2, b1, b2)), Mathf.Min(PointSeg(b1, a1, a2), PointSeg(b2, a1, a2)));
    }

    public static float Gap(Stick x, Stick y) => SegDist(x.a, x.b, y.a, y.b) - 2f * StickR;

    private static float PointSeg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude < 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + t * ab);
    }

    private static bool Intersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float Cross(Vector2 o, Vector2 p, Vector2 q) => (p.x - o.x) * (q.y - o.y) - (p.y - o.y) * (q.x - o.x);
        float d1 = Cross(c, d, a), d2 = Cross(c, d, b), d3 = Cross(a, b, c), d4 = Cross(a, b, d);
        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    /// <summary>Мировой прямоугольник непрозрачной части спрайта (альфа &gt; 0.5).</summary>
    public static Bounds VisibleBounds(SpriteRenderer sr)
    {
        Rect px = AlphaRect(sr.sprite);
        Vector2 piv = sr.sprite.pivot;
        float ppu = sr.sprite.pixelsPerUnit;
        var b = new Bounds();
        bool first = true;
        foreach (Vector2 corner in new[] { new Vector2(px.xMin, px.yMin), new Vector2(px.xMax, px.yMin), new Vector2(px.xMin, px.yMax), new Vector2(px.xMax, px.yMax) })
        {
            Vector3 local = new Vector3((corner.x - sr.sprite.rect.xMin - piv.x) / ppu, (corner.y - sr.sprite.rect.yMin - piv.y) / ppu, 0f);
            if (sr.flipX) local.x = -local.x;
            Vector3 w = sr.transform.TransformPoint(local);
            if (first) { b = new Bounds(w, Vector3.zero); first = false; } else b.Encapsulate(w);
        }
        return b;
    }

    private static readonly Dictionary<Sprite, Rect> alphaCache = new Dictionary<Sprite, Rect>();

    private static Rect AlphaRect(Sprite sprite)
    {
        if (alphaCache.TryGetValue(sprite, out Rect cached)) return cached;
        var tex = new Texture2D(2, 2);
        tex.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.texture)));
        Rect r = sprite.rect;
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
        for (int y = (int)r.yMin; y < (int)r.yMax; y++)
            for (int x = (int)r.xMin; x < (int)r.xMax; x++)
                if (tex.GetPixel(x, y).a > 0.5f) { x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
        Object.DestroyImmediate(tex);
        Rect res = x1 < 0 ? r : Rect.MinMaxRect(x0, y0, x1 + 1, y1 + 1);
        alphaCache[sprite] = res;
        return res;
    }

    private static float BoxCapsuleGap(Bounds box, Stick s)
    {
        // Расстояние от прямоугольника до отрезка палочки минус её радиус (приближённо — по 9 точкам отрезка)
        float best = float.MaxValue;
        for (int i = 0; i <= 8; i++)
        {
            Vector2 p = Vector2.Lerp(s.a, s.b, i / 8f);
            Vector2 q = new Vector2(Mathf.Clamp(p.x, box.min.x, box.max.x), Mathf.Clamp(p.y, box.min.y, box.max.y));
            best = Mathf.Min(best, Vector2.Distance(p, q));
        }
        return best - StickR;
    }

    private static float BoxGap(Bounds a, Bounds b)
    {
        float dx = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
        float dy = Mathf.Max(0f, Mathf.Max(a.min.y - b.max.y, b.min.y - a.max.y));
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private static bool OnGrid(float v) => Mathf.Abs(v / Grid - Mathf.Round(v / Grid)) < 0.02f;

    // ---------------------------------------------------------------- Отчёт

    [MenuItem("Tools/Match & Flame/Level Lab/Layout check (17-21)")]
    public static void Report()
    {
        string src = System.Environment.GetEnvironmentVariable("LAYOUT_SRC") ?? "design";
        string filter = System.Environment.GetEnvironmentVariable("LEVELS");
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Resources/LevelCatalog.asset");
        var solutions = Resources.Load<LevelSolutions>("LevelSolutions");
        var sb = new StringBuilder($"# Проверка расстановки ({src})\n");
        int total = 0;
        // LAYOUT_SRC=all — все уровни каталога по префабам: таблица LevelDesigns там, где она есть, иначе решение из LevelSolutions
        // (для уровней со слотом — поза слота), момент 0
        var specs = LevelDesigns.All();
        if (src == "all")
            for (int i = 0; i < catalog.levels.Length; i++)
                if (!specs.Any(sp => sp.index == i))
                    specs.Add(new LevelDesigns.Level { index = i, name = catalog.levels[i].name, titleRU = catalog.levels[i].name });
        specs = specs.OrderBy(sp => sp.index).ToList();
        foreach (LevelDesigns.Level spec in specs)
        {
            if (!string.IsNullOrEmpty(filter) && !filter.Split(',').Contains(spec.index.ToString())) continue;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root;
            Vector2 solRoot;
            if (src == "prefab" || src == "all")
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(catalog.levels[spec.index]);
                root.transform.position = Vector3.zero;
                Physics2D.SyncTransforms();
                if (solutions.TryGet(spec.index, out LevelSolutions.Solution sol)) { solRoot = sol.position; spec.solutionRot = sol.rotationZ; }
                else
                {
                    // Слот: корень спички игрока так, чтобы её центр совпал со слотом
                    Collider2D slotCol = root.GetComponentsInChildren<Collider2D>().FirstOrDefault(c => c.gameObject.layer == LayerMask.NameToLayer("Slot"));
                    DraggableMatch pd = root.GetComponentInChildren<DraggableMatch>();
                    var pc = (BoxCollider2D)pd.GetComponent<Collider2D>();
                    if (slotCol != null)
                    {
                        Transform slot = slotCol.transform;
                        spec.solutionRot = slot.eulerAngles.z;
                        solRoot = (Vector2)slot.position - (Vector2)(Quaternion.Euler(0, 0, spec.solutionRot) * Vector3.Scale(pc.offset, pd.transform.lossyScale));
                    }
                    else
                    {
                        // Решения в LevelSolutions нет (уровни с перетаскиванием спичек): берём старт — проверка решения не показательна
                        spec.solutionRot = pd.transform.eulerAngles.z;
                        solRoot = pd.transform.position;
                        spec.solutionText = "нет";
                    }
                }
            }
            else
            {
                root = LevelBuilder.Build(spec);
                solRoot = Vector2.zero;
            }
            root.transform.position = Vector3.zero;
            Physics2D.SyncTransforms();
            var issues = new List<string>();
            sb.AppendLine($"\n## L{spec.index} «{spec.titleRU}» ({spec.name}, экран «Уровень {spec.index + 1}»)\n");
            Check(spec, root, src == "design" ? (Vector2?)null : solRoot, sb, issues);
            sb.AppendLine(issues.Count == 0 ? "\n**Нарушений нет.**" : $"\n**Нарушений: {issues.Count}**");
            foreach (string s in issues) sb.AppendLine("- " + s);
            total += issues.Count;
            Object.DestroyImmediate(root);
        }
        Directory.CreateDirectory("Builds");
        File.WriteAllText($"Builds/layout-check-{src}.md", sb.ToString());
        LevelLab.Log($"LayoutCheck {src}: {total} issues\n" + sb);
    }

    public static void Check(LevelDesigns.Level spec, GameObject root, Vector2? solRootOverride, StringBuilder sb, List<string> issues)
    {
        MatchLogic[] all = root.GetComponentsInChildren<MatchLogic>(false);
        MatchLogic player = all.First(m => m.GetComponent<DraggableMatch>() != null);
        List<MatchLogic> level = all.Where(m => m != player).ToList();
        string N(MatchLogic m) => m.name.Replace("Match_", "#") + (m.isGolden ? "(G)" : "");

        // Позы в префабе: симулятор двигает маятники, замеры (сетка, зазоры, механики) — по исходным позам
        var startPose = root.GetComponentsInChildren<Transform>().ToDictionary(t => t, t => (t.position, t.rotation));
        void RestorePoses()
        {
            foreach (var kv in startPose)
                if (kv.Key != player.transform && !kv.Key.IsChildOf(player.transform)) kv.Key.SetPositionAndRotation(kv.Value.position, kv.Value.rotation);
            Physics2D.SyncTransforms();
        }

        using (var sim = new LevelSimulator(root, owned: false))
        {
            Vector2 solRoot = solRootOverride ?? sim.PlayerRootFor(spec.solutionCenter, spec.solutionRot);
            LevelSimulator.Result r = sim.Run(solRoot, spec.solutionRot, spec.solutionPhase);
            // Граф задуманного решения: кто кого поджёг
            var intended = new HashSet<(MatchLogic, MatchLogic)>();
            var litBy = new Dictionary<MatchLogic, string>();
            foreach (MatchLogic m in all)
            {
                string by = sim.LitBy(m);
                litBy[m] = by;
                if (by.Length > 1)
                {
                    MatchLogic from = all.First(x => x.name == by.Substring(1));
                    intended.Add((from, m));
                }
            }
            sb.AppendLine($"Задуманное решение: {r}; спичек {level.Count}, золотых {level.Count(m => m.isGolden)}.");
            sb.AppendLine("Цепочка (кто кого поджёг, ~ — боковое пламя, > — вспышка с конца): " +
                          string.Join(", ", all.Where(m => litBy[m].Length > 1).Select(m => $"{N(m)}{litBy[m][0]}{litBy[m].Substring(1).Replace("Match_", "#")}")));
            if (r.stars < 3) issues.Add($"задуманное решение даёт {r.stars}★");

            // Одиночные поджоги: все прямые связи
            var possible = new HashSet<(MatchLogic, MatchLogic)>();
            foreach (MatchLogic a in level)
            {
                sim.RunFrom(a, spec.solutionPhase);
                foreach (MatchLogic b in level)
                    if (b != a && sim.LitBy(b).Length > 1 && sim.LitBy(b).Substring(1) == a.name) possible.Add((a, b));
            }

            // Зазоры по видимому контуру
            var sticks = all.ToDictionary(m => m, StickOf);
            sim.Run(solRoot, spec.solutionRot, spec.solutionPhase); // вернуть спичку игрока в решение для замера
            RestorePoses();
            foreach (MatchLogic m in level) sticks[m] = StickOf(m);
            sticks[player] = StickOf(player);
            sb.AppendLine("\n| Пара | Видимый зазор | Связь в решении | Возможная связь |\n|---|---|---|---|");
            var linkGaps = new List<float>();
            for (int i = 0; i < all.Length; i++)
                for (int j = i + 1; j < all.Length; j++)
                {
                    MatchLogic a = all[i], b = all[j];
                    float g = Gap(sticks[a], sticks[b]);
                    if (g >= MinGapUnlinked) continue;
                    bool link = intended.Contains((a, b)) || intended.Contains((b, a));
                    bool can = possible.Contains((a, b)) || possible.Contains((b, a));
                    sb.AppendLine($"| {N(a)} – {N(b)} | {g:0.00} | {(link ? "да" : "—")} | {(can ? "да" : "—")} |");
                    if (a == player || b == player) continue; // спичку игрока ставит игрок — справочно
                    if (link || can) linkGaps.Add(g); // замыкающая связь цикла (рамка) — тоже связь
                    else issues.Add($"{N(a)} и {N(b)}: зазор {g:0.00} < {MinGapUnlinked} без связи в решении" + (can ? " (и огонь между ними может перескочить)" : ""));
                }
            foreach (var l in intended)
            {
                float g = Gap(sticks[l.Item1], sticks[l.Item2]);
                if (g >= MinGapUnlinked) sb.AppendLine($"| {N(l.Item1)} → {N(l.Item2)} | {g:0.00} | да (дальняя) | |");
            }
            if (linkGaps.Count > 0)
                sb.AppendLine($"\nЗазоры связей решения: {string.Join(", ", linkGaps.Select(g => g.ToString("0.00")))} (разброс {linkGaps.Max() - linkGaps.Min():0.00}).");
            foreach (var p in possible.Where(p => !intended.Contains(p) && !intended.Contains((p.Item2, p.Item1)) && p.Item1 != player && p.Item2 != player))
                sb.AppendLine($"- возможна связь вне решения: {N(p.Item1)} поджигает {N(p.Item2)} (зазор {Gap(sticks[p.Item1], sticks[p.Item2]):0.00})");

            // Сетка и повороты (центр видимой палочки)
            foreach (MatchLogic m in level)
            {
                Stick s = sticks[m];
                Vector2 c = (s.a + s.b) / 2f;
                float rot = Mathf.Repeat(m.transform.eulerAngles.z, 360f);
                bool rotOk = Mathf.Abs(Mathf.DeltaAngle(rot, Mathf.Round(rot / 90f) * 90f)) < 0.5f;
                if (!OnGrid(c.x) || !OnGrid(c.y) || !rotOk)
                    issues.Add($"{N(m)}: вне сетки {Grid} — центр палочки ({c.x:0.00}, {c.y:0.00}), поворот {rot:0}°");
            }

            // Роль: спички, которые никого не поджигают в решении и не золотые — «листья»
            var leaves = level.Where(m => !intended.Any(l => l.Item1 == m) && !m.isGolden).Select(N).ToList();
            sb.AppendLine($"\nЛистья цепочки (никого не поджигают, не золото): {(leaves.Count == 0 ? "нет" : string.Join(", ", leaves))}.");

            // Механики (после прогонов симулятора коллайдеры сгоревших выключены — вернуть)
            foreach (MatchLogic m in all) m.GetComponent<Collider2D>().enabled = true;
            Physics2D.SyncTransforms();
            var mechBoxes = new List<(string name, Bounds b)>();
            foreach (WaterDripper d in root.GetComponentsInChildren<WaterDripper>())
            {
                Bounds vb = VisibleBounds(d.GetComponent<SpriteRenderer>());
                mechBoxes.Add((d.name, vb));
                Vector2 n = d.NozzleWorld;
                // Траектория капли: какие спички лежат под соплом (по коллайдеру, радиус капли), сверху вниз
                var under = new List<string>();
                var hits = new List<RaycastHit2D>();
                Physics2D.CircleCast(n + Vector2.down * 0.05f, d.DropRadius, Vector2.down, new ContactFilter2D { useTriggers = true }, hits, 12f);
                foreach (RaycastHit2D h in hits.OrderBy(h => h.distance))
                {
                    if (h.collider.GetComponent<WaterShelter>() != null) { under.Add("зонтик " + h.collider.name); continue; }
                    MatchLogic m = h.collider.GetComponent<MatchLogic>();
                    if (m != null && m != player) under.Add(N(m));
                }
                sb.AppendLine($"\nКран {d.name}: сопло ({n.x:0.00}, {n.y:0.00}), каждые {d.Interval:0.0} с, первая в {d.StartDelay:0.0} с, мокрая {d.WetDuration:0.0} с; под соплом сверху вниз: {(under.Count == 0 ? "ничего" : string.Join(" → ", under))}.");
                if (!OnGrid(n.x)) issues.Add($"{d.name}: сопло вне сетки (x {n.x:0.00})");
            }
            foreach (WaterShelter u in root.GetComponentsInChildren<WaterShelter>())
            {
                SpriteRenderer sr = u.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) mechBoxes.Add((u.name, VisibleBounds(sr)));
                sb.AppendLine($"Зонтик {u.name}: ({u.transform.position.x:0.00}, {u.transform.position.y:0.00})" + (u.GetComponent<Oscillator>() != null ? $", качается ±{u.GetComponent<Oscillator>().distance:0.00}" : ""));
            }
            foreach (Fan f in root.GetComponentsInChildren<Fan>())
            {
                SpriteRenderer sr = f.GetComponent<SpriteRenderer>();
                mechBoxes.Add((f.name, VisibleBounds(sr)));
                float e = f.ExtinguishStrength;
                sb.AppendLine($"\nВентилятор {f.name}: сопло ({f.Nozzle.x:0.00}, {f.Nozzle.y:0.00}), дует {f.Direction}, сила {f.Strength:0.0}, зона {f.ZoneLength:0.0}, порог «задувает» {e:0.0} (до {f.ExtinguishReach():0.00} от сопла).");
                foreach (MatchLogic m in all)
                {
                    float smax = 0f;
                    for (int i = 0; i <= 10; i++)
                        smax = Mathf.Max(smax, f.StrengthAt(Vector2.Lerp(m.headPoint.position, m.tailPoint.position, i / 10f)));
                    if (smax <= 0f) continue;
                    float ratio = smax / e;
                    string who = m == player ? "игрок (в решении)" : N(m);
                    sb.AppendLine($"  - {who}: сила до {smax:0.0} ({ratio:P0} порога)");
                    if (ratio > 0.8f && ratio < 1.2f) issues.Add($"{f.name}: {who} на грани порога — {ratio:P0} (нужно ≤ 80% или ≥ 120%)");
                }
            }
            foreach (var mb in mechBoxes)
            {
                foreach (MatchLogic m in all)
                {
                    float g = BoxCapsuleGap(mb.b, sticks[m]);
                    if (g < 0.15f && !(mb.name.StartsWith("Shelter") && g >= 0f))
                        issues.Add($"{mb.name} вплотную к {(m == player ? "спичке игрока" : N(m))} (зазор {g:0.00})");
                    if (m.isGolden && g < 0.3f && !mb.name.StartsWith("Shelter")) issues.Add($"золотая {N(m)} вплотную к {mb.name} ({g:0.00})");
                }
                foreach (var other in mechBoxes)
                    if (string.CompareOrdinal(other.name, mb.name) > 0 && BoxGap(mb.b, other.b) < 0.1f && !(mb.name.StartsWith("Shelter") || other.name.StartsWith("Shelter")))
                        issues.Add($"{mb.name} и {other.name} перекрываются");
            }

            // Подсказка (в поля уровня не входит: ей достаточно быть в кадре с запасом 0.15)
            var hintBoxes = new List<Bounds>();
            foreach (WorldHintBacking h in root.GetComponentsInChildren<WorldHintBacking>())
            {
                h.Refit();
                Bounds hb = h.WorldRect;
                hintBoxes.Add(hb);
                foreach (var mb in mechBoxes)
                    if (BoxGap(hb, mb.b) < 0.1f) issues.Add($"подсказка вплотную к {mb.name}");
                foreach (MatchLogic m in all)
                    if (BoxCapsuleGap(hb, sticks[m]) < 0.1f) issues.Add($"подсказка лежит на {(m == player ? "спичке игрока" : N(m))}");
            }

            // Поля до краёв кадра и HUD (камера как в игре, вырез 100 px и без него)
            var boxes = all.Select(m => CapsuleBounds(sticks[m])).Concat(mechBoxes.Select(x => x.b)).ToList();
            var pStart = StickOfStart(player, sim);
            boxes.Add(CapsuleBounds(pStart));
            Bounds content = boxes[0];
            foreach (Bounds b in boxes) content.Encapsulate(b);
            sb.AppendLine($"\nВидимое содержимое: x {content.min.x:0.00}…{content.max.x:0.00}, y {content.min.y:0.00}…{content.max.y:0.00}.");
            sb.AppendLine("\n| Экран | Камера | Слева | Справа | До HUD | Снизу |\n|---|---|---|---|---|---|");
            VisualTheme theme = AssetDatabase.LoadAssetAtPath<VisualTheme>("Assets/Resources/VisualTheme.asset");
            CameraFit.LevelContent(root, out Bounds fitContent);
            if (System.Environment.GetEnvironmentVariable("DIAG") == "1") { sb.AppendLine($"  camera content {fitContent.min} .. {fitContent.max}"); foreach (Collider2D c in root.GetComponentsInChildren<Collider2D>()) if (c.enabled && (c.bounds.min.x < -2f || c.bounds.max.x > 2f)) sb.AppendLine($"  wide collider {c.name} {c.bounds.min}..{c.bounds.max}"); foreach (WorldHintBacking h in root.GetComponentsInChildren<WorldHintBacking>()) sb.AppendLine($"  hint rect {h.WorldRect.min}..{h.WorldRect.max}"); }
            foreach (var (w, h, notch, label) in new[] { (1080, 1920, 0f, "16:9"), (1080, 1920, 100f, "16:9 + вырез"), (1080, 2400, 0f, "20:9"), (1080, 2400, 100f, "20:9 + вырез") })
            {
                CameraFit.Reserves(theme, w, h, notch, 60f, 2.625f, out float top, out float bottom);
                float size = CameraFit.Size(fitContent, Vector2.zero, w / (float)h, 5f, 0.5f, theme.levelVerticalMargin, top, bottom, 1.5f);
                float halfW = size * w / h, ppu = h / (2f * size);
                float hud = size - (notch + HudCanvas * Mathf.Min(w / 1080f, h / 1920f)) / ppu;
                float l = content.min.x + halfW, rr = halfW - content.max.x, t = hud - content.max.y, bt = content.min.y + size;
                sb.AppendLine($"| {label} | {size / 5f:0.00}× | {l:0.00} | {rr:0.00} | {t:0.00} | {bt:0.00} |");
                if (size / 5f > 1.5f + 1e-3f) issues.Add($"{label}: камера отъезжает больше 1.5×");
                if (Mathf.Min(l, rr) < MinMargin) issues.Add($"{label}: до бокового края {Mathf.Min(l, rr):0.00} < {MinMargin}");
                if (t < MinMargin) issues.Add($"{label}: до HUD {t:0.00} < {MinMargin}");
                if (bt < MinMargin) issues.Add($"{label}: до нижнего края {bt:0.00} < {MinMargin}");
                foreach (Bounds hb in hintBoxes)
                    if (hb.min.x < -halfW + 0.15f || hb.max.x > halfW - 0.15f || hb.max.y > hud - 0.15f || hb.min.y < -size + 0.15f)
                        issues.Add($"{label}: подсказка у края кадра или под HUD");
            }

            // Окно по времени для задуманного решения
            float period = sim.TimingPeriod();
            if (period > 0f)
            {
                const float dt = 0.05f;
                int n = Mathf.RoundToInt(period / dt);
                var ok = new bool[n];
                for (int i = 0; i < n; i++) ok[i] = sim.Run(solRoot, spec.solutionRot, i * dt).stars == 3;
                int best = 0, run = 0;
                for (int i = 0; i < 2 * n; i++) { run = ok[i % n] ? run + 1 : 0; best = Mathf.Max(best, Mathf.Min(run, n)); }
                float window = best * dt;
                var line = new StringBuilder();
                for (int i = 0; i < n; i += 2) line.Append(ok[i] ? '#' : '.');
                sb.AppendLine($"\nМомент поджога (период {period:0.0} с, шаг 0.1 с): {line}  — окно 3★ {window:0.00} с ({ok.Count(x => x) * dt / period:P0} цикла).");
                if (window < 1.5f - 1e-3f) issues.Add($"окно для задуманного решения {window:0.00} с < 1.5 с");
            }

            // Золото только в 3★-решениях
            if (level.Any(m => m.isGolden))
            {
                // Утечки: золото сгорело, а 3★ нет. «Своя спичка» — не сгорела только спичка игрока (её потушила капля/ветер:
                // игрок положил её под струю) — ошибка игрока, а не обход решения; считается отдельно.
                int goldNot3 = 0, goldTotal = 0, ownOnly = 0;
                var leakSpots = new HashSet<string>();
                float[] phases = LevelLab.PhasesFor(sim);
                float step = 0.2f;
                for (float x = -2.4f; x <= 2.4f; x += step)
                    for (float y = -4.4f; y <= 3.6f; y += step)
                        for (int k = 0; k < 4; k++)
                            foreach (float ph in phases)
                            {
                                float rot = Mathf.Repeat(sim.PlayerStartRotation - 90f * k, 360f);
                                LevelSimulator.Result q = sim.Run(sim.PlayerRootFor(new Vector2(x, y), rot), rot, ph);
                                if (!q.valid || q.goldenBurnt == 0) continue;
                                goldTotal++;
                                if (q.stars >= 3) continue;
                                if (all.All(m => sim.Burned(m) || m == player)) { ownOnly++; continue; }
                                goldNot3++;
                                leakSpots.Add($"({x:0.0},{y:0.0},{rot:0})");
                                if (goldNot3 <= 12 && System.Environment.GetEnvironmentVariable("DIAG") == "1")
                                    sb.AppendLine($"  - золото без 3★: ({x:0.0}, {y:0.0}) поворот {rot:0} момент {ph:0.0} → {q}; не сгорели: " +
                                        string.Join(" ", all.Where(m => !sim.Burned(m)).Select(m => N(m) + (sim.Extinguished(m) ? "(потушена)" : ""))));
                            }
                float share = goldTotal > 0 ? (float)goldNot3 / goldTotal : 0f;
                sb.AppendLine($"\nЗолото сгорает в {goldTotal} расстановках; без 3★ из-за несгоревших спичек уровня: {goldNot3} ({share:P0}), " +
                              $"только своя спичка потушена: {ownOnly} ({(goldTotal > 0 ? (float)ownOnly / goldTotal : 0f):P0}).");
                if (System.Environment.GetEnvironmentVariable("DIAG") == "1") sb.AppendLine("  позиции: " + string.Join(" ", leakSpots));
                if (share > 0.1f) issues.Add($"золото без 3★ в {share:P0} расстановок, где оно сгорает (нужно ≤ 10%)");
            }
        }
    }

    private static Stick StickOfStart(MatchLogic player, LevelSimulator sim)
    {
        Transform t = player.transform;
        Vector3 p = t.position; Quaternion q = t.rotation;
        t.SetPositionAndRotation(sim.PlayerStartRoot, Quaternion.Euler(0f, 0f, sim.PlayerStartRotation));
        Stick s = StickOf(player);
        t.SetPositionAndRotation(p, q);
        return s;
    }

    public static Bounds CapsuleBounds(Stick s)
    {
        var b = new Bounds(s.a, Vector3.zero);
        b.Encapsulate(s.b);
        b.Expand(2f * StickR);
        return b;
    }
}
