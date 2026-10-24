using System;
using System.Collections.Generic;
using UnityEngine;

public interface IRetWallet
{
    int Coins { get; }
    void AddCoins(int amount);
    bool SpendCoins(int amount);
}

public enum RetEventKind
{
    NewDay, Welcome, StreakUp, StreakHalved, StreakReset, StreakRestored, ShieldUsed, ShieldGranted, Milestone, Gift,
    DailyWin, Daily3Stars, DailyDoubled, QuestDone, QuestsAll, WeekGoal, WeekStep, WeekEvent, StageUp, Box, SetDone, SecretReady,
    SecretOpened, SeasonReward, Outfit, Decor, Embers, ClockBack
}

/// <summary>Что произошло (для окошек и всплывашек). a/b — числа по смыслу события.</summary>
public struct RetEvent
{
    public RetEventKind kind;
    public int a, b;
    public RetEvent(RetEventKind k, int a = 0, int b = 0) { kind = k; this.a = a; this.b = b; }
    public override string ToString() => $"{kind}({a},{b})";
}

/// <summary>Результат броска коробка: индекс, новый или дубликат, редкость.</summary>
public struct RetBoxRoll
{
    public int box;
    public bool duplicate;
    public BoxRarity rarity;
    public int embers;
}

/// <summary>
/// Логика «Очага» без сцен и MonoBehaviour: время, серия и щиты, подарок, уровень дня, задания, неделя, стадии, декор,
/// облики, коробки, секретный коробок, праздники. Один календарный день — одна награда каждого вида.
/// Используется игрой (Retention) и редакторской симуляцией 60 дней.
/// </summary>
public class RetentionCore
{
    public readonly RetentionSettings cfg;
    public readonly RetState s;
    private readonly IRetWallet wallet;
    public readonly List<RetEvent> events = new List<RetEvent>();
    public List<RetSeasonal> seasonal = new List<RetSeasonal>();

    /// <summary>Механики, доступные игроку сейчас (открытые уровни + уровень дня) — чтобы задания были выполнимы.</summary>
    public Func<RetTags> availableTags = () => (RetTags)~0;
    public bool dailyAvailable = true;

    /// <summary>Действующий день: при переводе часов назад остаётся последним засчитанным.</summary>
    public int Today { get; private set; } = -1;
    public bool ClockRolledBack { get; private set; }

    public RetentionCore(RetentionSettings cfg, RetState state, IRetWallet wallet)
    {
        this.cfg = cfg; s = state; this.wallet = wallet;
        Today = s.lastDay;
    }

    // ------------------------------------------------------------------ время

    /// <summary>Пришла дата устройства. Возвращает true, если наступил новый день.</summary>
    public bool Advance(int today)
    {
        if (today < 0) return false;   // дата до 2000 года — часы сбиты, ждём нормальную дату (R-14)
        if (s.lastDay < 0)
        {
            s.firstDay = s.lastDay = s.maxDay = today;
            Today = today;
            OnWeek(today, true);
            NewDayChores(today);
            events.Add(new RetEvent(RetEventKind.NewDay, today));
            return true;
        }
        if (today < s.maxDay)
        {
            // Часы переведены назад: новый день не наступает. Если дата «прижилась» (идёт вперёд два дня подряд от новой точки),
            // принимаем её как настоящую — не замораживаем игру навсегда из-за однажды убежавших вперёд часов.
            if (s.rollbackFirstDay < 0 || s.rollbackFirstDay > today) s.rollbackFirstDay = today;
            if (today - s.rollbackFirstDay < 2)
            {
                if (!ClockRolledBack) events.Add(new RetEvent(RetEventKind.ClockBack, s.maxDay - today));
                ClockRolledBack = true;
                Today = s.lastDay;
                return false;
            }
            s.lastDay = s.maxDay = today - 1;
            s.lastCountedDay = Math.Min(s.lastCountedDay, today - 1);
            if (s.giftDay > today) s.giftDay = -1;
        }
        ClockRolledBack = false;
        s.rollbackFirstDay = -1;
        Today = today;
        if (today == s.lastDay) return false;
        s.lastDay = s.maxDay = today;
        s.lastUtcTicks = DateTime.UtcNow.Ticks;
        OnWeek(today, false);
        CloseStreakGap(today);
        NewDayChores(today);
        events.Add(new RetEvent(RetEventKind.NewDay, today));
        return true;
    }

