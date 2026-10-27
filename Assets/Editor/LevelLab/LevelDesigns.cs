using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Данные новых уровней (Level_17…Level_21) — правьте здесь и пересобирайте:
/// Tools → Match &amp; Flame → Level Lab → Build new levels.
///
/// Match(x, y, rot) — ЦЕНТР спички (центр её коллайдера) в мировых единицах, уровень создаётся в (0,0).
/// rot — поворот в градусах: при rot = 0 головка слева и огонь идёт вправо; 90 — огонь вверх;
/// 180 — влево; 270 — вниз. Длина спички ≈ 1.72; огонь идёт от головки (−0.6 от центра вдоль оси)
/// до «хвоста» (+0.33), линия огня смещена на 0.11 влево от оси. AtHead/AtTail ставят спичку по этим точкам.
/// Видимая половина ширины при размере камеры 5: 2.81 (16:9), 2.25 (20:9) — края спичек держим в |x| ≤ ~2.2;
/// правый верх (x > 1.3, y 1.5…2.7) занят кнопками.
/// </summary>
public static partial class LevelDesigns
{
    public const float HeadAlong = -0.6f, TailAlong = 0.33f, PathSide = 0.11f;

    public class Match
    {
        public Vector2 center;
        public float rot;
        public bool golden, immune;
        public float flashRange = -1f, flashRadius = -1f, spreadRadius = -1f, sideDelay = -1f;
        public Vector3 oscAxis;
        public float oscDistance, oscSpeed;

        public Match(float x, float y, float rot) { center = new Vector2(x, y); this.rot = Mathf.Repeat(rot, 360f); }
        public Match Gold() { golden = true; return this; }
        public Match Immune() { immune = true; return this; }
        public Match Osc(Vector3 axis, float distance, float speed) { oscAxis = axis; oscDistance = distance; oscSpeed = speed; return this; }

        /// <summary>Зеркальная копия относительно оси x = 0 (головка тоже зеркальна).</summary>
        public Match Mirror()
        {
            var m = (Match)MemberwiseClone();
            m.center = new Vector2(-center.x, center.y);
            m.rot = Mathf.Repeat(180f - rot, 360f);
            return m;
        }

        public Vector2 Head => center + HeadAlong * Dir(rot) + PathSide * Side(rot);
        public Vector2 Tail => center + TailAlong * Dir(rot) + PathSide * Side(rot);
    }

    public static Vector2 Dir(float rot) => new Vector2(Mathf.Cos(rot * Mathf.Deg2Rad), Mathf.Sin(rot * Mathf.Deg2Rad));
    public static Vector2 Side(float rot) { Vector2 d = Dir(rot); return new Vector2(-d.y, d.x); }

    // Видимая палочка смещена от центра коллайдера: +0.055 вдоль оси, −0.04 поперёк (см. LayoutCheck)
    public const float VisibleAlong = 0.055f, VisibleSide = -0.04f;

    /// <summary>
    /// Спичка по ЦЕНТРУ ВИДИМОЙ ПАЛОЧКИ (x, y) — так задаются уровни 17–21 на сетке 0.25.
    /// rot — куда идёт огонь: 0 вправо, 90 вверх, 180 влево, 270 вниз (головка — с противоположного конца).
    /// </summary>
    public static Match S(float x, float y, float rot)
    {
        Vector2 c = new Vector2(x, y) - VisibleAlong * Dir(rot) - VisibleSide * Side(rot);
        return new Match(c.x, c.y, rot);
    }

    /// <summary>Спичка, у которой головка (начало огня) — в точке (x, y).</summary>
    public static Match AtHead(float x, float y, float rot)
    {
        Vector2 c = new Vector2(x, y) - HeadAlong * Dir(rot) - PathSide * Side(rot);
        return new Match(c.x, c.y, rot);
    }

    /// <summary>Спичка, у которой «хвост» (конец огня) — в точке (x, y).</summary>
    public static Match AtTail(float x, float y, float rot)
    {
        Vector2 c = new Vector2(x, y) - TailAlong * Dir(rot) - PathSide * Side(rot);
        return new Match(c.x, c.y, rot);
    }

