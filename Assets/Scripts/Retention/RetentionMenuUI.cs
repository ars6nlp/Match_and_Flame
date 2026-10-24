using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// «Очаг» в главном меню: карточка «Сегодня» (уровень дня, серия, награда, время до нового), кнопка Очаг, задания,
/// событие недели/праздник, тизер ближайшего праздника. Карточки живут внутри MenuPanel и прячутся вместе с ним
/// (магазин, настройки). Окна — на отдельном Canvas поверх меню (ниже дверей перехода).
/// Порядок при входе: «Что нового» (раз за версию) → подарок дня (раз в день) → накопленные награды.
/// </summary>
public class RetentionMenuUI : MonoBehaviour
{
    public static RetentionMenuUI Instance { get; private set; }

    private RetentionSettings C => RetentionSettings.Current;
    private RetentionCore Core => Retention.Instance.core;

    private Canvas overlay;
    public RectTransform OverlayRoot { get; private set; }
    private RectTransform widgets;
    private TextMeshProUGUI shieldCount;
    private TextMeshProUGUI todayTitle, todayStatus, todayReward, todayStreak, hearthSub, questsLabel, eventLabel, teaserText;
    private Image todayIcon, todayCheck, eventIcon, teaserIcon, shieldIcon;
    private readonly Image[] streakDots = new Image[7];
    private RetBubble greetBubble;
    private static bool greetedThisSession;
    /// <summary>Открыть Очаг при следующем входе в меню (кнопка «В Очаг» после уровня дня).</summary>
    public static bool OpenHearthOnEnter;
    private RectTransform teaser, eventChip;
    private float nextTick;
    private bool startupDone;

    public HearthScreen Hearth { get; private set; }
    public DailyEntryScreen Daily { get; private set; }
    public DiaryScreen Diary { get; private set; }
    public CollectionScreen Collection { get; private set; }
    public RetPopups Popups { get; private set; }

    public static void Attach()
    {
        if (!Retention.Active || Instance != null) return;
        var menu = GameObject.Find("MenuPanel");
        if (menu == null) return;
        var go = new GameObject("RetentionMenuUI");
        Instance = go.AddComponent<RetentionMenuUI>();
        try { Instance.Build(menu.transform); }
        catch (System.Exception e)
        {
            // Меню должно работать и без «Очага»: убираем недостроенные карточки
            RetSafe.Report("menu build", e);
            Transform partial = menu.transform.Find("Retention");
            if (partial != null) Destroy(partial.gameObject);
            Destroy(go);
            Instance = null;
        }
    }

    /// <summary>Для аудита раскладки в редакторе (без Play Mode): построить карточки в указанную панель.</summary>
    public static RetentionMenuUI BuildForAudit(Transform menuPanel)
    {
        var go = new GameObject("RetentionMenuUI");
        var ui = go.AddComponent<RetentionMenuUI>();
        ui.Build(menuPanel, audit: true);
        return ui;
    }

    private void OnDestroy()
    {
        Retention.Changed -= Refresh;
        if (Instance == this) Instance = null;
    }

    private void Build(Transform menuPanel, bool audit = false)
    {
        overlay = RetUi.Overlay("RetentionOverlay", 60);
        overlay.transform.SetParent(transform, false);
        OverlayRoot = (RectTransform)overlay.transform;

        widgets = RetUi.Fill(menuPanel, "Retention");
        widgets.SetSiblingIndex(Mathf.Max(0, menuPanel.childCount - 3));
        BuildToday();
        BuildRight();
        BuildTeaser();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (C.debugPanel && !audit)
            HearthStyle.Button(widgets, "DebugButton", "DEV", "DEV", new Vector2(0f, 0f), new Vector2(125f, 330f), 150f, () => RetDebugPanel.Open(OverlayRoot), ButtonKind.Secondary, 132f, TextLevel.Caption);
#endif
        if (!audit) AddReminderSettings();
        Hearth = new HearthScreen(this);
        Daily = new DailyEntryScreen(this);
        Diary = new DiaryScreen(this);
        Collection = new CollectionScreen(this);
        Popups = new RetPopups(this);
        if (audit) { RefreshWith(null); return; }
        Retention.Changed += Refresh;
        Refresh();
    }

