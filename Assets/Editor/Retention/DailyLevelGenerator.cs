using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Офлайн-генератор пула уровней дня. Кандидаты собираются из проверенных модулей по таблице связей (Builds/link-table.txt):
/// дерево из одно­сторонних связей от входа, золото — иммунное и сразу за «воротами» (вход / кран / труба с ветром), поэтому
/// «золото без 3★» невозможно. Каждый кандидат проходит те же проверки, что уровни 17–31: LayoutCheck.Check (зазоры, сетка,
/// поля, камера, окно ≥ 1.5 с, золото) и LevelBuilder.Evaluate (3★ задуманного решения, ≤ 10% случайных побед, без
/// пассивной победы, ≤ 3 зон решений). Прошедшие сохраняются в Assets/Resources/DailyLevels/Daily_NNN.prefab и дописываются
/// в конец DailyLevelPool.asset (старые индексы не меняются).
///
/// Запуск: Tools → Match &amp; Flame → Retention → Build daily pool. Переменные окружения: DAILY_TIER=0..3 (иначе все),
/// DAILY_COUNT — сколько уровней каждого уровня сложности должно быть в пуле (по умолчанию 18/18/16/8), DAILY_SEED, DAILY_TRIES.
/// </summary>
public static class DailyLevelGenerator
{
    public const string PoolPath = "Assets/Resources/DailyLevelPool.asset";
    public const string Folder = "Assets/Resources/DailyLevels";
    private const float SEG = 0.665f, R = 0.11f, XMAX = 2.25f, YMIN = -2.55f, YMAX = 2.35f;
    private static readonly Rect Buttons = Rect.MinMaxRect(1.25f, 1.45f, 3f, 3f);
    private static readonly int[] DefaultCounts = { 18, 18, 16, 8 };

    public struct St
    {
        public float x, y; public int r; public bool gold, immune;
        public St(float x, float y, int r) { this.x = x; this.y = y; this.r = ((r % 360) + 360) % 360; gold = immune = false; }
        public Vector2 C => new Vector2(x, y);
    }

    // ------------------------------------------------------------------ таблица связей (как Tools/levels/sketch.py)

    private static Dictionary<(int, int, int, int), (bool ab, bool ba, bool un)> table;
    private static Dictionary<(int, int), List<Vector2>> offsets;