    private void OnWeek(int today, bool first)
    {
        int w = DayClock.Week(today);
        if (s.week == w) return;
        if (s.shieldWeek != w)
        {
            int before = s.shields;
            s.shields = Math.Min(cfg.shieldMax, s.shields + 1);   // один щит в неделю, сколько бы недель ни прошло
            s.shieldWeek = w;
            if (!first && s.shields > before) events.Add(new RetEvent(RetEventKind.ShieldGranted, s.shields));
        }
        s.week = w;
        s.weekWins = 0; s.weekGoalClaimed = false; s.weekStepsClaimed = 0;
        s.weekTheme = PickTheme(w);
        s.weekEventProgress = 0; s.weekFastWins = 0; s.weekEventClaimed = false;
        if (cfg.secretBox && s.secretOpened)
        {
            s.secretOpened = false;
            s.secretEarned = false;
            s.secretWeek = w;
            s.secretOpenDay = DayClock.WeekStart(w) + 6;   // воскресенье
        }
    }

    private void CloseStreakGap(int today)
    {
        if (!cfg.streak || s.streak <= 0 || s.lastCountedDay < 0) return;
        int missed = today - 1 - s.lastCountedDay;
        if (missed <= 0) return;
        if (missed > cfg.longBreakDays)
        {
            s.streak = 0; s.milestoneIndex = 0; s.welcomeDay = today;
            events.Add(new RetEvent(RetEventKind.Welcome, missed));
            events.Add(new RetEvent(RetEventKind.StreakReset));
            return;
        }
        int cover = Math.Min(s.shields, missed);
        for (int k = 0; k < cover; k++) SetHistory(s.lastCountedDay + 1 + k, 4);
        s.shields -= cover;
        if (cover > 0) events.Add(new RetEvent(RetEventKind.ShieldUsed, cover, s.shields));
        if (missed > cover)
        {
            s.restoreValue = s.streak;
            s.restoreDay = today;
            s.streak /= 2;
            s.welcomeDay = today;
            events.Add(new RetEvent(RetEventKind.StreakHalved, s.restoreValue, s.streak));
        }
        s.lastCountedDay = today - 1;   // разрыв закрыт: победа сегодня продолжает серию
    }

    private void NewDayChores(int today)
    {
        s.regularWinsDay = today; s.regularWinsToday = 0;
        s.winsTodayLevels = new int[0];
        s.dailyStarsToday = 0;
        GenerateQuests(today);
        TrimHistory(today);
        RetSeasonal ev = ActiveSeasonal(today, out string key);
        if (ev != null && s.eventKey != key) { s.eventKey = key; s.eventDailyCount = 0; }
        if (SecretReady) events.Add(new RetEvent(RetEventKind.SecretReady));
    }

    // ------------------------------------------------------------------ серия

    public bool CanRestoreStreak => cfg.streak && s.restoreDay == Today && s.restoreValue > s.streak;

    /// <summary>«Вернуть серию» (после добровольного rewarded-ролика).</summary>
    public void RestoreStreak() => RestoreStreak(Today);

    /// <summary>Вернуть серию за день, когда игрок нажал кнопку (день мог смениться, пока шёл ролик).</summary>
    public void RestoreStreak(int forDay)
    {
        if (!cfg.streak || s.restoreDay != forDay || s.restoreValue <= s.streak) return;
        s.streak = s.restoreValue + (s.lastCountedDay == Today ? 1 : 0);
        s.bestStreak = Math.Max(s.bestStreak, s.streak);
        s.restoreDay = -1;
        events.Add(new RetEvent(RetEventKind.StreakRestored, s.streak));
    }

