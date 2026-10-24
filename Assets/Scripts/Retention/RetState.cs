using System;
using UnityEngine;

/// <summary>
/// Сохраняемое состояние «Очага» (JSON в PlayerPrefs «Ret_State», версия — «Ret_SaveVersion»).
/// Дни — номера локальных календарных дат (DayClock.DayNumber); −1 — «ещё не было».
/// </summary>
[Serializable]
public class RetState
{
    public const int CurrentVersion = 2;
    public int version = CurrentVersion;

    // Время
    public int firstDay = -1, lastDay = -1, maxDay = -1;
    public long lastUtcTicks;
    public int rollbackFirstDay = -1;               // часы переведены назад: с какого дня (для мягкого «привыкания»)

    // Серия и щиты
    public int streak, bestStreak, lastCountedDay = -1;
    public int shields, shieldWeek = int.MinValue;
    public int restoreDay = -1, restoreValue;      // в этот день серию можно вернуть за rewarded
    public int milestoneIndex;                      // сколько вех серии уже выдано в текущей серии
    public int welcomeDay = -1;                     // «С возвращением!» показать в этот день

    // Подарок дня
    public int giftDay = -1;
    public bool firstRewardGiven;

    // Очаг
    public int embers, warmth, stage;
    public int[] aroma = new int[3];
    public int[] stageLine = { -1, -1, -1, -1, -1, -1, -1, -1 };
    public int stageSeen;                           // до какой стадии игроку показан переход
    public int atlasMask;                           // открытые облики Атласа: бит (стадия−1)·3 + линия
    public int cycles;                              // сколько раз огонёк начинал «новый круг»
    public int embersEarned, coinsEarned;

    // Уровень дня
    public int dailyWinDay = -1, daily3Day = -1, dailyDoubleDay = -1, dailyStarsToday;
    public int[] historyDay = new int[0], historyFlags = new int[0];   // последние 14 дней: 1 победа, 2 — 3★, 4 — щит, 8 — подарок

    // Задания дня
    public int questDay = -1;
    public int[] questIds = new int[0], questProgress = new int[0], questClaimed = new int[0];
    public bool questAllClaimed;
    public int regularWinsDay = -1, regularWinsToday;
    public int[] winsTodayLevels = new int[0];

    // Неделя
    public int week = int.MinValue, weekWins;
    public bool weekGoalClaimed;
    public int weekTheme = -1, weekEventProgress, weekFastWins;
    public bool weekEventClaimed;

    // Секретный коробок
    public int secretWeek = int.MinValue, secretOpenDay = -1, secretRewardIndex;
    public bool secretOpened = true;                // true — ждём новую неделю
    public bool secretEarned;                       // условие выполнено (серия или недельная цель)

    // Комната, облики, коллекция
    public int decorMask;
    public int[] outfitOwned = new int[0];          // индексы
    public int equippedOutfit = -1;
    public int[] boxCount = new int[0];
    public int rollCounter;

    // Праздники
    public string eventKey = "";                    // id:год текущего праздника
    public int eventDailyCount;
    public string[] eventClaimed = new string[0];

    // Прочее
    public string seenWhatsNew = "";
    // Версия 2: мягкие бейджи «новое», знакомство и постепенное открытие
    public int questsSeenDay = -1, boxesSeen, outfitsSeen;
    public bool introDone;
    public int featuresSeen;                        // биты: 1 Очаг, 2 Сегодня, 4 коллекция, 8 события
    public int lastSessionGreetDay = -1;
    public int rerollDay = -1, rerollsUsed, weekStepsClaimed, questsShownMask, questsShownDay = -1;
    public bool starterGiven;
    public int chapterSeen = -1;
    public bool notifEnabled = true, notifAsked;
    public int notifHour = -1;

    public bool HasOutfit(int i) => Array.IndexOf(outfitOwned, i) >= 0;
    public void AddOutfit(int i) { if (i >= 0 && !HasOutfit(i)) { Array.Resize(ref outfitOwned, outfitOwned.Length + 1); outfitOwned[outfitOwned.Length - 1] = i; } }
    public bool HasDecor(int i) => i >= 0 && i < 31 && (decorMask & (1 << i)) != 0;
    public int BoxCount(int i) => i >= 0 && i < boxCount.Length ? boxCount[i] : 0;
}