    /// <summary>Цепочка: каждая следующая спичка начинается чуть дальше хвоста предыдущей (вспышка передаёт огонь).</summary>
    public static List<Match> Chain(Vector2 firstHead, float gap, params float[] rots)
    {
        var list = new List<Match>();
        Vector2 head = firstHead;
        for (int i = 0; i < rots.Length; i++)
        {
            Match m = AtHead(head.x, head.y, rots[i]);
            list.Add(m);
            if (i + 1 < rots.Length)
            {
                Vector2 bis = (Dir(rots[i]) + Dir(rots[i + 1])).normalized;
                head = m.Tail + gap * bis;
            }
        }
        return list;
    }

    public class Level
    {
        public int index, difficulty;
        public string name, titleRU, titleEN, idea, solutionText;
        public bool free;
        public Match player;
        public List<Match> matches = new List<Match>();
        public Vector2 slotCenter;
        public float slotRot;
        public Vector2 solutionCenter;
        public float solutionRot, solutionPhase;
        public string hintEN, hintRU, hintIcon;
        public Vector2 hintPosition;
        public float hintWidth = 3.6f, hintFontSize = 2.3f;
        public List<Mech> mechs = new List<Mech>();

        public void Pair(Match left) { matches.Add(left); matches.Add(left.Mirror()); }
    }

    // ---------------------------------------------------------------- Механики на уровне

    /// <summary>Кран, вентилятор или зонтик: позиция, поворот, параметры инспектора (имя поля → значение).</summary>
    public class Mech
    {
        public enum Kind { Dripper, Fan, Shelter }
        public Kind kind;
        public Vector2 pos;
        public float rot;
        public bool flip;
        public readonly Dictionary<string, object> props = new Dictionary<string, object>();
        public Vector3 oscAxis;
        public float oscDistance, oscSpeed;

        public Mech Set(string field, object value) { props[field] = value; return this; }
        public Mech Osc(Vector3 axis, float distance, float speed) { oscAxis = axis; oscDistance = distance; oscSpeed = speed; return this; }
    }

    public static Mech Dripper(float x, float y) => new Mech { kind = Mech.Kind.Dripper, pos = new Vector2(x, y) };
    public static Mech Shelter(float x, float y) => new Mech { kind = Mech.Kind.Shelter, pos = new Vector2(x, y) };
    /// <summary>Вентилятор: rot — куда дует (0 вправо, 90 вверх); flip — дует влево, подставка внизу.</summary>
    public static Mech FanAt(float x, float y, float rot = 0f, bool flip = false) => new Mech { kind = Mech.Kind.Fan, pos = new Vector2(x, y), rot = rot, flip = flip };

    public static List<Level> All() => new List<Level> { L17(), L18(), L19(), L20(), L21(), L22(), L23(), L24(), L25(), L26(), L27(), L28(), L29(), L30(), L31() };

    // ---------------------------------------------------------------- 17. Ёлочка (без механик, разогрев)
    // Семь вертикальных веток по росту — силуэт ёлки (сетка 0.5 по x, ступень 0.5 по высоте). Огонь в ветках идёт вниз,
    // пламя по правому краю: каждая ветка поджигает соседку справа («лесенка», зазор 0.28), назад огонь не идёт.
    // Вход — крайняя левая ветка, сразу за ней золотая (иммунна к спичке игрока): золото горит, только если зажечь вход.
    private static Level L17()
    {
        var l = new Level
        {
            index = 17, difficulty = 1, name = "Level_17", titleRU = "Ёлочка", titleEN = "Fir tree", free = true,
            idea = "Ветки-ёлочка из семи вертикальных спичек, огонь переходит только слева направо. Подожги крайнюю левую ветку — огонь пройдёт золотую и всю ёлку. Если зажечь ветку в середине, левая часть и золото не сгорят.",
            player = S(0f, -3.5f, 0f),
        };
        float[] heights = { 0f, 0.5f, 1f, 1.5f, 1f, 0.5f, 0f };
        for (int i = 0; i < heights.Length; i++)
        {
            Match m = S(-1.5f + 0.5f * i, heights[i], 270f);
            if (i == 1) m.Gold().Immune();
            l.matches.Add(m);
        }
        Match sol = S(-0.875f, -1.125f, 0f); // спичка игрока лежит под левой веткой, огонь вправо: её пламя достаёт до нижнего конца ветки
        l.solutionCenter = sol.center; l.solutionRot = 0f;
        l.solutionText = "Горизонтально под крайней левой веткой (Т-стык к её нижнему концу): огонь поднимается по ней и идёт по ёлке слева направо.";
        return l;
    }

