using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Кошелёк монет — существующий CurrencyManager (тот же ключ PlayerPrefs).</summary>
public class CurrencyWallet : IRetWallet
{
    public int Coins => CurrencyManager.GetGoldenMatches();
    public void AddCoins(int amount) => CurrencyManager.AddGoldenMatches(amount);
    public bool SpendCoins(int amount) => CurrencyManager.SpendGoldenMatches(amount);
}

/// <summary>
/// Система «Очаг» в игре: живёт между сценами, хранит состояние (Ret_State), следит за сменой дня
/// (запуск, возврат из фона, раз в 30 с), принимает итоги уровней и запускает уровень дня.
/// Логика — RetentionCore; окна — RetentionUI. Всё выключается флагом RetentionSettings.systemEnabled.
/// </summary>
public class Retention : MonoBehaviour
{
    public static Retention Instance { get; private set; }
    public static bool Active => Instance != null && Instance.core != null;

    /// <summary>Состояние или события изменились (окна перерисовываются).</summary>
    public static event Action Changed;
    /// <summary>Отдельные события (подарок, стадия, задание…) — для всплывашек.</summary>
    public static event Action<RetEvent> Raised;

    public RetentionCore core;
    public RetentionSettings Cfg => RetentionSettings.Current;
    public RetState S => core.s;

    /// <summary>Награды, которые стоит показать окном (коробок, облик, веха, неделя, праздник) — меню показывает их по очереди.</summary>
    public static readonly Queue<RetEvent> Unseen = new Queue<RetEvent>();

    private IRetStore store;
    private float nextCheck;
    private readonly List<RetEvent> pending = new List<RetEvent>();
    public string LoadProblem { get; private set; }