    private void CountDay()
    {
        if (!cfg.streak || s.lastCountedDay == Today) return;
        s.streak = s.lastCountedDay == Today - 1 ? s.streak + 1 : 1;
        s.lastCountedDay = Today;
        s.bestStreak = Math.Max(s.bestStreak, s.streak);
        events.Add(new RetEvent(RetEventKind.StreakUp, s.streak));
        if (s.streak >= cfg.secretStreak) s.secretEarned = true;
        while (s.milestoneIndex < cfg.streakMilestones.Length && s.streak >= cfg.streakMilestones[s.milestoneIndex])
        {
            int e = s.milestoneIndex < cfg.milestoneEmbers.Length ? cfg.milestoneEmbers[s.milestoneIndex] : 0;
            AddEmbers(e);
            events.Add(new RetEvent(RetEventKind.Milestone, cfg.streakMilestones[s.milestoneIndex], e));
            if (cfg.collection) RollBox();
            s.milestoneIndex++;
        }
        // После последней вехи — каждые 30 дней
        int lastM = cfg.streakMilestones.Length > 0 ? cfg.streakMilestones[cfg.streakMilestones.Length - 1] : 30;
        if (s.milestoneIndex >= cfg.streakMilestones.Length && s.streak > lastM && (s.streak - lastM) % 30 == 0)
        {
            int e = cfg.milestoneEmbers.Length > 0 ? cfg.milestoneEmbers[cfg.milestoneEmbers.Length - 1] : 200;
            AddEmbers(e);
            events.Add(new RetEvent(RetEventKind.Milestone, s.streak, e));
            if (cfg.collection) RollBox();
        }
    }

    public float Glow => Mathf.Min(cfg.glowMax, s.streak * cfg.glowPerStreakDay);

    // ------------------------------------------------------------------ подарок дня

    public bool CanClaimGift => cfg.systemEnabled && cfg.dailyGift && Today >= 0 && s.giftDay != Today;
    public int GiftEmbers => cfg.giftEmbers + Math.Min(cfg.giftStreakBonusMax, s.streak * cfg.giftStreakBonusPerDay);
    public int GiftCoins => cfg.giftCoinEvery > 0 && s.streak > 0 && s.streak % cfg.giftCoinEvery == 0 ? 1 : 0;

    public void ClaimGift()
    {
        if (!CanClaimGift) return;
        int e = GiftEmbers, c = GiftCoins;
        s.giftDay = Today;
        AddEmbers(e);
        if (c > 0) AddCoins(c);
        s.firstRewardGiven = true;
        SetHistory(Today, 8);
        events.Add(new RetEvent(RetEventKind.Gift, e, c));
    }

    // ------------------------------------------------------------------ итог уровня

    public void OnLevelEnd(RetLevelResult r)
    {
        if (!cfg.systemEnabled || Today < 0) return;
        if (r.won)
        {
            CountDay();
            SetHistory(Today, 1);
            if (!r.daily && s.regularWinsToday < cfg.regularWinCap) { s.regularWinsToday++; AddEmbers(cfg.regularWinEmbers); }
            if (Array.IndexOf(s.winsTodayLevels, r.levelIndex + (r.daily ? 10000 : 0)) < 0)
            {
                Array.Resize(ref s.winsTodayLevels, s.winsTodayLevels.Length + 1);
                s.winsTodayLevels[s.winsTodayLevels.Length - 1] = r.levelIndex + (r.daily ? 10000 : 0);
            }
            if (r.daily) DailyWin(r);
            s.weekWins++;
            for (int k = 0; cfg.weeklyGoal && k < cfg.weekSteps.Length; k++)
                if ((s.weekStepsClaimed & (1 << k)) == 0 && s.weekWins >= cfg.weekSteps[k])
                {
                    s.weekStepsClaimed |= 1 << k;
                    int e = k < cfg.weekStepEmbers.Length ? cfg.weekStepEmbers[k] : 0;
                    AddEmbers(e);
                    events.Add(new RetEvent(RetEventKind.WeekStep, cfg.weekSteps[k], e));
                }
            if (cfg.weeklyGoal && !s.weekGoalClaimed && s.weekWins >= cfg.weeklyGoalWins)
            {
                s.weekGoalClaimed = true;
                AddEmbers(cfg.weeklyGoalEmbers);
                AddCoins(cfg.weeklyGoalCoins);
                s.secretEarned = true;
                events.Add(new RetEvent(RetEventKind.WeekGoal, cfg.weeklyGoalEmbers, cfg.weeklyGoalCoins));
                if (cfg.collection) RollBox();
            }
        }
        if (cfg.quests) UpdateQuests(r, 0);
        if (cfg.weeklyEvent) UpdateTheme(r);
    }