    // ---------------------------------------------------------------- 18. Два крана (введение капли)
    // Кран 1 капает прямо на золотую стойку: её надо зажечь сразу после капли — пока она горит (2 с), следующая
    // не успевает. Стойка лесенкой поджигает левую сторону «окна», огонь обходит окно по кругу (углы — Т-стыки 0.29).
    // Кран 2 капает на зонтик над окном: без зонтика капли падали бы на верх окна.
    // Стойка вертикальная, огонь вниз: сверху (под струёй) её поджигает только узкое окно в 4 точки сетки.
    private static Level L18()
    {
        var l = new Level
        {
            index = 18, difficulty = 1, name = "Level_18", titleRU = "Два крана", titleEN = "Two taps", free = true,
            idea = "Капля гасит горящую спичку и мочит несгоревшую. Кран 1 капает на золотую стойку — зажги её сразу после капли, и огонь обойдёт окно. Над окном второй кран, но его капли разбиваются о зонтик.",
            player = S(0f, -3.5f, 0f),
            hintEN = "Water puts fire out.\nStart right after a drop!", hintRU = "Вода гасит огонь.\nПоджигай сразу после капли!",
            hintPosition = new Vector2(0f, -2.5f), hintIcon = "water",
        };
        l.matches.Add(S(-1.125f, 0.25f, 270f).Gold());           // стойка под краном 1, огонь вниз
        // Окно справа: левая ↓ (лесенка от стойки), верх ←, правая ↑, низ →
        l.matches.Add(S(-0.625f, -0.625f, 270f));
        l.matches.Add(S(0.5f, 0.25f, 180f));
        l.matches.Add(S(1.625f, -0.625f, 90f));
        l.matches.Add(S(0.5f, -1.5f, 0f));
        l.mechs.Add(Dripper(-1.125f, 1.52f).Set("interval", 4f).Set("startDelay", 1f).Set("wetDuration", 0.4f));   // сопло (−1.125, 1.5)
        l.mechs.Add(Shelter(0.25f, 0.875f));
        l.mechs.Add(Dripper(0.25f, 1.52f).Set("interval", 1.2f).Set("startDelay", 0.3f).Set("wetDuration", 1f));
        Match sol = S(-1.625f, 0.25f, 270f);  // слева от стойки, огонь вниз: лесенка к стойке
        l.solutionCenter = sol.center; l.solutionRot = 270f; l.solutionPhase = 1.6f;
        l.solutionText = "Вертикально слева от золотой стойки, огнём вниз, сразу после того как на неё упала капля.";
        return l;
    }

