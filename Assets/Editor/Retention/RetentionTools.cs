using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Инструменты «Очага»:
///  • Refresh settings — создаёт/обновляет RetentionSettings.asset (шрифт, спрайты UI, звуки, механики и сложность уровней каталога);
///  • Tests — защита времени и повреждённые сохранения → Builds/retention-tests.md;
///  • Simulate 60 days — экономика по профилям игроков → Builds/retention-sim.md.
/// </summary>
public static class RetentionTools
{
    public const string SettingsPath = "Assets/Resources/RetentionSettings.asset";

    [MenuItem("Tools/Match & Flame/Retention/Refresh settings (font, sprites, level tags)")]
    public static void Refresh()
    {
        var s = AssetDatabase.LoadAssetAtPath<RetentionSettings>(SettingsPath);
        if (s == null)
        {
            s = ScriptableObject.CreateInstance<RetentionSettings>();
            RetentionDefaults.Fill(s);
            AssetDatabase.CreateAsset(s, SettingsPath);
        }
        s.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/LiberationSans RU SDF.asset");
        s.card = Sprite("Assets/Art/UI/ui_card.png");
        s.button = Sprite("Assets/Art/UI/ui_button.png");
        s.buttonLight = Sprite("Assets/Art/UI/ui_button_light.png");
        s.pill = Sprite("Assets/Art/UI/ui_pill.png");
        s.shadow = Sprite("Assets/Art/UI/ui_shadow.png");
        s.glow = Sprite("Assets/Art/Generated/glow.png");
        s.rewardSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/light-a-match-the-second-time_9CtFcAvh.mp3");
        s.stageSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/burn2.mp3");
        s.tapSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/knocked-on-a-wooden-surface-once.mp3");
        // Процедурный атлас очага (если уже нарисован)
        s.art = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Art/Retention" })
            .Select(AssetDatabase.GUIDToAssetPath).Distinct()
            .SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<Sprite>()).OrderBy(x => x.name).ToArray();

        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Resources/LevelCatalog.asset");
        if (catalog != null)
        {
            s.levelTags = new int[catalog.Count];
            s.levelDifficulty = new int[catalog.Count];
            for (int i = 0; i < catalog.Count; i++)
            {
                GameObject p = catalog.levels[i];
                s.levelDifficulty[i] = catalog.GetDifficulty(i);
                s.levelTags[i] = p == null ? 0 : (int)TagsOf(p, catalog.GetDifficulty(i));
            }
        }
        EditorUtility.SetDirty(s);
        // Вид и характер Очага
        const string hvPath = "Assets/Resources/HearthVisualSettings.asset";
        var hv = AssetDatabase.LoadAssetAtPath<HearthVisualSettings>(hvPath);
        if (hv == null) { hv = ScriptableObject.CreateInstance<HearthVisualSettings>(); HearthVisualSettings.FillLines(hv); AssetDatabase.CreateAsset(hv, hvPath); }
        hv.tapSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/knocked-on-a-wooden-surface-once.mp3");
        hv.feedSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/burn1.mp3");
        hv.crackleSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/burn4.mp3");
        hv.rewardSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/light-a-match-the-second-time_9CtFcAvh.mp3");
        EditorUtility.SetDirty(hv);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Polish] Hearth visual: lines {hv.flameLines.Count}, sounds {(hv.tapSound != null)}/{(hv.feedSound != null)}/{(hv.crackleSound != null)}/{(hv.rewardSound != null)}");
        Debug.Log($"[Polish] Retention settings: font {(s.font != null)}, card {(s.card != null)}, art {s.art.Length}, level tags [{string.Join(",", s.levelTags)}]");
    }

    public static RetTags TagsOf(GameObject root, int difficulty)
    {
        RetTags t = RetTags.None;
        if (root.GetComponentInChildren<WaterDripper>(true) != null) t |= RetTags.Water;
        if (root.GetComponentInChildren<Fan>(true) != null) t |= RetTags.Wind;
        if (root.GetComponentInChildren<Oscillator>(true) != null) t |= RetTags.Pendulum;
        foreach (MatchLogic m in root.GetComponentsInChildren<MatchLogic>(true))
        {
            if (m.ignorePlayerMatch) t |= RetTags.Immune;
            if (m.isGolden) t |= RetTags.Gold;
        }
        if (difficulty >= 3) t |= RetTags.Hard;
        return t;
    }

    private static Sprite Sprite(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();

    // ------------------------------------------------------------------ тесты

    private class FakeWallet : IRetWallet
    {
        public int coins;
        public int Coins => coins;
        public void AddCoins(int a) { coins += a; }
        public bool SpendCoins(int a) { if (coins < a) return false; coins -= a; return true; }
    }

    private static RetentionSettings Cfg()
    {
        var s = AssetDatabase.LoadAssetAtPath<RetentionSettings>(SettingsPath);
        if (s == null) { s = ScriptableObject.CreateInstance<RetentionSettings>(); RetentionDefaults.Fill(s); }
        return s;
    }

    private static RetentionCore NewCore(RetentionSettings cfg, out FakeWallet w)
    {
        w = new FakeWallet();
        var core = new RetentionCore(cfg, RetSave.Validate(new RetState(), cfg), w);
        core.seasonal = LoadSeasonal();
        return core;
    }

    private static List<RetSeasonal> LoadSeasonal()
    {
        var l = JsonUtility.FromJson<RetSeasonalList>(File.ReadAllText("Assets/Resources/Retention/events.json"));
        return l.events.ToList();
    }

    private static RetLevelResult Win(int stars = 3, bool daily = false, RetTags tags = RetTags.None) =>
        new RetLevelResult { won = true, stars = stars, burnt = 12, total = 12, daily = daily, levelIndex = daily ? -1 : 5, tags = tags, seconds = 9f };

    [MenuItem("Tools/Match & Flame/Retention/Tests (time, saves)")]
    public static void Tests()
    {
        RetentionSettings cfg = Cfg();
        var sb = new StringBuilder("# Очаг: защита времени и сохранения\n\n| Случай | Ожидание | Результат | |\n|---|---|---|---|\n");
        int fails = 0;
        void Row(string name, string expect, string got, bool ok) { sb.AppendLine($"| {name} | {expect} | {got} | {(ok ? "OK" : "ОШИБКА")} |"); if (!ok) fails++; }

        int d0 = DayClock.DayNumber(new DateTime(2026, 10, 12));   // понедельник

        // 1. Откат даты назад
        {
            var c = NewCore(cfg, out _);
            c.Advance(d0); c.ClaimGift();
            int e = c.s.embers;
            bool nd = c.Advance(d0 - 1);
            bool gift = c.CanClaimGift;
            c.ClaimGift();
            bool back = c.Advance(d0);
            bool next = c.Advance(d0 + 1);
            Row("Часы назад на 1 день", "нового дня нет, подарок не повторяется; вперёд — обычный новый день",
                $"новый день {nd}, подарок доступен {gift}, угольков {c.s.embers - e}, снова сегодня {back}, завтра {next}", !nd && !gift && c.s.embers == e && !back && next);
        }
        // 2. Скачок на год вперёд
        {
            var c = NewCore(cfg, out _);
            c.Advance(d0); c.OnLevelEnd(Win()); c.ClaimGift();
            for (int k = 1; k <= 5; k++) { c.Advance(d0 + k); c.OnLevelEnd(Win()); }
            int shields = c.s.shields, best = c.s.bestStreak;
            int before = c.s.embers;
            bool nd = c.Advance(d0 + 5 + 365);
            c.ClaimGift();
            int gained = c.s.embers - before;
            bool again = c.CanClaimGift;
            Row("Скачок вперёд на 365 дней", "один новый день и один подарок; серия заново, лучшая сохранена; щитов +≤1",
                $"новый день {nd}, подарок +{gained}, ещё подарок {again}, серия {c.s.streak}, лучшая {c.s.bestStreak}, щиты {shields}→{c.s.shields}",
                nd && !again && gained == cfg.giftEmbers && c.s.streak == 0 && c.s.bestStreak == best && c.s.shields <= Math.Min(cfg.shieldMax, shields + 1));
        }
        // 3. Смена часового пояса (−1 и +1 день)
        {
            var c = NewCore(cfg, out _);
            c.Advance(d0); c.OnLevelEnd(Win());
            int st = c.s.streak;
            bool minus = c.Advance(d0 - 1);
            bool same = c.Advance(d0);
            bool plus = c.Advance(d0 + 1);
            c.OnLevelEnd(Win());
            Row("Часовой пояс −1 день, затем +1", "−1: без нового дня и без потерь; +1: обычный день, серия растёт",
                $"−1 новый день {minus}, обратно {same}, +1 новый день {plus}, серия {st}→{c.s.streak}", !minus && !same && plus && c.s.streak == st + 1);
        }
        // 4. 23:59 → 00:00 и перевод часов (летнее время)
        {
            int a = DayClock.DayNumber(new DateTime(2026, 3, 28, 23, 59, 0)), b = DayClock.DayNumber(new DateTime(2026, 3, 29, 0, 0, 30));
            int dst1 = DayClock.DayNumber(new DateTime(2026, 3, 29, 1, 59, 0)), dst2 = DayClock.DayNumber(new DateTime(2026, 3, 29, 3, 1, 0));
            var c = NewCore(cfg, out _);
            c.Advance(a);
            bool n1 = c.Advance(b), n2 = c.Advance(dst1), n3 = c.Advance(dst2);
            Row("23:59 → 00:00; перевод часов 02:00 → 03:00", "полночь — ровно один новый день; перевод часов — тот же день",
                $"полночь: новый день {n1} (дни {a}→{b}); после перевода: {n2}/{n3}", b == a + 1 && n1 && !n2 && !n3 && dst1 == dst2);
        }
        // 5. Однажды убежавшие вперёд часы: дата «прижилась» через 2 дня
        {
            var c = NewCore(cfg, out _);
            c.Advance(d0); c.Advance(d0 + 30);   // часы ушли вперёд на месяц
            bool a1 = c.Advance(d0 + 1), a2 = c.Advance(d0 + 2), a3 = c.Advance(d0 + 3);
            Row("Часы были на месяц вперёд, вернули назад", "первые 2 дня без нового дня, потом игра идёт по настоящей дате",
                $"{a1}/{a2}/{a3}, действующий день {c.Today - d0}", !a1 && !a2 && a3 && c.Today == d0 + 3);
        }
        // 6. Пропуск со щитом и без
        {
            var c = NewCore(cfg, out _);
            for (int k = 0; k < 5; k++) { c.Advance(d0 + k); c.OnLevelEnd(Win()); }
            int st = c.s.streak, sh = c.s.shields;
            c.Advance(d0 + 6);   // пропущен 1 день
            int afterShield = c.s.streak, shAfter = c.s.shields;
            c.OnLevelEnd(Win());
            int cont = c.s.streak;
            c.Advance(d0 + 9);   // пропущено 2 дня, щитов меньше
            int halved = c.s.streak; bool canRestore = c.CanRestoreStreak;
            c.RestoreStreak();
            Row("Пропуск 1 дня со щитом; потом 2 дня без щитов", "щит закрывает день; без щита серия делится пополам, можно вернуть",
                $"серия {st}, щиты {sh}→{shAfter}, после щита {afterShield}, победа {cont}; пропуск 2 дн.: {halved}, вернуть {canRestore} → {c.s.streak}",
                afterShield == st && shAfter == sh - 1 && cont == st + 1 && halved == cont / 2 && canRestore && c.s.streak == cont);
        }
        // 7. Награды раз в день
        {
            var c = NewCore(cfg, out FakeWallet w);
            c.Advance(d0); c.ClaimGift(); c.ClaimGift();
            c.OnLevelEnd(Win(3, true)); int c1 = w.coins;
            c.events.Clear();
            c.OnLevelEnd(Win(3, true));
            int dailyEvents = c.events.Count(e => e.kind == RetEventKind.DailyWin || e.kind == RetEventKind.Daily3Stars || e.kind == RetEventKind.Gift);
            Row("Подарок и уровень дня дважды за день", "второй раз награды уровня дня и подарка нет",
                $"событий награды {dailyEvents}, монеты +{w.coins - c1}", dailyEvents == 0 && w.coins == c1);
        }
        // 8. Повреждённые и старые сохранения
        {
            var store = new MemoryStore();
            string[] bad = { "{garbage", "null", "{\"streak\":-50,\"embers\":-3,\"stage\":99,\"questIds\":[999],\"questProgress\":[],\"boxCount\":[1,2]}", "{\"version\":99,\"streak\":3}" };
            foreach (string json in bad)
            {
                store.SetString(RetSave.StateKey, json);
                bool ok = true; string got;
                try
                {
                    RetState st = RetSave.Load(store, cfg, out string problem);
                    var c = new RetentionCore(cfg, st, new FakeWallet());
                    c.Advance(d0); c.ClaimGift(); c.OnLevelEnd(Win()); c.Feed(10);
                    got = $"streak {st.streak}, embers {st.embers}, stage {st.stage}, quests {st.questIds.Length}, boxes {st.boxCount.Length}, проблема: {problem ?? "нет"}";
                    ok = st.streak >= 0 && st.embers >= 0 && st.stage < cfg.stages.Count && st.boxCount.Length == cfg.boxes.Count;
                }
                catch (Exception e) { got = "исключение " + e.GetType().Name; ok = false; }
                Row($"Сохранение `{json.Replace("|", "/").Substring(0, Math.Min(40, json.Length))}`", "не падает, значения в пределах", got, ok);
            }
            // Старый игрок: ключей Ret_ нет, старые ключи не трогаются
            store.data.Clear();
            store.SetInt("GoldenMatchesCurrency", 17);
            RetState fresh = RetSave.Load(store, cfg, out _);
            RetSave.Write(store, fresh);
            Row("Обновление поверх старой версии (нет Ret_)", "чистое состояние; старые ключи без изменений",
                $"монеты {store.GetInt("GoldenMatchesCurrency", 0)}, ключи Ret_: {string.Join(",", store.data.Keys.Where(k => k.StartsWith("Ret_")))}",
                store.GetInt("GoldenMatchesCurrency", 0) == 17 && store.data.Keys.Where(k => !k.StartsWith("Ret_")).SequenceEqual(new[] { "GoldenMatchesCurrency" }));
        }
        // 9. Пул уровней дня: детерминированность и повторы
        {
            DailyLevelPool pool = AssetDatabase.LoadAssetAtPath<DailyLevelPool>("Assets/Resources/DailyLevelPool.asset");
            if (pool != null && pool.entries.Count > 0)
            {
                int repeatsInCycle = 0, consecutive = 0, prev = -1;
                var seenTier = new Dictionary<int, HashSet<int>>();
                for (int d = d0; d < d0 + 365; d++)
                {
                    int i = pool.IndexFor(d, cfg.dailySeed);
                    if (i == prev) consecutive++;
                    prev = i;
                }
                // без повторов внутри полного цикла каждого уровня сложности: дни с номером k = c·n … c·n+n−1
                for (int tier = 0; tier < 4; tier++)
                {
                    int n = pool.entries.Count(e => e.enabled && e.tier == tier);
                    int perWeek = tier == 3 ? 1 : 2, firstWd = tier * 2;
                    int c0 = DayClock.Week(d0) * perWeek / n;
                    for (int c = c0; c < c0 + 6; c++)
                    {
                        var seen = new HashSet<int>();
                        for (int k = c * n; k < c * n + n; k++)
                        {
                            int day = DayClock.WeekStart(k / perWeek) + firstWd + k % perWeek;
                            if (!seen.Add(pool.IndexFor(day, cfg.dailySeed))) repeatsInCycle++;
                        }
                    }
                }
                Row("Уровень дня на год вперёд", "один день — один уровень у всех; без повторов подряд и внутри цикла",
                    $"повторов подряд {consecutive}, повторов в цикле {repeatsInCycle}, пул {pool.entries.Count}", consecutive == 0 && repeatsInCycle == 0);
            }
            else Row("Уровень дня", "пул собран", "пула ещё нет", true);
        }
        // 10. Праздники: включаются и выключаются по датам (включая Новый год через смену года)
        {
            var c = NewCore(cfg, out _);
            (string date, string expect)[] cases = { ("2026-10-24", ""), ("2026-10-25", "halloween"), ("2026-11-02", "halloween"), ("2026-11-03", ""),
                ("2026-12-24", ""), ("2026-12-25", "newyear"), ("2027-01-07", "newyear"), ("2027-01-08", ""), ("2027-03-21", "nauryz"), ("2027-03-24", "") };
            var got = new List<string>(); bool ok = true;
            foreach (var (date, expect) in cases)
            {
                int d = DayClock.DayNumber(DateTime.Parse(date));
                RetSeasonal ev = c.ActiveSeasonal(d, out string key);
                string id = ev != null ? ev.id : "";
                got.Add($"{date}: {(id == "" ? "—" : id)}");
                if (id != expect) ok = false;
            }
            Row("Праздники по датам", "начало и конец включительно, Новый год через смену года", string.Join(", ", got), ok);
        }
        // 11. Замена задания и этапы недели
        {
            var c = NewCore(cfg, out _);
            c.Advance(d0);
            int before = c.s.questIds.Length > 0 ? c.s.questIds[0] : -1;
            bool r1 = c.RerollQuest(0), r2 = c.RerollQuest(1);
            int e0 = c.s.embers;
            for (int k = 0; k < 8; k++) c.OnLevelEnd(Win(2, false, RetTags.None));
            Row("Замена задания раз в день; этапы недели 4/8", "первая замена есть, вторая нет; за 8 побед — этапы 4 и 8",
                $"замена {r1}/{r2} (было {before} → {c.s.questIds[0]}), этапы {Convert.ToString(c.s.weekStepsClaimed, 2)}", r1 && !r2 && before != c.s.questIds[0] && c.s.weekStepsClaimed == 3);
        }
        // 12. Миграция сохранения 1 → 2: бейджи не загораются разом, знакомство не повторяется
        {
            var store = new MemoryStore();
            store.SetString(RetSave.StateKey, "{\"version\":1,\"lastDay\":9000,\"maxDay\":9000,\"boxCount\":[1,0,2],\"outfitOwned\":[0,3],\"questDay\":9000}");
            store.SetInt(RetSave.VersionKey, 1);
            RetState st = RetSave.Load(store, cfg, out string problem);
            Row("Миграция сохранения v1 → v2", "увиденные коробки/облики = имеющиеся, знакомство пройдено",
                $"boxesSeen {st.boxesSeen}, outfitsSeen {st.outfitsSeen}, introDone {st.introDone}, версия {st.version}", st.boxesSeen == 2 && st.outfitsSeen == 2 && st.introDone && st.version == RetState.CurrentVersion);
        }
        sb.AppendLine($"\n**Итого ошибок: {fails}**");
        Directory.CreateDirectory("Builds");
        File.WriteAllText("Builds/retention-tests.md", sb.ToString());
        Debug.Log($"[Polish] Retention tests: {fails} failures\n" + sb);
    }

    // ------------------------------------------------------------------ симуляция экономики

    private class Profile
    {
        public string name;
        public Func<int, bool> plays;     // играет ли в день k (0..59)
        public int regular;               // обычных побед за день
        public bool daily, quests;
    }

    [MenuItem("Tools/Match & Flame/Retention/Simulate 60 days")]
    public static void Simulate()
    {
        RetentionSettings cfg = Cfg();
        var profiles = new[]
        {
            new Profile { name = "каждый день (всё)", plays = k => true, regular = 3, daily = true, quests = true },
            new Profile { name = "каждый день (подарок + уровень дня)", plays = k => true, regular = 0, daily = true },
            new Profile { name = "раз в 3 дня", plays = k => k % 3 == 0, regular = 3, daily = true, quests = true },
            new Profile { name = "только выходные (сб, вс)", plays = k => k % 7 == 5 || k % 7 == 6, regular = 4, daily = true, quests = true },
            new Profile { name = "пропуск недели (дни 15–21)", plays = k => k < 14 || k > 20, regular = 3, daily = true, quests = true },
            new Profile { name = "вернулся через месяц (1–10, 41–60)", plays = k => k < 10 || k >= 40, regular = 3, daily = true, quests = true },
        };
        int[] marks = { 1, 3, 7, 14, 30, 60 };
        var sb = new StringBuilder("# Очаг: симуляция 60 дней\n\nСтарт — 2026-10-12 (понедельник), монеты с уровней не учитываются (только новые). Игрок подкладывает в очаг половину угольков (вторую копит на декор за угольки, пока он не куплен весь), покупает доступный декор, открывает секретный коробок, когда можно.\n\n");
        var traps = new List<string>();
        int d0 = DayClock.DayNumber(new DateTime(2026, 10, 12));
        RetTags[] levelTags = { RetTags.None, RetTags.Water, RetTags.Wind | RetTags.Water, RetTags.Pendulum, RetTags.Immune | RetTags.Gold, RetTags.Gold, RetTags.Hard | RetTags.Wind };
        foreach (Profile p in profiles)
        {
            var c = NewCore(cfg, out FakeWallet w);
            sb.AppendLine($"## {p.name}\n\n| День | Монет получено | Угольков получено | Тепло | Стадия | Декор | Облики | Коробки | Серия (лучшая) | Щиты |\n|---|---|---|---|---|---|---|---|---|---|");
            int prevStreak = 0;
            for (int k = 0; k < 60; k++)
            {
                int day = d0 + k;
                if (p.plays(k))
                {
                    c.Advance(day);
                    int savedBefore = c.s.embers;
                    if (c.CanClaimGift) c.ClaimGift();
                    if (p.daily) c.OnLevelEnd(Win(k % 3 == 2 ? 2 : 3, true, levelTags[k % levelTags.Length]));
                    for (int r = 0; r < p.regular; r++)
                    {
                        RetTags t = levelTags[(k * 3 + r) % levelTags.Length];
                        c.OnLevelEnd(new RetLevelResult { won = true, stars = r == 2 ? 2 : 3, burnt = 10 + r * 3, total = 14, golds = (t & RetTags.Gold) != 0 ? 1 : 0, tags = t, restarted = r == 1, seconds = 8f + r * 6f, levelIndex = (k + r) % 32 });
                    }
                    // Половина угольков — в очаг, половина копится на декор (пока он не куплен весь)
                    bool wantsDecor = Enumerable.Range(0, cfg.decor.Count).Any(i => cfg.decor[i].priceEmbers > 0 && !c.s.HasDecor(i));
                    c.Feed(wantsDecor ? (c.s.embers - savedBefore) / 2 : c.s.embers);
                    for (int i = 0; i < cfg.decor.Count; i++) { RetDecor dd = cfg.decor[i]; if (!c.s.HasDecor(i) && dd.priceEmbers > 0 && c.s.embers >= dd.priceEmbers) c.BuyDecor(i); }
                    for (int i = 0; i < cfg.decor.Count; i++) { RetDecor dd = cfg.decor[i]; if (!c.s.HasDecor(i) && dd.priceCoins > 0 && w.coins >= dd.priceCoins) c.BuyDecor(i); }
                    if (c.SecretReady) c.OpenSecret();
                    // ловушки
                    if (c.s.embers < 0 || w.coins < 0) traps.Add($"{p.name}: отрицательный ресурс в день {k + 1}");
                    if (c.s.streak < prevStreak / 2 && k > 0 && p.plays(k - 1)) traps.Add($"{p.name}: серия упала без пропуска в день {k + 1}");
                    prevStreak = c.s.streak;
                }
                if (marks.Contains(k + 1))
                {
                    int decor = Enumerable.Range(0, cfg.decor.Count).Count(c.s.HasDecor);
                    sb.AppendLine($"| {k + 1} | {c.s.coinsEarned} | {c.s.embersEarned} | {c.s.warmth} | {c.s.stage + 1} {cfg.stages[c.s.stage].nameRU} | {decor}/{cfg.decor.Count} | {c.s.outfitOwned.Length} | {c.BoxesOwned}/{cfg.boxes.Count} | {c.s.streak} ({c.s.bestStreak}) | {c.s.shields} |");
                }
                c.events.Clear();
            }
            sb.AppendLine();
        }
        sb.AppendLine(traps.Count == 0 ? "**Ловушек не найдено** (отрицательных ресурсов, падения серии без пропуска)." : "**Ловушки:**\n- " + string.Join("\n- ", traps));
        Directory.CreateDirectory("Builds");
        File.WriteAllText("Builds/retention-sim.md", sb.ToString());
        Debug.Log("[Polish] Retention simulate\n" + sb);
    }
}