    private void DailyWin(RetLevelResult r)
    {
        if (!cfg.dailyLevel) return;
        s.dailyStarsToday = Math.Max(s.dailyStarsToday, r.stars);
        if (s.dailyWinDay != Today)
        {
            s.dailyWinDay = Today;
            AddEmbers(cfg.dailyWinEmbers);
            AddCoins(cfg.dailyWinCoins);
            if ((r.tags & (RetTags.Water | RetTags.Wind | RetTags.Pendulum)) != 0) AddAroma(RetFuel.Pine, cfg.dailyAroma);
            if ((r.tags & RetTags.Gold) != 0) AddAroma(RetFuel.Oak, cfg.dailyAroma);
            if (r.burnt >= 10) AddAroma(RetFuel.Cherry, cfg.dailyAroma);
            events.Add(new RetEvent(RetEventKind.DailyWin, cfg.dailyWinEmbers, cfg.dailyWinCoins));
            SeasonalDaily();
        }
        if (r.stars >= 3 && s.daily3Day != Today)
        {
            s.daily3Day = Today;
            AddEmbers(cfg.daily3StarEmbers);
            AddCoins(cfg.daily3StarCoins);
            AddAroma(RetFuel.Oak, cfg.dailyAroma);
            SetHistory(Today, 2);
            events.Add(new RetEvent(RetEventKind.Daily3Stars, cfg.daily3StarEmbers, cfg.daily3StarCoins));
        }
    }

    public bool DailyDoneToday => s.dailyWinDay == Today;
    public bool CanDoubleDaily => DailyDoneToday && s.dailyDoubleDay != Today;

    /// <summary>«Удвоить» угольки уровня дня (добровольный rewarded-ролик, раз в день).</summary>
    public void DoubleDaily() => DoubleDaily(Today);

    /// <summary>«Удвоить» за день, когда игрок нажал кнопку (день мог смениться, пока шёл ролик).</summary>
    public void DoubleDaily(int forDay)
    {
        if (s.dailyWinDay != forDay || s.dailyDoubleDay == forDay) return;
        int e = cfg.dailyWinEmbers + (s.daily3Day == forDay ? cfg.daily3StarEmbers : 0);
        s.dailyDoubleDay = forDay;
        AddEmbers(e);
        events.Add(new RetEvent(RetEventKind.DailyDoubled, e));
    }

    // ------------------------------------------------------------------ задания

    public bool Feasible(QuestKind k)
    {
        RetTags t = availableTags != null ? availableTags() : (RetTags)~0;
        switch (k)
        {
            case QuestKind.WinWater: return (t & RetTags.Water) != 0;
            case QuestKind.WinWind: return (t & RetTags.Wind) != 0;
            case QuestKind.WinPendulum: return (t & RetTags.Pendulum) != 0;
            case QuestKind.WinImmune: return (t & RetTags.Immune) != 0;
            case QuestKind.BurnGold: return (t & RetTags.Gold) != 0;
            case QuestKind.WinHard: return (t & RetTags.Hard) != 0;
            case QuestKind.PlayDaily: case QuestKind.Daily3Stars: return cfg.dailyLevel && dailyAvailable;
            case QuestKind.FeedHearth: return cfg.hearth;
            default: return true;
        }
    }

    private void GenerateQuests(int day)
    {
        if (s.questDay == day) return;
        s.questDay = day;
        s.questAllClaimed = false;
        var picked = new List<int>();
        var kinds = new List<QuestKind>();
        var rng = new RetRng(RetRng.Mix(day, 0x5157));
        var pool = new List<int>();
        for (int i = 0; i < cfg.questPool.Count; i++) if (cfg.questPool[i].enabled && Feasible(cfg.questPool[i].kind)) pool.Add(i);
        int fuelMask = 0;
        for (int tries = 0; tries < 64 && picked.Count < 3 && pool.Count > 0; tries++)
        {
            int i = pool[rng.Range(pool.Count)];
            RetQuest q = cfg.questPool[i];
            if (picked.Contains(i) || kinds.Contains(q.kind)) continue;
            if (tries < 32 && picked.Count < 2 && (fuelMask & (1 << (int)q.fuel)) != 0) continue;   // разные «дрова», если получится
            picked.Add(i); kinds.Add(q.kind); fuelMask |= 1 << (int)q.fuel;
        }
        s.questIds = picked.ToArray();
        s.questProgress = new int[picked.Count];
        s.questClaimed = new int[picked.Count];
    }