    // ---------------------------------------------------------------- 19. Тяга (введение вентилятора)
    // Вентилятор внизу справа дует вверх по «трубе» из двух соосных спичек. Между ними зазор 0.2: вспышка нижней
    // перелетает его только на ветру (вентилятор работает циклами: 1.5 с дует, 2.5 с стоит). Верхняя — золотая; от неё огонь уходит
    // перекладиной влево и спускается по лесенке. Нижняя спичка трубы — 77 % порога «задувает» (не гаснет), вспышка
    // на её конце — 123 % нужной дальности; у самого сопла ветер сильнее порога в 1.2 раза — там спичку задует.
    private static Level L19()
    {
        var l = new Level
        {
            index = 19, difficulty = 2, name = "Level_19", titleRU = "Тяга", titleEN = "Updraft", free = true,
            idea = "Вентилятор дует вверх по трубе. Огонь перелетает зазор между спичками трубы только на ветру, а вентилятор то дует, то стоит. Подожги нижнюю спичку так, чтобы она догорела, пока он дует; у самого вентилятора огонь задувает.",
            player = S(-0.75f, -3.75f, 0f),
            // Подсказка слева от потока воздуха, узкая (три строки, без иконки): справа поток, слева край кадра
            hintEN = "Wind carries fire.\nRight at the fan\nit blows out!", hintRU = "Ветер несёт огонь.\nВплотную к нему\nзадует!",
            hintPosition = new Vector2(-0.75f, -1.25f), hintWidth = 2.4f, hintFontSize = 2.1f,
        };
        l.mechs.Add(FanAt(1f, -3.19f, 90f).Set("mode", Fan.Mode.Cycle).Set("onDuration", 1.5f).Set("offDuration", 2.5f)   // сопло (1, −2.5)
            .Set("strength", 9.75f).Set("farStrength", 0.4f).Set("zoneLength", 3.25f).Set("zoneWidth", 0.6f)
            .Set("extinguishStrength", 7.5f).Set("extinguishTime", 0.35f));
        l.matches.Add(S(1f, 0.375f, 90f));                     // труба: нижняя
        l.matches.Add(S(1f, 2.125f, 90f).Gold().Immune());     // труба: верхняя, зазор 0.2 — только по ветру
        l.matches.Add(S(-0.125f, 2.125f, 180f));               // перекладина: Т-стык к середине золотой
        for (int i = 1; i <= 4; i++) l.matches.Add(S(-0.875f, 2.125f - 0.5f * i, 180f)); // лесенка вниз
        Match sol = S(1.5f, 0.375f, 90f);   // справа от нижней спички трубы, вне потока: лесенка к ней
        l.solutionCenter = sol.center; l.solutionRot = 90f; l.solutionPhase = 2.4f;
        l.solutionText = "Вертикально справа от нижней спички трубы (вне потока), огнём вверх — когда вентилятор вот-вот включится: она догорит на ветру, и вспышка перелетит к золотой.";
        return l;
    }

    // ---------------------------------------------------------------- 20. Ворота (ветер + капля + маятник)
    // Правая стойка — «труба» над вентилятором: нижняя спичка (вход) и верхняя (иммунная) с зазором 0.2, огонь
    // перелетает его только на ветру. Верхняя поджигает перекладину — золотую (иммунную), над ней кран и качающийся
    // зонтик (период 4 с): перекладина должна сгореть между каплями или пока зонтик над струёй. От перекладины —
    // две левые стойки лесенкой. Обойти ветер нельзя: верх трубы и золото своей спичкой не поджечь.
    private static Level L20()
    {
        var l = new Level
        {
            index = 20, difficulty = 3, name = "Level_20", titleRU = "Ворота", titleEN = "Gate", free = true,
            idea = "Огонь поднимается по правой стойке: зазор в ней перелетает только на ветру. Дальше — золотая перекладина под краном, над ней качается зонтик. Поджог должен совпасть и с ветром, и с паузой между каплями.",
            player = S(-0.75f, -3.5f, 0f),
            // Здесь впервые видна иммунная (голубая) спичка — подсказка слева внизу, узкая
            hintEN = "Blue matches\ncan't be lit\nby your match", hintRU = "Голубую спичку\nсвоей спичкой\nне зажечь",
            hintPosition = new Vector2(-0.75f, -2.375f), hintWidth = 2.4f, hintFontSize = 2.1f,
        };
        const float W = Mathf.PI / 2f; // период 4 с
        l.mechs.Add(FanAt(1f, -2.44f, 90f).Set("mode", Fan.Mode.Cycle).Set("onDuration", 2f).Set("offDuration", 2f)   // сопло (1, −1.75)
            .Set("strength", 8.5f).Set("farStrength", 0.3f).Set("zoneLength", 2.38f).Set("zoneWidth", 0.6f)
            .Set("extinguishStrength", 9f).Set("extinguishTime", 0.35f));
        l.matches.Add(S(1f, -0.5f, 90f));                      // труба: нижняя (вход)
        l.matches.Add(S(1f, 1.25f, 90f).Immune());             // труба: верхняя, зазор 0.2 — только по ветру
        l.matches.Add(S(-0.125f, 1f, 180f).Gold().Immune());   // перекладина: Т-стык к середине верхней
        l.matches.Add(S(-0.75f, -0.125f, 90f));                // левая стойка: Т-стык к перекладине
        l.matches.Add(S(-1.25f, -0.625f, 90f));                // вторая левая стойка: лесенка
        l.mechs.Add(Dripper(-0.25f, 2.02f).Set("interval", 4f).Set("startDelay", 1f).Set("wetDuration", 0.4f));   // сопло (−0.25, 2.0)
        l.mechs.Add(Shelter(-0.25f, 1.625f).Osc(new Vector3(1f, 0f, 0f), 0.4f, W));
        Match sol = S(1.5f, -0.5f, 90f);   // справа от нижней спички трубы, вне потока
        l.solutionCenter = sol.center; l.solutionRot = 90f; l.solutionPhase = 2.5f;
        l.solutionText = "Вертикально справа от нижней спички трубы (вне потока), огнём вверх — чтобы она догорела на ветру, а золотая перекладина горела между каплями.";
        return l;
    }