/// <summary>Хранилище строк (PlayerPrefs в игре, память в тестах).</summary>
public interface IRetStore
{
    string GetString(string key, string def);
    int GetInt(string key, int def);
    void SetString(string key, string value);
    void SetInt(string key, int value);
    void DeleteKey(string key);
    void Save();
}

public class PlayerPrefsStore : IRetStore
{
    public string GetString(string key, string def) => PlayerPrefs.GetString(key, def);
    public int GetInt(string key, int def) => PlayerPrefs.GetInt(key, def);
    public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
    public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
    public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
    public void Save() => PlayerPrefs.Save();
}

public class MemoryStore : IRetStore
{
    public readonly System.Collections.Generic.Dictionary<string, string> data = new System.Collections.Generic.Dictionary<string, string>();
    public string GetString(string key, string def) => data.TryGetValue(key, out string v) ? v : def;
    public int GetInt(string key, int def) => data.TryGetValue(key, out string v) && int.TryParse(v, out int i) ? i : def;
    public void SetString(string key, string value) => data[key] = value;
    public void SetInt(string key, int value) => data[key] = value.ToString();
    public void DeleteKey(string key) => data.Remove(key);
    public void Save() { }
}

/// <summary>Загрузка с миграцией и проверкой; повреждённые данные не роняют игру.</summary>
public static class RetSave
{
    public const string StateKey = "Ret_State", VersionKey = "Ret_SaveVersion", BrokenKey = "Ret_StateBroken";

    public static RetState Load(IRetStore store, RetentionSettings cfg, out string problem)
    {
        problem = null;
        string json = store.GetString(StateKey, "");
        int version = store.GetInt(VersionKey, 0);
        if (string.IsNullOrEmpty(json)) return Validate(new RetState(), cfg);
        RetState s = null;
        try { s = JsonUtility.FromJson<RetState>(json); }
        catch (Exception e) { problem = "parse: " + e.Message; }
        if (s == null)
        {
            problem = problem ?? "parse: null";
            store.SetString(BrokenKey, json);   // для разбора; игрок получает чистое состояние
            return Validate(new RetState(), cfg);
        }
        if (version <= 0) version = s.version;
        if (version > RetState.CurrentVersion) problem = $"newer save version {version}";
        Migrate(s, version);
        return Validate(s, cfg);
    }

    public static void Write(IRetStore store, RetState s)
    {
        s.version = RetState.CurrentVersion;
        store.SetString(StateKey, JsonUtility.ToJson(s));
        store.SetInt(VersionKey, RetState.CurrentVersion);
        store.Save();
    }

    /// <summary>Миграции по шагам: from → from+1 … (сейчас версия 1 — первая).</summary>
    private static void Migrate(RetState s, int from)
    {
        if (from < 2)
        {
            // 1 → 2: новые бейджи «новое» не должны загореться разом у тех, кто уже играл; знакомство с огоньком им не нужно
            s.boxesSeen = 0; foreach (int c in s.boxCount) if (c > 0) s.boxesSeen++;
            s.outfitsSeen = s.outfitOwned != null ? s.outfitOwned.Length : 0;
            s.questsSeenDay = s.questDay;
            s.introDone = s.lastDay >= 0;
            s.featuresSeen = s.lastDay >= 0 ? 0xF : 0;
        }
        s.version = RetState.CurrentVersion;
    }