    private static void LoadTable()
    {
        if (table != null) return;
        const string path = "Builds/link-table.txt";
        if (!File.Exists(path)) LinkTable.Run();
        table = new Dictionary<(int, int, int, int), (bool, bool, bool)>();
        var head = new Regex(@"A rot (\d+), B rot (\d+):");
        var row = new Regex(@"^\s+(-?[\d,]+),\s*(-?[\d,]+) gap ([\d,-]+) (A>B|   ) (B>A|   ) ?(!?)");
        (int, int) sec = (-1, -1);
        foreach (string line in File.ReadAllLines(path))
        {
            Match m = head.Match(line);
            if (m.Success) { sec = (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)); continue; }
            m = row.Match(line);
            if (!m.Success || sec.Item1 < 0) continue;
            int dx = Mathf.RoundToInt(F(m.Groups[1].Value) * 8f), dy = Mathf.RoundToInt(F(m.Groups[2].Value) * 8f);
            table[(sec.Item1, sec.Item2, dx, dy)] = (m.Groups[4].Value == "A>B", m.Groups[5].Value == "B>A", m.Groups[6].Value == "!");
        }
        offsets = new Dictionary<(int, int), List<Vector2>>();
        foreach (int ra in new[] { 0, 90, 180, 270 })
            foreach (int rb in new[] { 0, 90, 180, 270 })
            {
                var list = new List<Vector2>();
                int bas = ra % 180; bool flip = ra >= 180;
                foreach (var kv in table)
                {
                    var (a, b, dx, dy) = kv.Key;
                    var (ab, ba, un) = kv.Value;
                    if (a != bas || un || !ab || ba) continue;
                    int rb2 = flip ? (b + 180) % 360 : b;
                    if (rb2 != rb) continue;
                    Vector2 d = flip ? new Vector2(-dx, -dy) / 8f : new Vector2(dx, dy) / 8f;
                    float g = Gap(new St(0, 0, ra), new St(d.x, d.y, rb));
                    if (g >= 0.2f && g <= 0.32f) list.Add(d);
                }
                offsets[(ra, rb)] = list;
            }
    }

    private static float F(string s) => float.Parse(s.Replace(',', '.'), CultureInfo.InvariantCulture);

    public static (bool ab, bool ba, bool un) Rel(St A, St B)
    {
        int ra = A.r, rb = B.r;
        Vector2 d = B.C - A.C;
        if (ra >= 180) { d = -d; ra -= 180; rb = (rb + 180) % 360; }
        var key = (ra, rb, Mathf.RoundToInt(d.x * 8f), Mathf.RoundToInt(d.y * 8f));
        return table.TryGetValue(key, out var v) ? v : (false, false, false);
    }

    private static void Seg(St s, out Vector2 a, out Vector2 b)
    {
        Vector2 d = new Vector2(Mathf.Cos(s.r * Mathf.Deg2Rad), Mathf.Sin(s.r * Mathf.Deg2Rad));
        d = new Vector2(Mathf.Round(d.x), Mathf.Round(d.y));
        a = s.C - SEG * d; b = s.C + SEG * d;
    }

    public static float Gap(St A, St B)
    {
        Seg(A, out Vector2 a1, out Vector2 a2); Seg(B, out Vector2 b1, out Vector2 b2);
        if (Cross(a1, a2, b1, b2)) return -1f;
        return Mathf.Min(Mathf.Min(PS(a1, b1, b2), PS(a2, b1, b2)), Mathf.Min(PS(b1, a1, a2), PS(b2, a1, a2))) - 2f * R;
    }

    private static float PS(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a; float l2 = ab.sqrMagnitude;
        float t = l2 == 0 ? 0 : Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
        return (p - a - t * ab).magnitude;
    }

    private static bool Cross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float Cr(Vector2 o, Vector2 p, Vector2 q) => (p.x - o.x) * (q.y - o.y) - (p.y - o.y) * (q.x - o.x);
        float d1 = Cr(c, d, a), d2 = Cr(c, d, b), d3 = Cr(a, b, c), d4 = Cr(a, b, d);
        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    public static Rect Vis(St s)
    {
        Seg(s, out Vector2 a, out Vector2 b);
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x) - R, Mathf.Min(a.y, b.y) - R, Mathf.Max(a.x, b.x) + R, Mathf.Max(a.y, b.y) + R);
    }

    private static float BoxGap(Rect a, Rect b)
    {
        float dx = Mathf.Max(0f, Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax)), dy = Mathf.Max(0f, Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax));
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private static bool OnGrid(float v) => Mathf.Abs(v * 8f - Mathf.Round(v * 8f)) < 0.01f;

    // ------------------------------------------------------------------ кандидат

    private class Cand
    {
        public List<St> m = new List<St>();
        public List<int> parent = new List<int>();
        public List<Rect> forbid = new List<Rect>();       // механизмы, поток, подсказки
        public List<float> columns = new List<float>();    // колонки капель (x): ниже крана ничего, кроме золота
        public float columnTop = 99f;
        public St player;
        public List<LevelDesigns.Mech> mechs = new List<LevelDesigns.Mech>();
        public int tier, golds;
        public string kind;
        public float phase;
        public int dripIndex = -1;                          // индекс крана в mechs (для подбора момента)
        public bool timing;
    }

    private static bool InBox(St s, Cand c)
    {
        Rect v = Vis(s);
        if (!OnGrid(s.x) || !OnGrid(s.y)) return false;
        if (v.xMin < -XMAX || v.xMax > XMAX || v.yMin < YMIN || v.yMax > YMAX) return false;
        if (BoxGap(v, Buttons) < 0.05f) return false;
        foreach (Rect f in c.forbid) if (BoxGap(v, f) < 0.15f) return false;
        foreach (float x in c.columns) if (v.xMin <= x + 0.22f && v.xMax >= x - 0.22f && v.yMax < c.columnTop) return false;
        return true;
    }

    /// <summary>B рядом (зазор < 0.7) только со своим источником.</summary>
    private static bool NearOnly(St b, List<St> all, int allow)
    {
        for (int i = 0; i < all.Count; i++)
        {
            if (i == allow) continue;
            if (Gap(b, all[i]) < 0.7f) return false;
        }
        return true;
    }

    /// <summary>Добавить ребёнка к parent (односторонняя связь parent → B). Возвращает false, если места нет.</summary>
    private static bool AddChild(Cand c, int parent, ref RetRng rng, Func<St, bool> extra = null, int tries = 60)
    {
        St a = c.m[parent];
        int[] rots = { 0, 90, 180, 270 };
        for (int t = 0; t < tries; t++)
        {
            int rb = rots[rng.Range(4)];
            List<Vector2> offs = offsets[(a.r, rb)];
            if (offs.Count == 0) continue;
            Vector2 d = offs[rng.Range(offs.Count)];
            var b = new St(a.x + d.x, a.y + d.y, rb);
            if (!InBox(b, c) || !NearOnly(b, c.m, parent) || (extra != null && !extra(b))) continue;
            var rel = Rel(a, b);
            if (!rel.ab || rel.ba || rel.un) continue;
            c.m.Add(b); c.parent.Add(parent);
            return true;
        }
        return false;
    }

    /// <summary>Растим тело: родитель — один из последних (лесенки) или случайный.</summary>
    private static int Grow(Cand c, int from, int n, ref RetRng rng)
    {
        int added = 0;
        for (int guard = 0; added < n && guard < n * 12; guard++)
        {
            int count = c.m.Count;
            int p = rng.Range(3) < 2 ? count - 1 - rng.Range(Math.Min(2, count - from)) : from + rng.Range(count - from);
            p = Mathf.Clamp(p, from, count - 1);
            if (AddChild(c, p, ref rng, null, 30)) added++;
        }
        return added;
    }

    /// <summary>Место для спички игрока: поджигает вход, рядом с остальными не лежит.</summary>
    private static bool FindPlayer(Cand c, int entry, ref RetRng rng, St? fixedPlayer = null)
    {
        if (fixedPlayer.HasValue) { c.player = fixedPlayer.Value; return true; }
        St e = c.m[entry];
        var cands = new List<St>();
        foreach (int rp in new[] { 0, 90, 180, 270 })
            foreach (Vector2 d in offsets[(rp, e.r)])
            {
                var p = new St(e.x - d.x, e.y - d.y, rp);
                Rect v = Vis(p);
                if (v.xMin < -XMAX || v.xMax > XMAX || v.yMin < YMIN - 0.3f || v.yMax > YMAX || BoxGap(v, Buttons) < 0.05f) continue;
                if (c.forbid.Any(f => BoxGap(v, f) < 0.1f)) continue;
                bool ok = true;
                for (int i = 0; i < c.m.Count && ok; i++) if (i != entry && Gap(p, c.m[i]) < 0.5f) ok = false;
                if (!ok) continue;
                var rel = Rel(p, e);
                if (!rel.ab || rel.un) continue;
                cands.Add(p);
            }
        if (cands.Count == 0) return false;
        c.player = cands[rng.Range(cands.Count)];
        return true;
    }

    private static float Score(Cand c)
    {
        // Чем больше соосных/параллельных рядов и чем компактнее по ширине — тем «рисованнее» выглядит уровень
        float align = 0;
        for (int i = 0; i < c.m.Count; i++)
            for (int j = i + 1; j < c.m.Count; j++)
                if (c.m[i].r % 180 == c.m[j].r % 180 && (Mathf.Abs(c.m[i].x - c.m[j].x) < 0.01f || Mathf.Abs(c.m[i].y - c.m[j].y) < 0.01f)) align++;
        float w = c.m.Max(s => Mathf.Max(Mathf.Abs(Vis(s).xMin), Mathf.Abs(Vis(s).xMax)));
        float cy = c.m.Average(s => s.y);
        return align - 3f * Mathf.Max(0f, w - 1.9f) - Mathf.Abs(cy) * 0.5f;
    }

    // ------------------------------------------------------------------ шаблоны

    /// <summary>Цепь: вход → иммунное золото (1 или 2) → тело.</summary>
    private static Cand Chain(int tier, ref RetRng rng)
    {
        var c = new Cand { tier = tier, kind = tier == 0 ? "цепь" : "два золота", golds = tier == 0 ? 1 : 2 };
        int bodyN = tier == 0 ? 5 + rng.Range(4) : 6 + rng.Range(4);
        int[] rots = { 0, 90, 180, 270 };
        var e = new St(Mathf.Round((rng.Range(30) / 10f - 1.5f) * 8f) / 8f, Mathf.Round((rng.Range(40) / 10f - 2.2f) * 8f) / 8f, rots[rng.Range(4)]);
        if (!InBox(e, c)) return null;
        c.m.Add(e); c.parent.Add(-1);
        for (int g = 0; g < c.golds; g++)
        {
            if (!AddChild(c, c.m.Count - 1, ref rng)) return null;
            St s = c.m[c.m.Count - 1]; s.gold = true; s.immune = true; c.m[c.m.Count - 1] = s;
        }
        int from = 1;
        if (Grow(c, from, bodyN, ref rng) < bodyN - 1) return null;
        if (!FindPlayer(c, 0, ref rng)) return null;
        return c;
    }

    /// <summary>Кран над иммунным золотом: вход → золото под краном → тело. Момент поджога важен.</summary>
    private static Cand Tap(int tier, ref RetRng rng)
    {
        var c = new Cand { tier = tier, kind = "кран", golds = 1, timing = true };
        int[] rots = { 0, 90, 180, 270 };
        var e = new St(Mathf.Round((rng.Range(30) / 10f - 1.5f) * 8f) / 8f, Mathf.Round((rng.Range(30) / 10f - 2.2f) * 8f) / 8f, rots[rng.Range(4)]);
        if (!InBox(e, c)) return null;
        c.m.Add(e); c.parent.Add(-1);
        // Золото: над ним должно быть место для крана (колонка свободна вверх), ниже — тоже ничего
        for (int t = 0; t < 40; t++)
        {
            int before = c.m.Count;
            if (!AddChild(c, 0, ref rng)) return null;
            St g = c.m[1];
            Rect v = Vis(g);
            float x = g.r % 180 == 90 ? g.x : Mathf.Round((v.center.x + (rng.Range(5) - 2) * 0.25f) * 8f) / 8f;
            x = Mathf.Clamp(x, v.xMin + 0.08f, v.xMax - 0.08f);
            x = Mathf.Round(x * 8f) / 8f;
            float nozzle = v.yMax + 0.675f;
            var tapBox = Rect.MinMaxRect(x - 0.53f, v.yMax + 0.1f, x + 0.53f, v.yMax + 1.08f);
            bool ok = tapBox.yMax <= 3.0f && tapBox.xMin >= -XMAX - 0.05f && tapBox.xMax <= XMAX + 0.05f && BoxGap(tapBox, Buttons) >= 0.05f
                      && BoxGap(tapBox, Vis(e)) >= 0.15f && x >= v.xMin + 0.06f && x <= v.xMax - 0.06f;
            Rect ve = Vis(e);
            if (ok && ve.xMin <= x + 0.22f && ve.xMax >= x - 0.22f) ok = false;   // вход не в струе
            if (!ok) { c.m.RemoveAt(1); c.parent.RemoveAt(1); continue; }
            g.gold = g.immune = true; c.m[1] = g;
            c.forbid.Add(tapBox);
            c.columns.Add(x); c.columnTop = v.yMin;   // ниже золота в колонке ничего
            c.mechs.Add(LevelDesigns.Dripper(x, nozzle).Set("interval", 4f).Set("startDelay", 0f).Set("wetDuration", 0.4f));
            c.dripIndex = c.mechs.Count - 1;
            break;
        }
        if (c.m.Count < 2) return null;
        int bodyN = 6 + rng.Range(4);
        if (Grow(c, 1, bodyN, ref rng) < bodyN - 1) return null;
        if (!FindPlayer(c, 0, ref rng)) return null;
        // Спичка игрока не в струе
        Rect vp = Vis(c.player);
        foreach (float x in c.columns) if (vp.xMin <= x + 0.22f && vp.xMax >= x - 0.22f) return null;
        return c;
    }

    /// <summary>Труба с ветром (как L25/L29): вход и голубая соосно с зазором 0.2 — огонь перелетает только на ветру.</summary>
    private static Cand Pipe(int tier, bool twoGolds, bool withTap, ref RetRng rng)
    {
        float fx = rng.Range(2) == 0 ? 1.25f : -1.25f;
        var c = new Cand { tier = tier, kind = withTap ? "труба + кран" : twoGolds ? "труба, два золота" : "труба", golds = twoGolds ? 2 : 1, timing = true };
        c.mechs.Add(LevelDesigns.FanAt(fx, -2.8f, 90f).Set("mode", Fan.Mode.Cycle).Set("onDuration", 3f).Set("offDuration", 1f).Set("cyclePhase", 0f)
            .Set("strength", 6f).Set("farStrength", 0.5f).Set("zoneLength", 2.2f).Set("zoneWidth", 0.6f).Set("extinguishStrength", 50f).Set("extinguishTime", 0.35f));
        c.forbid.Add(Rect.MinMaxRect(fx - 0.44f, -3.36f, fx + 0.44f, -2.24f));
        c.forbid.Add(Rect.MinMaxRect(fx - 0.45f, -2.3f, fx + 0.45f, 0.25f));           // поток (кроме трубы)
        var e = new St(fx, -1.125f, 90);
        var u = new St(fx, 0.625f, 90) { immune = true };
        c.m.Add(e); c.parent.Add(-1);
        c.m.Add(u); c.parent.Add(0);
        var player = new St(fx + 0.5f, -1.125f, 90);
        c.forbid.Add(Rect.MinMaxRect(Vis(player).xMin - 0.1f, Vis(player).yMin, Vis(player).xMax + 0.1f, Vis(player).yMax));
        // InBox для фиксированных частей не проверяем (они в «запретной» зоне потока)
        for (int g = 0; g < c.golds; g++)
        {
            if (!AddChild(c, c.m.Count - 1, ref rng)) return null;
            St s = c.m[c.m.Count - 1]; s.gold = true; s.immune = true; c.m[c.m.Count - 1] = s;
        }
        if (withTap)
        {
            St g = c.m[2]; Rect v = Vis(g);
            float x = g.r % 180 == 90 ? g.x : Mathf.Round(Mathf.Clamp(v.center.x, v.xMin + 0.1f, v.xMax - 0.1f) * 8f) / 8f;
            var tapBox = Rect.MinMaxRect(x - 0.53f, v.yMax + 0.1f, x + 0.53f, v.yMax + 1.08f);
            if (tapBox.yMax > 3.0f || BoxGap(tapBox, Buttons) < 0.05f || c.m.Any(s => BoxGap(Vis(s), tapBox) < 0.15f && !s.Equals(g))) return null;
            foreach (St s in c.m) if (!s.Equals(g)) { Rect vs = Vis(s); if (vs.xMin <= x + 0.22f && vs.xMax >= x - 0.22f && vs.yMin > v.yMax) return null; }
            c.forbid.Add(tapBox);
            c.columns.Add(x); c.columnTop = v.yMin;
            c.mechs.Add(LevelDesigns.Dripper(x, v.yMax + 0.675f).Set("interval", 4f).Set("startDelay", 0f).Set("wetDuration", 0.4f));
            c.dripIndex = c.mechs.Count - 1;
        }
        int bodyN = 5 + rng.Range(4);
        if (Grow(c, 2, bodyN, ref rng) < bodyN - 1) return null;
        c.player = player;
        return c;
    }

    // ------------------------------------------------------------------ спецификация и проверка

    private static LevelDesigns.Level ToSpec(Cand c, int n)
    {
        var l = new LevelDesigns.Level
        {
            index = 1000 + n, difficulty = Mathf.Min(3, c.tier + 1), name = $"Daily_{n:000}",
            titleRU = "Коробок дня", titleEN = "Daily box", free = true,
            idea = $"Уровень дня ({c.kind})", player = LevelDesigns.S(0.75f, -3.625f, 0f),
        };
        foreach (St s in c.m)
        {
            LevelDesigns.Match m = LevelDesigns.S(s.x, s.y, s.r);
            if (s.gold) m.Gold();
            if (s.immune) m.Immune();
            l.matches.Add(m);
        }
        l.mechs.AddRange(c.mechs);
        LevelDesigns.Match sol = LevelDesigns.S(c.player.x, c.player.y, c.player.r);
        l.solutionCenter = sol.center; l.solutionRot = c.player.r; l.solutionPhase = c.phase;
        l.solutionText = "Генератор: спичка игрока у входа";
        return l;
    }

    /// <summary>Лучшее окно 3★ (по моменту поджога) для разных задержек крана; подбирает задержку и момент решения.</summary>
    private static float TuneTiming(Cand c, int n, out float phase)
    {
        phase = 0f;
        float best = -1f, bestDelay = 0f;
        float[] delays = c.dripIndex >= 0 ? new[] { 0f, 0.5f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f } : new[] { 0f };
        foreach (float d in delays)
        {
            if (c.dripIndex >= 0) c.mechs[c.dripIndex].Set("startDelay", d);
            LevelDesigns.Level spec = ToSpec(c, n);
            GameObject root = LevelBuilder.Build(spec);
            root.transform.position = Vector3.zero;
            Physics2D.SyncTransforms();
            float w, ph;
            using (var sim = new LevelSimulator(root, owned: false))
            {
                float period = Mathf.Max(0.1f, sim.TimingPeriod());
                Vector2 solRoot = sim.PlayerRootFor(spec.solutionCenter, spec.solutionRot);
                int steps = Mathf.RoundToInt(period / 0.1f);
                var ok = new bool[steps];
                for (int i = 0; i < steps; i++) ok[i] = sim.Run(solRoot, spec.solutionRot, i * 0.1f).stars == 3;
                Window(ok, 0.1f, out w, out ph);
            }
            UnityEngine.Object.DestroyImmediate(root);
            if (w > best) { best = w; bestDelay = d; phase = ph; }
        }
        if (c.dripIndex >= 0) c.mechs[c.dripIndex].Set("startDelay", bestDelay);
        return best;
    }

    private static void Window(bool[] ok, float dt, out float window, out float center)
    {
        int n = ok.Length, bestLen = 0, bestStart = 0;
        if (ok.All(x => x)) { window = n * dt; center = 0f; return; }
        for (int s = 0; s < n; s++)
        {
            if (!ok[s] || ok[(s - 1 + n) % n]) continue;
            int len = 0;
            while (len < n && ok[(s + len) % n]) len++;
            if (len > bestLen) { bestLen = len; bestStart = s; }
        }
        window = bestLen * dt;
        center = ((bestStart + bestLen / 2f) % n) * dt;
    }

    [MenuItem("Tools/Match & Flame/Retention/Build daily pool")]
    public static void Build()
    {
        LoadTable();
        int onlyTier = int.TryParse(Environment.GetEnvironmentVariable("DAILY_TIER"), out int t0) ? t0 : -1;
        int seed = int.TryParse(Environment.GetEnvironmentVariable("DAILY_SEED"), out int s0) ? s0 : 7;
        int maxTries = int.TryParse(Environment.GetEnvironmentVariable("DAILY_TRIES"), out int m0) ? m0 : 400;
        int[] counts = (int[])DefaultCounts.Clone();
        if (int.TryParse(Environment.GetEnvironmentVariable("DAILY_COUNT"), out int cnt)) for (int i = 0; i < 4; i++) counts[i] = cnt;

        Directory.CreateDirectory(Folder);
        if (AssetDatabase.LoadAssetAtPath<DailyLevelPool>(PoolPath) == null) { AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<DailyLevelPool>(), PoolPath); AssetDatabase.SaveAssets(); }
        // NewScene выгружает неиспользуемые ассеты — таблицу берём заново при каждом обращении
        DailyLevelPool pool = AssetDatabase.LoadAssetAtPath<DailyLevelPool>(PoolPath);
        var log = new StringBuilder("DailyLevelGenerator\n");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int tier = 0; tier < 4; tier++)
        {
            if (onlyTier >= 0 && tier != onlyTier) continue;
            var rng = new RetRng(RetRng.Mix(seed, tier * 101 + pool.entries.Count));
            int tries = 0;
            while (pool.entries.Count(e => e.tier == tier) < counts[tier] && tries++ < maxTries)
            {
                // Кандидат: лучший из нескольких по «рисованности»
                Cand best = null; float bestScore = float.MinValue;
                for (int k = 0; k < 12; k++)
                {
                    Cand c = tier == 0 || tier == 1 ? Chain(tier, ref rng)
                           : tier == 2 ? (rng.Range(3) < 2 ? Tap(tier, ref rng) : Pipe(tier, false, false, ref rng))
                           : (rng.Range(2) == 0 ? Pipe(tier, true, false, ref rng) : Pipe(tier, false, true, ref rng));
                    if (c == null) continue;
                    float sc = Score(c);
                    if (sc > bestScore) { bestScore = sc; best = c; }
                }
                if (best == null) continue;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                pool = AssetDatabase.LoadAssetAtPath<DailyLevelPool>(PoolPath);
                int n = pool.entries.Count;
                float window = 0f;
                if (best.timing)
                {
                    window = TuneTiming(best, n, out float ph);
                    if (window < 1.6f) { log.AppendLine($"  tier {tier} {best.kind}: окно {window:0.00} — мимо"); continue; }
                    best.phase = ph;
                }
                LevelDesigns.Level spec = ToSpec(best, n);
                GameObject root = LevelBuilder.Build(spec);
                root.transform.position = Vector3.zero;
                Physics2D.SyncTransforms();
                // Быстрый отсев: задуманное решение
                using (var sim = new LevelSimulator(root, owned: false))
                {
                    var r = sim.Run(sim.PlayerRootFor(spec.solutionCenter, spec.solutionRot), spec.solutionRot, spec.solutionPhase);
                    if (r.stars < 3 || r.goldenBurnt < r.golden) { log.AppendLine($"  tier {tier} {best.kind}: решение {r.stars}★ — мимо"); UnityEngine.Object.DestroyImmediate(root); continue; }
                }
                var issues = new List<string>();
                LayoutCheck.Check(spec, root, null, new StringBuilder(), issues);
                if (issues.Count > 0) { log.AppendLine($"  tier {tier} {best.kind}: LayoutCheck {issues.Count}: {issues[0]}"); UnityEngine.Object.DestroyImmediate(root); continue; }
                LevelBuilder.Evaluation ev = LevelBuilder.Evaluate(spec, root);
                if (!ev.Passed) { log.AppendLine($"  tier {tier} {best.kind}: {LevelBuilder.Describe(ev)}"); UnityEngine.Object.DestroyImmediate(root); continue; }
                string path = $"{Folder}/{spec.name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                int tags = (int)RetentionTools.TagsOf(root, tier >= 2 ? 3 : 1);
                UnityEngine.Object.DestroyImmediate(root);
                if (!saved) { log.AppendLine("  ERROR saving " + path); continue; }
                pool = AssetDatabase.LoadAssetAtPath<DailyLevelPool>(PoolPath);
                pool.entries.Add(new DailyLevelPool.Entry
                {
                    prefab = spec.name, tier = tier, tags = tags, solutionCenter = ev.solutionRoot, solutionRot = spec.solutionRot,
                    solutionPhase = spec.solutionPhase, matches = best.m.Count, window = window,
                    randomWinShare = ev.randomTried > 0 ? (float)ev.randomWins / ev.randomTried : 0f,
                });
                EditorUtility.SetDirty(pool);
                AssetDatabase.SaveAssets();
                log.AppendLine($"  + {spec.name} tier {tier} {best.kind}: спичек {best.m.Count}, окно {window:0.00}, {LevelBuilder.Describe(ev)}");
                Debug.Log($"[Polish] daily + {spec.name} tier {tier} ({watch.Elapsed.TotalMinutes:0.0} min)");
            }
        }
        pool = AssetDatabase.LoadAssetAtPath<DailyLevelPool>(PoolPath);
        log.AppendLine($"Пул: {pool.entries.Count} (по уровням сложности {string.Join("/", Enumerable.Range(0, 4).Select(t => pool.entries.Count(e => e.tier == t)))}), {watch.Elapsed.TotalMinutes:0.0} мин");
        File.AppendAllText("Builds/daily-pool.log", log.ToString());
        Debug.Log("[Polish] " + log);
    }

    /// <summary>Схемы всех уровней пула (ASCII, как LayoutAscii) → Builds/daily-pool-ascii.txt.</summary>
    [MenuItem("Tools/Match & Flame/Retention/Daily pool ASCII")]
    public static void Ascii()
    {
        var pool = AssetDatabase.LoadAssetAtPath<DailyLevelPool>(PoolPath);
        var sb = new StringBuilder();
        string[] tierName = { "лёгкий", "средний", "сложный", "особый" };
        for (int i = 0; i < pool.entries.Count; i++)
        {
            DailyLevelPool.Entry e = pool.entries[i];
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            pool = AssetDatabase.LoadAssetAtPath<DailyLevelPool>(PoolPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{e.prefab}.prefab");
            if (prefab == null) { sb.AppendLine($"{e.prefab}: НЕТ ПРЕФАБА"); continue; }
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            root.transform.position = Vector3.zero;
            Physics2D.SyncTransforms();
            sb.AppendLine($"{e.prefab} — {tierName[Mathf.Clamp(e.tier, 0, 3)]}, спичек {e.matches}, механики {(RetTags)e.tags}, окно {e.window:0.00} с, случайные {e.randomWinShare:P0}");
            sb.Append(LayoutAscii.Draw(root));
            UnityEngine.Object.DestroyImmediate(root);
        }
        File.WriteAllText("Builds/daily-pool-ascii.txt", sb.ToString());
        Debug.Log("[Polish] Daily pool ASCII\n" + sb);
    }
}