    public static int Needed(RetQuest q) => q.kind == QuestKind.WinFast ? 1 : q.target;

    public bool CanReroll(int i) => cfg.quests && i >= 0 && i < s.questIds.Length && s.questClaimed[i] == 0
                                    && (s.rerollDay != Today ? 0 : s.rerollsUsed) < cfg.questRerollsPerDay;

    /// <summary>Заменить невыполненное задание (бесплатно, раз в день): новое — другого вида, выполнимое.</summary>
    public bool RerollQuest(int i)
    {
        if (!CanReroll(i)) return false;
        var rng = new RetRng(RetRng.Mix(Today, 0x7123 + i + s.rerollsUsed * 31));
        var kinds = new List<QuestKind>();
        foreach (int id in s.questIds) kinds.Add(cfg.questPool[id].kind);
        var pool = new List<int>();
        for (int q = 0; q < cfg.questPool.Count; q++)
            if (cfg.questPool[q].enabled && Feasible(cfg.questPool[q].kind) && !kinds.Contains(cfg.questPool[q].kind)) pool.Add(q);
        if (pool.Count == 0) return false;
        if (s.rerollDay != Today) { s.rerollDay = Today; s.rerollsUsed = 0; }
        s.rerollsUsed++;
        s.questIds[i] = pool[rng.Range(pool.Count)];
        s.questProgress[i] = 0;
        return true;
    }

    /// <summary>Угольки для знакомства с огоньком (один раз, если их нет).</summary>
    public void GiveStarter()
    {
        if (s.starterGiven) return;
        s.starterGiven = true;
        if (s.embers < cfg.starterEmbers) AddEmbers(cfg.starterEmbers - s.embers);
    }

    /// <summary>Прибавка к счётчику задания/темы от итога уровня (feed — от кормления очага).</summary>
    private int Increment(QuestKind k, int target, RetLevelResult r, int feed, int current, out bool absolute)
    {
        absolute = false;
        if (feed > 0) return k == QuestKind.FeedHearth ? feed : 0;
        bool w = r.won;
        switch (k)
        {
            case QuestKind.WinLevels: return w ? 1 : 0;
            case QuestKind.WinStars3: return w && r.stars >= 3 ? 1 : 0;
            case QuestKind.BurnMatches: return r.burnt;
            case QuestKind.ChainInLevel: absolute = true; return Math.Max(current, r.burnt);
            case QuestKind.BurnGold: return r.golds;
            case QuestKind.WinWater: return w && (r.tags & RetTags.Water) != 0 ? 1 : 0;
            case QuestKind.WinWind: return w && (r.tags & RetTags.Wind) != 0 ? 1 : 0;
            case QuestKind.WinPendulum: return w && (r.tags & RetTags.Pendulum) != 0 ? 1 : 0;
            case QuestKind.WinImmune: return w && (r.tags & RetTags.Immune) != 0 ? 1 : 0;
            case QuestKind.WinNoRestart: return w && !r.restarted ? 1 : 0;
            case QuestKind.WinFast: return w && r.seconds > 0f && r.seconds <= target ? 1 : 0;
            case QuestKind.PlayDaily: return w && r.daily ? 1 : 0;
            case QuestKind.Daily3Stars: return w && r.daily && r.stars >= 3 ? 1 : 0;
            case QuestKind.EarnStars: return w ? r.stars : 0;
            case QuestKind.WinHard: return w && (r.tags & RetTags.Hard) != 0 ? 1 : 0;
            case QuestKind.WinDifferent: absolute = true; return s.winsTodayLevels.Length;
            default: return 0;
        }
    }

    private void UpdateQuests(RetLevelResult r, int feed)
    {
        if (s.questDay != Today) return;
        for (int i = 0; i < s.questIds.Length; i++)
        {
            if (s.questClaimed[i] != 0) continue;
            RetQuest q = cfg.questPool[s.questIds[i]];
            int inc = Increment(q.kind, q.target, r, feed, s.questProgress[i], out bool abs);
            s.questProgress[i] = abs ? inc : s.questProgress[i] + inc;
            if (s.questProgress[i] >= Needed(q))
            {
                s.questProgress[i] = Needed(q);
                s.questClaimed[i] = 1;
                AddEmbers(cfg.questEmbers);
                AddAroma(q.fuel, cfg.questAroma);
                events.Add(new RetEvent(RetEventKind.QuestDone, s.questIds[i], cfg.questEmbers));
            }
        }
        if (!s.questAllClaimed && s.questIds.Length > 0 && Array.TrueForAll(s.questClaimed, c => c != 0))
        {
            s.questAllClaimed = true;
            AddEmbers(cfg.questAllBonus);
            events.Add(new RetEvent(RetEventKind.QuestsAll, cfg.questAllBonus));
        }
    }