    /// <summary>Границы и размеры массивов: битые значения → по умолчанию, без исключений.</summary>
    public static RetState Validate(RetState s, RetentionSettings cfg)
    {
        int stages = Mathf.Max(1, cfg.stages.Count);
        s.streak = Mathf.Clamp(s.streak, 0, 100000);
        s.bestStreak = Mathf.Max(s.bestStreak, s.streak);
        s.shields = Mathf.Clamp(s.shields, 0, cfg.shieldMax);
        s.embers = Mathf.Clamp(s.embers, 0, 10000000);
        s.warmth = Mathf.Clamp(s.warmth, 0, 100000000);
        if (s.stage < 0 || s.stage >= stages)
        {
            // Битый номер стадии — пересчитать по накопленному теплу (а не выдать максимум)
            s.stage = 0;
            for (int i = 1; i < cfg.stages.Count; i++) if (s.warmth >= cfg.stages[i].warmth) s.stage = i;
        }
        s.stageSeen = Mathf.Clamp(s.stageSeen, 0, s.stage);
        if (s.aroma == null || s.aroma.Length != 3) s.aroma = new int[3];
        for (int i = 0; i < 3; i++) s.aroma[i] = Mathf.Max(0, s.aroma[i]);
        if (s.stageLine == null || s.stageLine.Length != stages)
        {
            int[] l = new int[stages];
            for (int i = 0; i < stages; i++) l[i] = s.stageLine != null && i < s.stageLine.Length ? s.stageLine[i] : -1;
            s.stageLine = l;
        }
        for (int i = 0; i < s.stageLine.Length; i++) if (s.stageLine[i] < -1 || s.stageLine[i] > 2) s.stageLine[i] = -1;
        if (s.historyDay == null || s.historyFlags == null || s.historyDay.Length != s.historyFlags.Length) { s.historyDay = new int[0]; s.historyFlags = new int[0]; }
        if (s.questIds == null || s.questProgress == null || s.questClaimed == null
            || s.questIds.Length != s.questProgress.Length || s.questIds.Length != s.questClaimed.Length
            || Array.Exists(s.questIds, q => q < 0 || q >= cfg.questPool.Count))
        { s.questIds = new int[0]; s.questProgress = new int[0]; s.questClaimed = new int[0]; s.questDay = -1; }
        if (s.winsTodayLevels == null) s.winsTodayLevels = new int[0];
        if (s.outfitOwned == null) s.outfitOwned = new int[0];
        s.outfitOwned = Array.FindAll(s.outfitOwned, o => o >= 0 && o < cfg.outfits.Count);
        if (s.equippedOutfit >= cfg.outfits.Count || (s.equippedOutfit >= 0 && !s.HasOutfit(s.equippedOutfit))) s.equippedOutfit = -1;
        if (s.boxCount == null) s.boxCount = new int[0];
        if (s.boxCount.Length != cfg.boxes.Count) Array.Resize(ref s.boxCount, cfg.boxes.Count);
        for (int i = 0; i < s.boxCount.Length; i++) s.boxCount[i] = Mathf.Max(0, s.boxCount[i]);
        s.decorMask &= (1 << Mathf.Min(30, cfg.decor.Count)) - 1;
        if (s.weekTheme >= cfg.weekThemes.Count) s.weekTheme = -1;
        if (s.eventClaimed == null) s.eventClaimed = new string[0];
        if (s.eventKey == null) s.eventKey = "";
        if (s.seenWhatsNew == null) s.seenWhatsNew = "";
        if (s.notifHour < -1 || s.notifHour > 23) s.notifHour = -1;
        // Индексы и счётчики из повреждённого сохранения — в допустимые пределы (иначе выход за массив на каждой победе)
        s.milestoneIndex = Mathf.Clamp(s.milestoneIndex, 0, cfg.streakMilestones != null ? cfg.streakMilestones.Length : 0);
        if (s.weekTheme < -1) s.weekTheme = -1;
        s.secretRewardIndex = Mathf.Max(0, s.secretRewardIndex);
        s.weekWins = Mathf.Clamp(s.weekWins, 0, 100000);
        s.weekEventProgress = Mathf.Max(0, s.weekEventProgress);
        s.weekFastWins = Mathf.Max(0, s.weekFastWins);
        s.regularWinsToday = Mathf.Max(0, s.regularWinsToday);
        s.dailyStarsToday = Mathf.Clamp(s.dailyStarsToday, 0, 3);
        s.eventDailyCount = Mathf.Max(0, s.eventDailyCount);
        s.rerollsUsed = Mathf.Max(0, s.rerollsUsed);
        s.restoreValue = Mathf.Max(0, s.restoreValue);
        s.cycles = Mathf.Max(0, s.cycles);
        s.rollCounter = Mathf.Max(0, s.rollCounter);
        s.boxesSeen = Mathf.Max(0, s.boxesSeen);
        s.outfitsSeen = Mathf.Max(0, s.outfitsSeen);
        s.embersEarned = Mathf.Max(0, s.embersEarned);
        s.coinsEarned = Mathf.Max(0, s.coinsEarned);
        if (s.chapterSeen < -1) s.chapterSeen = -1;
        // Даты: лишний «максимум» из будущего (повреждение) не должен навсегда заморозить новые дни
        if (s.maxDay < s.lastDay) s.maxDay = s.lastDay;
        return s;
    }
}