    // Уровень дня: запуск из меню → GameScene берёт префаб отсюда
    public static int PendingDaily = -1;
    public static bool PlayingDaily => PendingDaily >= 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Instance != null || !RetentionSettings.Current.systemEnabled) return;
        var go = new GameObject("Retention");
        DontDestroyOnLoad(go);
        go.AddComponent<Retention>();
    }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        store = new PlayerPrefsStore();
        try
        {
            RetState state = RetSave.Load(store, Cfg, out string problem);
            LoadProblem = problem;
            if (problem != null) Debug.LogWarning("[Retention] save: " + problem);
            core = NewCore(state);
            CheckDay();
        }
        catch (System.Exception e)
        {
            // Повреждённые данные: «Очаг» начинает с чистого состояния; если и так не вышло — система
            // возвращения выключается (Active = false), игра работает как без неё
            RetSafe.Report("load", e);
            try { core = NewCore(RetSave.Validate(new RetState(), Cfg)); CheckDay(); }
            catch (System.Exception e2) { RetSafe.Report("load-default", e2); core = null; }
        }
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private RetentionCore NewCore(RetState state)
    {
        var c = new RetentionCore(Cfg, state, new CurrencyWallet());
        c.seasonal = LoadSeasonal();
        c.availableTags = AvailableTags;
        c.dailyAvailable = DailyLevelPool.Current != null && DailyLevelPool.Current.entries.Count > 0;
        return c;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    /// <summary>Интерфейс «Очага» для сцены: меню — карточки и окна, игра — награда уровня дня и всплывашки.</summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        try
        {
            if (scene.name == "MainScene") RetentionMenuUI.Attach();
            else if (scene.name == "GameScene") RetentionGameUI.Attach();
        }
        catch (System.Exception e) { RetSafe.Report("attach " + scene.name, e); }
        if (scene.name != "GameScene") PendingDaily = -1;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 30f;
        if (core == null) return;
        try { CheckDay(); } catch (System.Exception e) { RetSafe.Report("check day", e); }
    }

    private void OnApplicationFocus(bool focus)
    {
        if (!focus || core == null) return;
        try { CheckDay(); } catch (System.Exception e) { RetSafe.Report("check day", e); }
    }

    private void OnApplicationPause(bool paused)
    {
        if (core == null) return;
        try
        {
            if (!paused) { CheckDay(); return; }
            Save();
            NotificationPlanner.Reschedule(core);   // без пакета уведомлений — пустая реализация
        }
        catch (System.Exception e) { RetSafe.Report("app pause", e); }
    }

    public void CheckDay()
    {
        if (core == null) return;
        bool newDay = core.Advance(DayClock.Today);
        if (newDay || core.events.Count > 0) Flush();   // без нового дня — ни записи, ни выделений
    }

    /// <summary>Сохранить и разослать накопленные события.</summary>
    public void Flush()
    {
        Save();
        if (core.events.Count == 0) { Changed?.Invoke(); return; }
        pending.Clear();
        pending.AddRange(core.events);
        core.events.Clear();
        bool stageUp = pending.Exists(e => e.kind == RetEventKind.StageUp);
        foreach (RetEvent e in pending)
        {
            switch (e.kind)
            {
                case RetEventKind.Box: case RetEventKind.Milestone: case RetEventKind.WeekGoal: case RetEventKind.WeekEvent:
                case RetEventKind.SeasonReward: case RetEventKind.SecretOpened: case RetEventKind.SetDone:
                    if (Unseen.Count < 20) Unseen.Enqueue(e); break;
                case RetEventKind.Outfit:
                    if (!stageUp && Unseen.Count < 20) Unseen.Enqueue(e); break;   // облик стадии показывает окно стадии
            }
        }
        foreach (RetEvent e in pending) Raised?.Invoke(e);
        Changed?.Invoke();
    }

    public void Save() { if (core != null) RetSave.Write(store, core.s); }

    // ------------------------------------------------------------------ данные для ядра

    private static List<RetSeasonal> LoadSeasonal()
    {
        var list = new List<RetSeasonal>();
        var ta = Resources.Load<TextAsset>("Retention/events");
        if (ta == null) return list;
        try
        {
            RetSeasonalList l = JsonUtility.FromJson<RetSeasonalList>(ta.text);
            if (l != null && l.events != null) foreach (RetSeasonal e in l.events) if (e != null && !string.IsNullOrEmpty(e.id)) list.Add(e);
        }
        catch (Exception e) { Debug.LogWarning("[Retention] events.json: " + e.Message); }
        return list;
    }

    public static RetWhatsNew WhatsNewFor(string version)
    {
        var ta = Resources.Load<TextAsset>("Retention/whatsnew");
        if (ta == null) return null;
        try
        {
            RetWhatsNewList l = JsonUtility.FromJson<RetWhatsNewList>(ta.text);
            if (l?.entries != null) foreach (RetWhatsNew w in l.entries) if (w != null && w.version == version) return w;
        }
        catch (Exception e) { Debug.LogWarning("[Retention] whatsnew.json: " + e.Message); }
        return null;
    }

    /// <summary>Механики открытых уровней (замок — как на экране выбора) и сегодняшнего уровня дня.</summary>
    private RetTags AvailableTags()
    {
        RetentionSettings c = Cfg;
        RetTags t = RetTags.None;
        bool locked = VisualTheme.Current.levelLock;
        for (int i = 0; i < c.levelTags.Length; i++)
        {
            bool open = !locked || i == 0 || PlayerPrefs.GetInt("Level_" + (i - 1) + "_Stars", 0) > 0;
            if (open) t |= (RetTags)c.levelTags[i];
        }
        int d = TodayDailyIndex;
        if (d >= 0) t |= (RetTags)DailyLevelPool.Current.entries[d].tags;
        return t;
    }

    // ------------------------------------------------------------------ уровень дня

    public int TodayDailyIndex
    {
        get
        {
            DailyLevelPool pool = DailyLevelPool.Current;
            if (!Cfg.dailyLevel || pool == null || core == null || core.Today < 0) return -1;
            return pool.IndexFor(core.Today, Cfg.dailySeed);
        }
    }

    public void StartDaily()
    {
        int i = TodayDailyIndex;
        if (i < 0) return;
        PendingDaily = i;
        GameManager.RestartPressed = false;
        SceneLoadGuard.Load("GameScene");
    }

    /// <summary>Итог уровня (из GameManager): серия, задания, неделя, награды уровня дня.</summary>
    public void ReportLevelEnd(RetLevelResult r)
    {
        core.Advance(DayClock.Today);
        if (!Unlocked && !r.daily) { Flush(); return; }   // до знакомства с Очагом — без серий и заданий (новичок играет спокойно)
        core.OnLevelEnd(r);
        Flush();
        if (r.daily && r.won) NotificationPlanner.Reschedule(core);   // уровень дня пройден — сегодня не напоминаем
    }

    // ------------------------------------------------------------------ действия игрока (из окон)

    public void ClaimGift() { core.ClaimGift(); Flush(); }
    public bool RerollQuest(int i) { bool ok = core.RerollQuest(i); Flush(); return ok; }
    public void GiveStarter() { core.GiveStarter(); Flush(); }

    /// <summary>Мягкий вход: Очаг, «Сегодня» и задания — после первых уровней (или если игрок уже знаком с Очагом).</summary>
    public bool Unlocked
    {
        get
        {
            int n = Mathf.Max(1, Cfg.unlockLevels);
            return core.s.embersEarned > 0 || core.s.stage > 0 || PlayerPrefs.GetInt("Level_" + (n - 1) + "_Stars", 0) > 0;
        }
    }
    /// <summary>Коллекция, события, тизер — чуть позже.</summary>
    public bool ExtrasUnlocked => Unlocked && (core.s.stage >= Cfg.extrasStage || core.s.cycles > 0);
    public int Feed(int amount) { int n = core.Feed(amount); Flush(); return n; }
    public bool BuyDecor(int i) { bool ok = core.BuyDecor(i); Flush(); return ok; }
    public void Equip(int outfit) { core.Equip(outfit); Flush(); }
    public void OpenSecret() { core.OpenSecret(); Flush(); }
    public void MarkStageSeen() { core.s.stageSeen = core.s.stage; Save(); }
    public void Rebirth() { core.Rebirth(); Flush(); }

    /// <summary>«Вернуть серию» — только после добровольно досмотренного ролика.</summary>
    public void RestoreStreakWithAd()
    {
        if (!core.CanRestoreStreak) return;
        int day = core.Today;   // если день сменится, пока идёт ролик, награда всё равно за досмотренное
        AdsManager.ShowRewardedAd(() => { core.RestoreStreak(day); Flush(); });
    }

    /// <summary>«Удвоить» угольки уровня дня — добровольный ролик.</summary>
    public void DoubleDailyWithAd()
    {
        if (!core.CanDoubleDaily) return;
        int day = core.Today;
        AdsManager.ShowRewardedAd(() => { core.DoubleDaily(day); Flush(); });
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // ------------------------------------------------------------------ отладка (только dev)

    public void DebugShiftDays(int n)
    {
        DayClock.DebugOffset += n;
        if (n < 0) { core.s.lastDay = Mathf.Min(core.s.lastDay, DayClock.Today - 1); core.s.maxDay = core.s.lastDay; core.s.rollbackFirstDay = -1; }   // в отладке назад можно
        CheckDay();
    }

    public void DebugReset()
    {
        foreach (string k in new[] { RetSave.StateKey, RetSave.VersionKey, RetSave.BrokenKey, "Ret_DebugDayOffset" }) PlayerPrefs.DeleteKey(k);
        PlayerPrefs.Save();
        core = new RetentionCore(Cfg, RetSave.Validate(new RetState(), Cfg), new CurrencyWallet());
        core.seasonal = LoadSeasonal();
        core.availableTags = AvailableTags;
        core.dailyAvailable = DailyLevelPool.Current != null && DailyLevelPool.Current.entries.Count > 0;
        CheckDay();
    }

    public void DebugEmbers(int n) { core.s.embers += n; Flush(); }
    public void DebugBox() { core.RollBox(); Flush(); }
    public void DebugWinDaily(int stars)
    {
        int i = TodayDailyIndex;
        RetTags tags = i >= 0 ? (RetTags)DailyLevelPool.Current.entries[i].tags : RetTags.None;
        ReportLevelEnd(new RetLevelResult { won = true, stars = stars, burnt = 12, total = 12, daily = true, levelIndex = -1, tags = tags, seconds = 9f });
    }
    public void DebugSecretNow() { core.s.secretOpenDay = core.Today; core.s.secretEarned = true; Flush(); }
    public void DebugStageUp()
    {
        int next = core.NextStageWarmth;
        if (next < 0) return;
        core.s.embers += next - core.s.warmth;
        core.Feed(next - core.s.warmth);
        Flush();
    }
    public void DebugNextEvent()
    {
        RetSeasonal best = null; int bestDays = int.MaxValue;
        System.DateTime today = DayClock.DateOf(core.Today);
        foreach (RetSeasonal e in core.seasonal)
        {
            int d = (int)(e.NextStart(today.AddDays(1)) - today).TotalDays;
            if (d > 0 && d < bestDays) { bestDays = d; best = e; }
        }
        if (best != null) DebugShiftDays(bestDays);
    }
    public void DebugGiftAgain() { core.s.giftDay = -1; Flush(); }
    public void DebugIntroAgain() { core.s.introDone = false; core.s.starterGiven = false; core.s.featuresSeen = 0; Flush(); }
#endif
}