    // ------------------------------------------------------------------ событие недели

    private int PickTheme(int week)
    {
        int n = cfg.weekThemes.Count;
        for (int k = 0; k < n; k++)
        {
            int i = DayClock.Mod(week + k, n);
            if (cfg.weekThemes[i].enabled && Feasible(cfg.weekThemes[i].kind)) return i;
        }
        return n > 0 ? DayClock.Mod(week, n) : -1;
    }

    public RetWeekTheme Theme => s.weekTheme >= 0 && s.weekTheme < cfg.weekThemes.Count ? cfg.weekThemes[s.weekTheme] : null;
    public int ThemeNeeded => Theme == null ? 1 : Theme.kind == QuestKind.WinFast ? 5 : Theme.target;

    private void UpdateTheme(RetLevelResult r)
    {
        RetWeekTheme t = Theme;
        if (t == null || s.weekEventClaimed) return;
        int inc = Increment(t.kind, t.target, r, 0, s.weekEventProgress, out bool abs);
        s.weekEventProgress = abs ? Math.Max(s.weekEventProgress, inc) : s.weekEventProgress + inc;
        if (s.weekEventProgress >= ThemeNeeded)
        {
            s.weekEventProgress = ThemeNeeded;
            s.weekEventClaimed = true;
            AddEmbers(cfg.weeklyEventEmbers);
            bool newOutfit = t.outfit >= 0 && t.outfit < cfg.outfits.Count && !s.HasOutfit(t.outfit);
            if (newOutfit) GiveOutfit(t.outfit);
            else AddEmbers(cfg.weeklyEventDupEmbers);
            events.Add(new RetEvent(RetEventKind.WeekEvent, s.weekTheme, newOutfit ? t.outfit : -1));
        }
    }

    // ------------------------------------------------------------------ очаг

    public int NextStageWarmth => s.stage + 1 < cfg.stages.Count ? cfg.stages[s.stage + 1].warmth : -1;
    public int DominantFuel => s.aroma[0] > s.aroma[1] && s.aroma[0] >= s.aroma[2] ? 0 : s.aroma[2] > s.aroma[1] && s.aroma[2] > s.aroma[0] ? 2 : 1;
    public int LineOf(int stage) => stage >= 0 && stage < s.stageLine.Length && s.stageLine[stage] >= 0 ? s.stageLine[stage] : 1;

    /// <summary>Подкинуть угольки в очаг (тепло копится, стадии растут; облик стадии — по преобладающему «дереву»).</summary>
    public int Feed(int amount)
    {
        if (!cfg.hearth) return 0;
        amount = Math.Min(amount, s.embers);
        if (amount <= 0) return 0;
        s.embers -= amount;
        s.warmth += amount;
        while (s.stage + 1 < cfg.stages.Count && s.warmth >= cfg.stages[s.stage + 1].warmth)
        {
            s.stage++;
            int line = DominantFuel;
            s.stageLine[s.stage] = line;
            s.atlasMask |= 1 << ((s.stage - 1) * 3 + line);
            s.aroma = new int[3];
            RetStage st = cfg.stages[s.stage];
            if (st.coins > 0) AddCoins(st.coins);
            if (st.outfit >= 0 && st.outfit < cfg.outfits.Count) GiveOutfit(st.outfit);
            events.Add(new RetEvent(RetEventKind.StageUp, s.stage, line));
        }
        if (cfg.quests) UpdateQuests(default, amount);
        return amount;
    }

    private void AddAroma(RetFuel f, int n) { s.aroma[(int)f] += n; }

    /// <summary>Атлас: облик стадии (1..7) и линии открыт.</summary>
    public bool AtlasHas(int stage, int line) => stage >= 1 && (s.atlasMask & (1 << ((stage - 1) * 3 + line))) != 0;
    public int AtlasCount { get { int n = 0; for (int i = 0; i < 21; i++) if ((s.atlasMask & (1 << i)) != 0) n++; return n; } }