    // ------------------------------------------------------------------ карточки

    private void BuildToday()
    {
        // Размер и место прежние (290×390, слева по центру), меняется только вид: светлая карточка, тёмный текст ≥ 14 sp
        Image card = HearthStyle.Card(widgets, "TodayCard", new Vector2(0f, 0.5f), new Vector2(185f, -40f), new Vector2(290f, 390f), CardKind.Window, raycast: true);
        RetUi.HitArea(card.gameObject, OnToday);
        todayTitle = HearthStyle.Text(card.transform, "Title", "Сегодня", "Today", TextLevel.Caption, HearthVisualSettings.Current.heading, new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(260f, 50f));
        todayTitle.fontStyle = FontStyles.Bold;
        todayIcon = RetUi.Img(card.transform, "Icon", RetUi.Art("box_00"), Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 74f), new Vector2(84f, 112f));
        todayCheck = HearthStyle.Icon(card.transform, "Check", "ic_check", new Vector2(0.5f, 0.5f), new Vector2(46f, 36f), 54f);
        todayStatus = HearthStyle.Text(card.transform, "Status", "", "", TextLevel.Caption, HearthVisualSettings.Current.ink, new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(270f, 46f));
        todayStatus.fontStyle = FontStyles.Bold;
        todayStatus.textWrappingMode = TextWrappingModes.NoWrap;
        todayReward = HearthStyle.Text(card.transform, "Reward", "", "", TextLevel.Caption, HearthVisualSettings.Current.inkSoft, new Vector2(0.5f, 0.5f), new Vector2(0f, -56f), new Vector2(270f, 46f));
        todayReward.textWrappingMode = TextWrappingModes.NoWrap;
        for (int i = 0; i < 7; i++)
            streakDots[i] = RetUi.Img(card.transform, "Dot" + i, RetUi.Art("s_flame_off"), Color.white, new Vector2(0.5f, 0f), new Vector2(-108f + i * 36f, 94f), new Vector2(32f, 32f));
        shieldIcon = RetUi.Img(card.transform, "Shield", RetUi.Art("s_shield"), Color.white, new Vector2(0f, 0f), new Vector2(34f, 40f), new Vector2(46f, 46f));
        todayStreak = HearthStyle.Text(card.transform, "Streak", "", "", TextLevel.Caption, HearthVisualSettings.Current.heading, new Vector2(0.5f, 0f), new Vector2(44f, 40f), new Vector2(200f, 46f), TextAlignmentOptions.Left);
        todayStreak.textWrappingMode = TextWrappingModes.NoWrap;
        // число щитов — цифрой на значке
        shieldCount = HearthStyle.Num(card.transform, "ShieldCount", TextLevel.Caption, HearthVisualSettings.Current.ink, new Vector2(0f, 0f), new Vector2(72f, 40f), new Vector2(30f, 46f));
    }

    private void BuildRight()
    {
        HearthVisualSettings v = HearthVisualSettings.Current;
        Image hearth = HearthStyle.Card(widgets, "HearthButton", new Vector2(1f, 0.5f), new Vector2(-162f, 100f), new Vector2(240f, 250f), CardKind.Window, raycast: true);
        RetUi.HitArea(hearth.gameObject, () => Hearth.Open());
        RetUi.Img(hearth.transform, "Fireplace", RetUi.Art("hearth_fireplace"), Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 34f), new Vector2(140f, 140f));
        TextMeshProUGUI hl = HearthStyle.Text(hearth.transform, "Label", "Очаг", "Hearth", TextLevel.Body, v.heading, new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(220f, 52f));
        hl.fontStyle = FontStyles.Bold;
        hearthSub = HearthStyle.Text(hearth.transform, "Sub", "", "", TextLevel.Caption, v.ink, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(226f, 44f));
        hearthSub.textWrappingMode = TextWrappingModes.NoWrap;

        Image quests = HearthStyle.Card(widgets, "QuestsChip", new Vector2(1f, 0.5f), new Vector2(-162f, -100f), new Vector2(240f, 130f), CardKind.Window, raycast: true);
        RetUi.HitArea(quests.gameObject, () => Diary.Open(0));
        RetUi.Img(quests.transform, "Icon", RetUi.Art("ic_book"), Color.white, new Vector2(0f, 0.5f), new Vector2(50f, 0f), new Vector2(64f, 64f));
        questsLabel = HearthStyle.Text(quests.transform, "Label", "", "", TextLevel.Caption, v.ink, new Vector2(0.5f, 0.5f), new Vector2(36f, 2f), new Vector2(156f, 96f));

        Image ev = HearthStyle.Card(widgets, "EventChip", new Vector2(1f, 0.5f), new Vector2(-162f, -245f), new Vector2(240f, 130f), CardKind.Window, raycast: true);
        eventChip = ev.rectTransform;
        RetUi.HitArea(ev.gameObject, () => Diary.Open(1));
        eventIcon = RetUi.Img(ev.transform, "Icon", RetUi.Art("s_chest"), Color.white, new Vector2(0f, 0.5f), new Vector2(50f, 0f), new Vector2(64f, 64f));
        eventLabel = HearthStyle.Text(ev.transform, "Label", "", "", TextLevel.Caption, v.ink, new Vector2(0.5f, 0.5f), new Vector2(36f, 2f), new Vector2(156f, 96f));

        Image coll = HearthStyle.Card(widgets, "CollectionChip", new Vector2(0f, 0.5f), new Vector2(185f, -310f), new Vector2(290f, 130f), CardKind.Window, raycast: true);
        RetUi.HitArea(coll.gameObject, () => Collection.Open());
        RetUi.Img(coll.transform, "Icon", RetUi.Art("box_06"), Color.white, new Vector2(0f, 0.5f), new Vector2(52f, 0f), new Vector2(56f, 74f));
        TextMeshProUGUI cl = HearthStyle.Text(coll.transform, "Label", "Коробки", "Boxes", TextLevel.Caption, v.ink, new Vector2(0.5f, 0.5f), new Vector2(36f, 2f), new Vector2(190f, 60f));
        cl.fontStyle = FontStyles.Bold;
    }

    private void BuildTeaser()
    {
        Image t = HearthStyle.Sliced(widgets, "Teaser", "ui_tag", Color.white, new Vector2(0.5f, 1f), new Vector2(0f, -610f), new Vector2(660f, 78f));
        teaser = t.rectTransform;
        t.raycastTarget = false;   // надпись-предвкушение, не кнопка (праздники — в «Дневнике» → «Неделя» и «Календарь»)
        teaserIcon = RetUi.Img(t.transform, "Icon", null, new Color(0f, 0f, 0f, 0.6f), new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(60f, 60f));
        teaserText = HearthStyle.Text(t.transform, "Text", "", "", TextLevel.Caption, HearthVisualSettings.Current.ink, new Vector2(0.5f, 0.5f), new Vector2(30f, 1f), new Vector2(560f, 62f));
        teaserText.textWrappingMode = TextWrappingModes.NoWrap;
    }

    /// <summary>Строки «Напоминания: вкл/выкл» и «Время» в карточке настроек (если подключён пакет уведомлений).</summary>
    private void AddReminderSettings()
    {
        if (!C.notifications || !NotificationPlanner.Service.Available) return;
        GameObject panel = GameObject.Find("SettingsPanel");
        Transform card = panel != null ? panel.transform.Find("Card") : null;
        if (card == null) return;
        int insertAt = Mathf.Max(0, card.childCount - 1);   // перед «Назад»
        Button onOff = RetUi.Button(card, "RemindersToggle", "", "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 120f), null, false, 38f);
        Button hour = RetUi.Button(card, "RemindersHour", "", "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 120f), null, false, 38f);
        foreach (Button b in new[] { onOff, hour }) { var le = b.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 120f; le.preferredWidth = 640f; }
        onOff.transform.SetSiblingIndex(insertAt);
        hour.transform.SetSiblingIndex(insertAt + 1);
        void Show()
        {
            RetState s = Core.s;
            int h = s.notifHour >= 0 ? s.notifHour : C.defaultReminderHour;
            RetUi.Set(RetUi.Label(onOff), s.notifEnabled ? "Напоминания: вкл" : "Напоминания: выкл", s.notifEnabled ? "Reminders: on" : "Reminders: off");
            RetUi.Set(RetUi.Label(hour), $"Время напоминания: {h}:00", $"Reminder time: {h}:00");
            hour.interactable = s.notifEnabled;
        }
        onOff.onClick.AddListener(() =>
        {
            RetState s = Core.s;
            s.notifEnabled = !s.notifEnabled;
            if (s.notifEnabled && !NotificationPlanner.Service.HasPermission)
                NotificationPlanner.Service.RequestPermission(ok => { s.notifEnabled = ok; Retention.Instance.Save(); Show(); NotificationPlanner.Reschedule(Core); });
            Retention.Instance.Save();
            NotificationPlanner.Reschedule(Core);
            Show();
        });
        hour.onClick.AddListener(() =>
        {
            RetState s = Core.s;
            int h = s.notifHour >= 0 ? s.notifHour : C.defaultReminderHour;
            h = h + 1 >= C.quietFrom ? C.quietTo : h + 1;   // по кругу в дневных часах 9…21
            s.notifHour = h;
            Retention.Instance.Save();
            NotificationPlanner.Reschedule(Core);
            Show();
        });
        Show();
    }

    // ------------------------------------------------------------------ обновление

    private void Refresh() => RefreshWith(Retention.Active ? Core : null);

    private void RefreshWith(RetentionCore core)
    {
        if (todayStatus == null) return;
        // Мягкий вход: новичок видит простое меню; Очаг и «Сегодня» — после первых уровней, коробки и события — позже
        bool unlocked = core == null || Retention.Instance.Unlocked, extras = core == null || Retention.Instance.ExtrasUnlocked;
        foreach (string n in new[] { "TodayCard", "HearthButton", "QuestsChip" }) { Transform t = widgets.Find(n); if (t != null) t.gameObject.SetActive(unlocked); }
        foreach (string n in new[] { "EventChip", "CollectionChip", "Teaser" }) { Transform t = widgets.Find(n); if (t != null) t.gameObject.SetActive(extras); }
        if (core != null && unlocked) HighlightNew(core.s, extras);
        RetState s = core?.s;
        bool dailyOn = C.dailyLevel && DailyLevelPool.Current != null && DailyLevelPool.Current.entries.Count > 0;
        todayIcon.transform.parent.gameObject.SetActive(dailyOn || C.streak);
        bool done = core != null && core.DailyDoneToday;
        int today = core != null ? core.Today : DayClock.Today;
        todayIcon.sprite = RetUi.Art("box_" + DayClock.Mod(today, 24).ToString("00"));
        todayIcon.color = done ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
        todayCheck.enabled = done;
        bool waiting = core != null && (core.SecretReady || core.CanDoubleDaily);
        RetUi.Set(todayStatus, done ? (waiting ? "Награда ждёт" : "Пройден!") : "Уровень дня", done ? (waiting ? "Reward waiting" : "Done!") : "Daily level");
        if (done) UpdateTimer();
        else RetUi.Set(todayReward, $"+{RetText.EmbersRu(C.dailyWinEmbers)}", $"+{RetText.EmbersEn(C.dailyWinEmbers)}");
        int streak = s?.streak ?? 0;
        int lit = streak <= 0 ? 0 : streak % 7 == 0 ? 7 : streak % 7;
        for (int i = 0; i < 7; i++) streakDots[i].sprite = RetUi.Art(i < lit ? "s_flame_on" : "s_flame_off");
        int shields = s?.shields ?? 0;
        shieldIcon.color = shields > 0 ? Color.white : new Color(1f, 1f, 1f, 0.3f);
        RetUi.Set(todayStreak, $"серия {streak}", $"streak {streak}");   // «новая серия» не помещалась в карточку прежнего размера
        shieldCount.text = shields.ToString();

        int embers = s?.embers ?? 0;
        RetUi.Set(hearthSub, embers > 0 ? RetText.EmbersRu(embers) : (s != null ? C.stages[s.stage].nameRU : "Искра"),
                  embers > 0 ? RetText.EmbersEn(embers) : (s != null ? C.stages[s.stage].nameEN : "Spark"));

        int doneQ = 0, totalQ = 0;
        if (s != null) { totalQ = s.questClaimed.Length; foreach (int q in s.questClaimed) if (q != 0) doneQ++; }
        RetUi.Set(questsLabel, $"Задания\n{doneQ}/{Mathf.Max(3, totalQ)}", $"Tasks\n{doneQ}/{Mathf.Max(3, totalQ)}");

        // Событие: праздник важнее темы недели
        RetSeasonal ev = core != null ? core.ActiveSeasonal(core.Today, out _) : null;
        if (ev != null)
        {
            RetUi.Set(eventLabel, $"Праздник\n{Mathf.Min(s.eventDailyCount, ev.dailyTarget)}/{ev.dailyTarget}", $"Holiday\n{Mathf.Min(s.eventDailyCount, ev.dailyTarget)}/{ev.dailyTarget}");
            eventIcon.sprite = ev.outfit >= 0 && ev.outfit < C.outfits.Count ? RetUi.Art(C.outfits[ev.outfit].sprite) : RetUi.Art("s_gift");
        }
        else if (core != null && core.Theme != null && C.weeklyEvent)
        {
            RetWeekTheme t = core.Theme;
            RetUi.Set(eventLabel, $"Неделя\n{s.weekEventProgress}/{core.ThemeNeeded}", $"Week\n{s.weekEventProgress}/{core.ThemeNeeded}");
            eventIcon.sprite = t.outfit >= 0 && t.outfit < C.outfits.Count ? RetUi.Art(C.outfits[t.outfit].sprite) : RetUi.Art("s_chest");
        }
        else RetUi.Set(eventLabel, "Неделя", "Week");

        // Тизер: ближайший праздник или следующая глава
        bool teaserOn = C.teaser;
        teaser.gameObject.SetActive(teaserOn);
        if (teaserOn)
        {
            RetSeasonal next = NextSeasonal(out int days);
            if (ev != null)
            {
                RetUi.Set(teaserText, $"Праздник: {ev.nameRU}! Костюм за 3 уровня дня", $"{ev.nameEN} is here! Outfit for 3 daily levels");
                teaserIcon.sprite = ev.outfit >= 0 ? RetUi.Art(C.outfits[ev.outfit].sprite) : null;
                teaserIcon.color = Color.white;
            }
            else if (next != null && days <= 30)
            {
                RetUi.Set(teaserText, $"{next.nameRU} через {RetText.Days(days)}", $"{next.nameEN} in {RetText.Days(days)}");
                teaserIcon.sprite = next.outfit >= 0 ? RetUi.Art(C.outfits[next.outfit].sprite) : null;
                teaserIcon.color = new Color(0f, 0f, 0f, 0.7f);   // силуэт костюма — что-то будет
            }
            else if (string.IsNullOrEmpty(C.nextChapterRU))
            {
                teaser.gameObject.SetActive(false);   // ни праздника, ни объявленной главы — без обещаний
            }
            else
            {
                int n = C.chapterList.Count + 1;
                RetUi.Set(teaserText, string.Format(C.nextChapterRU, n), string.Format(C.nextChapterEN, n));
                teaserIcon.sprite = RetUi.Art("s_secret");
                teaserIcon.color = new Color(0f, 0f, 0f, 0.7f);
            }
            teaserIcon.enabled = teaserIcon.sprite != null;
        }
        if (core != null && !startupDone)
        {
            startupDone = true;
            Popups.Startup();
            if (OpenHearthOnEnter) { OpenHearthOnEnter = false; if (!Popups.Busy) Hearth.Open(); }
        }
        else if (core != null) Popups.DrainUnseen();
    }

    /// <summary>Новое показывается один раз: мягкий пульс карточки и пузырь огонька.</summary>
    private void HighlightNew(RetState s, bool extras)
    {
        if ((s.featuresSeen & 1) == 0)
        {
            s.featuresSeen |= 3;
            RetPop.On(widgets.Find("HearthButton")).Pulse(4f);
            RetPop.On(widgets.Find("TodayCard")).Pulse(4f);
            greetedThisSession = true;
            if (greetBubble == null) greetBubble = RetBubble.Create(widgets, new Vector2(0.5f, 0.5f), new Vector2(40f, -150f), 440f);
            greetBubble.Say("Загляни в Очаг — я там живу!", "Come see the Hearth — I live there!", 4.5f);
            Retention.Instance.Save();
        }
        else if (extras && (s.featuresSeen & 4) == 0)
        {
            s.featuresSeen |= 12;
            RetPop.On(widgets.Find("CollectionChip")).Pulse(4f);
            RetPop.On(widgets.Find("EventChip")).Pulse(4f);
            Retention.Instance.Save();
        }
    }

    public RetSeasonal NextSeasonal(out int days)
    {
        days = int.MaxValue;
        RetSeasonal best = null;
        if (!Retention.Active || !C.seasonal) return null;
        DateTime today = DayClock.DateOf(Core.Today < 0 ? DayClock.Today : Core.Today);
        foreach (RetSeasonal e in Core.seasonal)
        {
            DateTime st = e.NextStart(today);
            int d = (int)(st - today).TotalDays;
            if (d >= 0 && d < days) { days = d; best = e; }
        }
        return best;
    }

    private void UpdateTimer()
    {
        TimeSpan left = DayClock.UntilMidnight;
        RetUi.Set(todayReward, $"Новый через {left.Hours} ч {left.Minutes:00} мин", $"New in {left.Hours}h {left.Minutes:00}m");
    }

    /// <summary>«Назад» Android: закрыть верхнее окно «Очага». true — нажатие обработано.</summary>
    public static bool HandleBack()
    {
        RetentionMenuUI u = Instance;
        if (u == null || !u.enabled) return false;
        if (u.Popups.Busy) { u.Popups.CloseCurrent(); return true; }
        if (u.Hearth.SheetOpen) { u.Hearth.CloseSheet(); return true; }
        if (u.Collection.IsOpen) { u.Collection.Close(); return true; }
        if (u.Diary.IsOpen) { u.Diary.Close(); return true; }
        if (u.Daily != null && u.Daily.IsOpen) { u.Daily.Close(); return true; }
        if (u.Hearth.IsOpen) { u.Hearth.Close(); return true; }
        return false;
    }

    private void Update()
    {
        try { Tick(); }
        catch (System.Exception e) { RetSafe.Report("menu update", e); enabled = false; }   // один раз в лог, меню продолжает работать
    }

    private void Tick()
    {
        if (Retention.Active) { Hearth.Tick(); Popups.Tick(); Greet(); }
        if (Time.unscaledTime < nextTick || !Retention.Active) return;
        nextTick = Time.unscaledTime + 1f;
        if (Core.DailyDoneToday) UpdateTimer();
    }

    /// <summary>Приветствие огонька: раз за сессию, коротко, после окон входа; тап закрывает.</summary>
    private float nextGreetCheck;

    private void Greet()
    {
        if (greetedThisSession || Time.unscaledTime < nextGreetCheck) return;
        nextGreetCheck = Time.unscaledTime + 0.5f;   // Unlocked читает PlayerPrefs — не в каждом кадре (R-05)
        HearthVisualSettings v = HearthVisualSettings.Current;
        if (greetedThisSession || !v.greeting || !startupDone || Popups.Busy || Hearth.IsOpen || Time.timeSinceLevelLoad < 1.2f || !Retention.Instance.Unlocked) return;
        greetedThisSession = true;
        if (greetBubble == null) greetBubble = RetBubble.Create(widgets, new Vector2(0.5f, 0.5f), new Vector2(40f, -150f), 440f);
        RetState s = Core.s;
        LineKind k = s.welcomeDay == Core.Today ? LineKind.GreetReturn : s.streak >= 5 ? LineKind.GreetStreak
                   : (LineKind)((int)LineKind.GreetMorning + HearthVisualSettings.TimeOfDay(HearthVisualSettings.Hour));
        greetBubble.Say(FlameLines.Pick(k), v.greetSeconds);
    }

    private void OnToday()
    {
        if (!Retention.Active) return;
        if (Retention.Instance.TodayDailyIndex < 0) { Diary.Open(DiaryScreen.CalendarTab); return; }
        Daily.Open();
    }
}