    // ---------------------------------------------------------------- 21. Маятник (финал: ветер, маятник, капля)
    // Вход — горизонтальная спичка над вентилятором. Без ветра её пламя не достаёт до башни (иммунной) над её концом:
    // ветер приподнимает пламя (циклы 2 с / 2 с). Башня лесенкой поджигает золотой маятник, если он в этот момент
    // не слишком высоко (качается вверх-вниз, период 4 с), а на маятник капает кран (раз в 4 с) — он должен сгореть
    // между каплями. От маятника огонь спускается лесенкой из четырёх вертикальных спичек.
    private static Level L21()
    {
        var l = new Level
        {
            index = 21, difficulty = 3, name = "Level_21", titleRU = "Маятник", titleEN = "Pendulum", free = true,
            idea = "Три ритма по 4 с: вентилятор помогает огню перепрыгнуть со входа на башню, золотой маятник ловит огонь башни, только когда он внизу, а кран капает на маятник. Подожги вход так, чтобы всё совпало.",
            player = S(-0.75f, -3.625f, 0f),
        };
        const float W = Mathf.PI / 2f; // период 4 с
        l.mechs.Add(FanAt(1f, -3.58f, 90f).Set("mode", Fan.Mode.Cycle).Set("onDuration", 2f).Set("offDuration", 2f)   // сопло (1, −2.89)
            .Set("strength", 7f).Set("farStrength", 0.3f).Set("zoneLength", 2f).Set("zoneWidth", 0.6f)
            .Set("extinguishStrength", 7f).Set("extinguishTime", 0.35f));
        l.matches.Add(S(0.875f, -2f, 0f));                     // вход над вентилятором, огонь вправо (пламя по верхнему краю)
        l.matches.Add(S(1f, -0.5f, 90f).Immune());             // башня: над концом входа, зазор 0.6 — только по ветру
        Match pendulum = S(0.5f, 0.125f, 90f).Gold().Immune(); // маятник: лесенка от башни, качается вверх-вниз ±0.5
        pendulum.Osc(new Vector3(0f, 1f, 0f), 0.5f, W);
        l.matches.Add(pendulum);
        l.matches.Add(S(0f, 0.125f, 90f));                     // лесенка вниз-влево
        l.matches.Add(S(-0.5f, -0.375f, 90f));
        l.matches.Add(S(-1f, -0.875f, 90f));
        l.matches.Add(S(-1.5f, -1.375f, 90f));
        l.mechs.Add(Dripper(0.5f, 1.645f).Set("interval", 4f).Set("startDelay", 0f).Set("wetDuration", 0.4f));   // сопло (0.5, 1.625)
        Match sol = S(0.25f, -2.5f, 0f);   // под входом, левее потока: лесенка ко входу
        l.solutionCenter = sol.center; l.solutionRot = 0f; l.solutionPhase = 1.4f;
        l.solutionText = "Горизонтально под входом, левее потока воздуха, огнём вправо — когда вентилятор вот-вот включится, маятник идёт вниз, а капля только что упала.";
        return l;
    }
}