    /// <summary>«Новый круг»: после последней стадии огонёк по желанию снова становится искрой — чтобы открыть другие облики Атласа.
    /// Облики, декор, коробки и серия остаются.</summary>
    public bool CanRebirth => cfg.hearth && s.stage >= cfg.stages.Count - 1;
    public void Rebirth()
    {
        if (!CanRebirth) return;
        s.stage = 0; s.stageSeen = 0; s.warmth = 0; s.cycles++;
        s.aroma = new int[3];
        for (int i = 0; i < s.stageLine.Length; i++) s.stageLine[i] = -1;
    }

    public bool BuyDecor(int i)
    {
        if (i < 0 || i >= cfg.decor.Count || s.HasDecor(i) || !cfg.decor[i].enabled) return false;
        RetDecor d = cfg.decor[i];
        if (d.priceCoins > 0) { if (!wallet.SpendCoins(d.priceCoins)) return false; }
        else if (d.priceEmbers > 0) { if (s.embers < d.priceEmbers) return false; s.embers -= d.priceEmbers; }
        s.decorMask |= 1 << i;
        events.Add(new RetEvent(RetEventKind.Decor, i));
        return true;
    }

    public void Equip(int outfit) { if (outfit < 0 || s.HasOutfit(outfit)) s.equippedOutfit = outfit; }

    private void GiveOutfit(int i)
    {
        if (s.HasOutfit(i)) return;
        s.AddOutfit(i);
        events.Add(new RetEvent(RetEventKind.Outfit, i));
    }

    // ------------------------------------------------------------------ коробки

    /// <summary>Бросок коробка: редкость по шансам; пока в редкости есть недостающие — без дубликатов.</summary>
    public RetBoxRoll RollBox()
    {
        var rng = new RetRng(RetRng.Mix(s.rollCounter++, s.firstDay * 31 + 7));
        int total = 0; foreach (int o in cfg.rarityOdds) total += Math.Max(0, o);
        int roll = rng.Range(Math.Max(1, total)), rarity = 0;
        for (int acc = 0; rarity < cfg.rarityOdds.Length; rarity++) { acc += Math.Max(0, cfg.rarityOdds[rarity]); if (roll < acc) break; }
        rarity = Math.Min(rarity, 2);
        var missing = new List<int>(); var all = new List<int>();
        for (int i = 0; i < cfg.boxes.Count; i++)
        {
            RetBox b = cfg.boxes[i];
            if (!b.enabled || b.seasonal || (int)b.rarity != rarity) continue;
            all.Add(i);
            if (s.BoxCount(i) == 0) missing.Add(i);
        }
        if (all.Count == 0) return new RetBoxRoll { box = -1 };
        bool dup = missing.Count == 0;
        int pick = dup ? all[rng.Range(all.Count)] : missing[rng.Range(missing.Count)];
        return GiveBox(pick, dup);
    }

    public RetBoxRoll GiveBox(int i, bool dupKnown = false)
    {
        if (i < 0 || i >= cfg.boxes.Count) return new RetBoxRoll { box = -1 };
        RetBox b = cfg.boxes[i];
        bool dup = s.BoxCount(i) > 0;
        bool setBefore = SetComplete(b.set);
        s.boxCount[i]++;
        int e = 0;
        if (dup) { e = cfg.dupEmbers[Mathf.Clamp((int)b.rarity, 0, cfg.dupEmbers.Length - 1)]; AddEmbers(e); }
        events.Add(new RetEvent(RetEventKind.Box, i, dup ? 1 : 0));
        if (b.set >= 0 && !setBefore && SetComplete(b.set))
        {
            AddEmbers(cfg.setEmbers);
            events.Add(new RetEvent(RetEventKind.SetDone, b.set, cfg.setEmbers));
        }
        return new RetBoxRoll { box = i, duplicate = dup, rarity = b.rarity, embers = e };
    }

    public bool SetComplete(int set)
    {
        if (set < 0) return false;
        int n = 0;
        for (int i = 0; i < cfg.boxes.Count; i++) if (cfg.boxes[i].set == set) { if (s.BoxCount(i) == 0) return false; n++; }
        return n > 0;
    }

    public int BoxesOwned { get { int n = 0; for (int i = 0; i < s.boxCount.Length; i++) if (s.boxCount[i] > 0) n++; return n; } }

    // ------------------------------------------------------------------ секретный коробок

    public bool SecretVisible => cfg.secretBox && !s.secretOpened && s.secretWeek != int.MinValue;
    public bool SecretConditionMet => s.secretEarned || s.streak >= cfg.secretStreak || s.weekGoalClaimed;
    public bool SecretReady => SecretVisible && Today >= s.secretOpenDay && SecretConditionMet;

    public void OpenSecret()
    {
        if (!SecretReady) return;
        s.secretOpened = true;
        int[] list = cfg.secretOutfits;
        int given = -1;
        for (int k = 0; k < list.Length; k++)
        {
            int o = list[DayClock.Mod(s.secretRewardIndex + k, list.Length)];
            if (o >= 0 && o < cfg.outfits.Count && !s.HasOutfit(o)) { given = o; break; }
        }
        s.secretRewardIndex++;
        if (given >= 0) { GiveOutfit(given); events.Add(new RetEvent(RetEventKind.SecretOpened, 0, given)); return; }
        // Все облики есть — легендарный коробок (недостающий) или угольки
        for (int i = 0; i < cfg.boxes.Count; i++)
            if (cfg.boxes[i].enabled && !cfg.boxes[i].seasonal && cfg.boxes[i].rarity == BoxRarity.Legendary && s.BoxCount(i) == 0)
            { GiveBox(i); events.Add(new RetEvent(RetEventKind.SecretOpened, 1, i)); return; }
        AddEmbers(150);
        events.Add(new RetEvent(RetEventKind.SecretOpened, 2, 150));
    }

    // ------------------------------------------------------------------ праздники

    public RetSeasonal ActiveSeasonal(int day, out string key)
    {
        key = "";
        if (!cfg.seasonal || seasonal == null) return null;
        DateTime d = DayClock.DateOf(day);
        foreach (RetSeasonal e in seasonal)
            if (e != null && e.Contains(d, out int year)) { key = e.id + ":" + year; return e; }
        return null;
    }

    private void SeasonalDaily()
    {
        RetSeasonal ev = ActiveSeasonal(Today, out string key);
        if (ev == null) return;
        if (s.eventKey != key) { s.eventKey = key; s.eventDailyCount = 0; }
        s.eventDailyCount++;
        if (s.eventDailyCount >= ev.dailyTarget && Array.IndexOf(s.eventClaimed, key) < 0)
        {
            Array.Resize(ref s.eventClaimed, s.eventClaimed.Length + 1);
            s.eventClaimed[s.eventClaimed.Length - 1] = key;
            if (ev.outfit >= 0 && ev.outfit < cfg.outfits.Count) GiveOutfit(ev.outfit);
            if (ev.box >= 0 && cfg.collection) GiveBox(ev.box);
            events.Add(new RetEvent(RetEventKind.SeasonReward, ev.outfit, ev.box));
        }
    }

    // ------------------------------------------------------------------ история и ресурсы

    private void SetHistory(int day, int flag)
    {
        int i = Array.IndexOf(s.historyDay, day);
        if (i < 0)
        {
            Array.Resize(ref s.historyDay, s.historyDay.Length + 1);
            Array.Resize(ref s.historyFlags, s.historyFlags.Length + 1);
            i = s.historyDay.Length - 1;
            s.historyDay[i] = day;
        }
        s.historyFlags[i] |= flag;
    }

    public int HistoryFlags(int day) { int i = Array.IndexOf(s.historyDay, day); return i >= 0 ? s.historyFlags[i] : 0; }

    private void TrimHistory(int today)
    {
        var d = new List<int>(); var f = new List<int>();
        for (int i = 0; i < s.historyDay.Length; i++)
            if (s.historyDay[i] > today - 14 && s.historyDay[i] <= today) { d.Add(s.historyDay[i]); f.Add(s.historyFlags[i]); }
        s.historyDay = d.ToArray(); s.historyFlags = f.ToArray();
    }

    private void AddEmbers(int n)
    {
        if (n <= 0) return;
        s.embers += n;
        s.embersEarned += n;
        events.Add(new RetEvent(RetEventKind.Embers, n));
    }

    private void AddCoins(int n)
    {
        if (n <= 0) return;
        wallet.AddCoins(n);
        s.coinsEarned += n;
    }
}
