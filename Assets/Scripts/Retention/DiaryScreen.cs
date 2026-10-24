using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// «Дневник», три вкладки одной ширины. «Сегодня» — три задания карточками на всю ширину (значок дерева, заголовок до 2 строк,
/// полоса с числом, награда; замена — маленькой кнопкой, только когда доступна; выполненное — светлая карточка с галочкой
/// и «Забрать»: награда уже начислена, кнопка только показывает её). «Неделя» — дорожка 4/8/12, событие недели, праздник,
/// секретный коробок с двумя полосами условия. «Календарь» — месяц клетками (сегодня выделен, дни с победой — огонёк,
/// пропуск — нейтрально), легенда в одну строку, серия и щиты, ближайшие праздники. Длинные вкладки прокручиваются.
/// </summary>
public class DiaryScreen
{
    private readonly RetentionMenuUI ui;
    private RectTransform panel, todayTab, weekTab, calTab, weekContent, calContent;
    private int tab;
    private Button[] tabs;
    // Сегодня
    private readonly Image[] questCard = new Image[3], questFuel = new Image[3], questCheck = new Image[3];
    private readonly TextMeshProUGUI[] questText = new TextMeshProUGUI[3], questReward = new TextMeshProUGUI[3];
    private readonly RetBar[] questBar = new RetBar[3];
    private readonly RectTransform[] questRewardPill = new RectTransform[3], swap = new RectTransform[3];
    private readonly Button[] collect = new Button[3];
    private TextMeshProUGUI allText;
    private RetBar allBar;
    // Неделя
    private TextMeshProUGUI weekTitle, eventTitle, eventText, eventReward, seasonTitle, seasonText, seasonDeadline, secretTitle, secretText;
    private Image[] weekNodes;
    private RetBar eventBar, seasonBar, secretStreakBar, secretWeekBar;
    private Image eventIcon, seasonIcon, secretIcon;
    private RectTransform seasonCard, secretCard;
    private Button secretButton;
    // Календарь
    private TextMeshProUGUI calTitle, streakText, shieldText, monthTitle, eventsText;
    private Image[] shieldIcons;
    private Button restoreButton;
    private Image[] dayCells, dayIcons, dayRings;
    private TextMeshProUGUI[] dayNums;
    private RectTransform legendRt, eventsTitleRt, eventsCardRt;
    private float gridTop;
    private readonly RectTransform[] legendIcons = new RectTransform[4], legendTexts = new RectTransform[4];

    /// <summary>Вкладка «Календарь» (из Очага и с карточки «Сегодня», когда уровня дня нет).</summary>
    public const int CalendarTab = 2;

    private static readonly string[] MonRU = { "Январь", "Февраль", "Март", "Апрель", "Май", "Июнь", "Июль", "Август", "Сентябрь", "Октябрь", "Ноябрь", "Декабрь" };
    private static readonly string[] MonEN = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
    private static readonly string[] MonGenRU = { "января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря" };

    private static RetentionSettings C => RetentionSettings.Current;
    private static HearthVisualSettings V => HearthVisualSettings.Current;
    private static RetentionCore Core => Retention.Instance.core;

    public DiaryScreen(RetentionMenuUI ui) { this.ui = ui; }
    public bool IsOpen => panel != null && panel.gameObject.activeSelf;

    public void Open(int tab)
    {
        this.tab = Mathf.Clamp(tab, 0, 2);
        if (panel == null) Build();
        panel.gameObject.SetActive(true);
        panel.SetAsLastSibling();
        Retention.Changed -= Refresh;
        Retention.Changed += Refresh;
        LanguageInit.LanguageChanged -= Refresh;
        LanguageInit.LanguageChanged += Refresh;
        Refresh();
    }

    public void Close()
    {
        Retention.Changed -= Refresh;
        LanguageInit.LanguageChanged -= Refresh;
        if (panel != null) panel.gameObject.SetActive(false);
        ui.Popups.DrainUnseen();
    }

    private void Build()
    {
        panel = HearthStyle.Window(ui.OverlayRoot, "DiaryScreen", "Дневник", "Diary", Close, out RectTransform body);
        float w = body.rect.width;
        string[] ru = { "Сегодня", "Неделя", "Календарь" }, en = { "Today", "Week", "Calendar" };
        tabs = new Button[3];
        float tw = (w - 2f * 16f) / 3f;
        for (int k = 0; k < 3; k++)
        {
            int kk = k;
            tabs[k] = HearthStyle.Button(body, "Tab" + k, ru[k], en[k], new Vector2(0.5f, 1f), new Vector2((k - 1) * (tw + 16f), -66f), tw, () => { tab = kk; Refresh(); }, ButtonKind.Secondary, 132f, TextLevel.Caption);
        }
        todayTab = RetUi.Rect(body, "Today", Vector2.zero, Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -152f), new Vector2(0f, -152f));
        weekTab = RetUi.Rect(body, "Week", Vector2.zero, Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -152f), new Vector2(0f, -152f));
        calTab = RetUi.Rect(body, "Calendar", Vector2.zero, Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -152f), new Vector2(0f, -152f));
        BuildToday(w);
        weekContent = HearthStyle.Scroll(weekTab);
        BuildWeek(w);
        calContent = HearthStyle.Scroll(calTab);
        BuildCalendar(w);
    }

    // ------------------------------------------------------------------ Сегодня

    private void BuildToday(float w)
    {
        Vector2 top = new Vector2(0.5f, 1f);
        const float H = 176f;
        for (int i = 0; i < 3; i++)
        {
            int ii = i;
            float y = -i * (H + 18f) - H * 0.5f;
            Image card = HearthStyle.Card(todayTab, "Quest" + i, top, new Vector2(0f, y), new Vector2(w, H), CardKind.Inner);
            questCard[i] = card;
            RetUi.Img(card.transform, "IconBg", RetUi.Art("ui_circle"), new Color(1f, 0.93f, 0.82f), new Vector2(0f, 0.5f), new Vector2(66f, 0f), new Vector2(96f, 96f));
            questFuel[i] = HearthStyle.Icon(card.transform, "Fuel", "fuel_0", new Vector2(0f, 0.5f), new Vector2(66f, 0f), 66f);
            questText[i] = HearthStyle.Text(card.transform, "Text", "", "", TextLevel.Body, V.ink, new Vector2(0f, 1f), new Vector2(134f + 230f, -54f), new Vector2(460f, 96f), TextAlignmentOptions.Left);
            questBar[i] = HearthStyle.Bar(card.transform, "Bar", new Vector2(0f, 0f), new Vector2(134f + 230f, 36f), new Vector2(460f, 44f));
            Image pill = HearthStyle.Sliced(card.transform, "Reward", "ui_tag", Color.white, new Vector2(1f, 0.5f), new Vector2(-152f - 80f, 0f), new Vector2(150f, 64f));
            HearthStyle.Icon(pill.transform, "Icon", "s_ember", new Vector2(0f, 0.5f), new Vector2(30f, 0f), 44f);
            questReward[i] = HearthStyle.Num(pill.transform, "Value", TextLevel.Caption, V.ink, new Vector2(0f, 0.5f), new Vector2(100f, 2f), new Vector2(90f, 50f));
            questRewardPill[i] = pill.rectTransform;
            // Замена: маленький значок в углу, зона касания 48 dp
            RectTransform sw = RetUi.Rect(card.transform, "Swap", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-12f - 66f, 0f), new Vector2(132f, 132f));
            RetUi.HitArea(sw.gameObject, () => Reroll(ii));
            RetUi.Img(sw, "Bg", RetUi.Art("ui_circle"), new Color(1f, 0.93f, 0.82f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 84f));
            HearthStyle.Icon(sw, "Icon", "ic_swap", new Vector2(0.5f, 0.5f), Vector2.zero, 60f);
            swap[i] = sw;
            questCheck[i] = HearthStyle.Icon(card.transform, "Check", "ic_check", new Vector2(1f, 0.5f), new Vector2(-12f - 66f, 0f), 72f);
            collect[i] = HearthStyle.Button(card.transform, "Collect", "Забрать", "Collect", new Vector2(1f, 0.5f), new Vector2(-24f - 120f, 0f), 240f, () => Collect(ii), ButtonKind.Done, 132f, TextLevel.Caption);
        }
        float yb = -3f * (H + 18f) - 10f;
        Image all = HearthStyle.Card(todayTab, "All", top, new Vector2(0f, yb - 70f), new Vector2(w, 140f), CardKind.Accent);
        allText = HearthStyle.Text(all.transform, "Text", "", "", TextLevel.Caption, V.ink, new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(w - 60f, 50f));
        allText.fontStyle = FontStyles.Bold;
        allBar = HearthStyle.Bar(all.transform, "Bar", new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(w - 72f, 44f));
        HearthStyle.Text(todayTab, "Wood", "Дерево задания добавляет огоньку свой аромат", "Each task's wood adds its aroma to your flame", TextLevel.Caption, V.inkSoft, top,
            new Vector2(0f, yb - 140f - 50f), new Vector2(w, 50f));
    }

    private void Reroll(int i)
    {
        if (Retention.Instance.RerollQuest(i)) RetPop.On(questCard[i]).Pop();
        else ui.Popups.Toast("Заменить можно одно задание в день", "You can swap one task a day");
    }

    /// <summary>«Забрать»: награда уже на счету — показываем её (пружинка) и запоминаем, что карточку видели.</summary>
    private void Collect(int i)
    {
        RetState s = Core.s;
        s.questsShownMask |= 1 << i;
        RetPop.On(questCard[i]).Pop();
        HearthAudio.Reward();
        Retention.Instance.Save();
        Refresh();
    }

    // ------------------------------------------------------------------ Неделя

    private void BuildWeek(float w)
    {
        Vector2 top = new Vector2(0.5f, 1f);
        float y = 0f;
        // Дорожка недели
        Image week = HearthStyle.Card(weekContent, "Track", top, new Vector2(0f, y - 120f), new Vector2(w, 240f), CardKind.Inner);
        weekTitle = HearthStyle.Text(week.transform, "Title", "", "", TextLevel.Body, V.ink, new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(w - 60f, 56f));
        weekTitle.fontStyle = FontStyles.Bold;
        int n = Mathf.Max(1, C.weeklyGoalWins);
        weekNodes = new Image[n];
        float span = w - 140f;
        Image line = RetUi.Img(week.transform, "Line", RetUi.Art("ui_divider"), new Color(1f, 1f, 1f, 0.9f), new Vector2(0.5f, 0f), new Vector2(0f, 108f), new Vector2(span + 40f, 10f));
        line.preserveAspect = false;
        for (int k = 0; k < n; k++)
        {
            float x = -span * 0.5f + k * (span / Mathf.Max(1, n - 1));
            bool step = Array.IndexOf(C.weekSteps, k + 1) >= 0 || k + 1 == n;
            weekNodes[k] = RetUi.Img(week.transform, "Node" + k, RetUi.Art(step ? (k + 1 == n ? "s_chest" : "s_ember") : "ui_circle"), Color.white, new Vector2(0.5f, 0f),
                new Vector2(x, 108f), step ? new Vector2(76f, 76f) : new Vector2(30f, 30f));
            if (step)
                HearthStyle.Num(week.transform, "Label" + k, TextLevel.Caption, V.inkSoft, new Vector2(0.5f, 0f), new Vector2(x, 44f), new Vector2(90f, 46f)).text = (k + 1).ToString();
        }
        y -= 240f + 20f;

        // Событие недели: заголовок, условие (до 2 строк), награда, полоса
        Image ev = HearthStyle.Card(weekContent, "Event", top, new Vector2(0f, y - 145f), new Vector2(w, 290f), CardKind.Inner);
        eventIcon = HearthStyle.Icon(ev.transform, "Icon", "s_star", new Vector2(0f, 1f), new Vector2(76f, -76f), 104f);
        eventTitle = HearthStyle.Text(ev.transform, "Title", "", "", TextLevel.Body, V.heading, new Vector2(0f, 1f), new Vector2(150f + 360f, -46f), new Vector2(720f, 56f), TextAlignmentOptions.Left);
        eventTitle.fontStyle = FontStyles.Bold;
        eventText = HearthStyle.Text(ev.transform, "Text", "", "", TextLevel.Caption, V.ink, new Vector2(0f, 1f), new Vector2(150f + 360f, -118f), new Vector2(720f, 92f), TextAlignmentOptions.TopLeft);
        eventReward = HearthStyle.Text(ev.transform, "Reward", "", "", TextLevel.Caption, V.inkSoft, new Vector2(0f, 1f), new Vector2(150f + 360f, -188f), new Vector2(720f, 48f), TextAlignmentOptions.Left);
        eventBar = HearthStyle.Bar(ev.transform, "Bar", new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(w - 72f, 44f));
        y -= 290f + 20f;

        // Праздник: заголовок, условие (до 2 строк), срок, полоса
        Image season = HearthStyle.Card(weekContent, "Season", top, new Vector2(0f, y - 145f), new Vector2(w, 290f), CardKind.Accent);
        seasonCard = season.rectTransform;
        seasonIcon = HearthStyle.Icon(season.transform, "Icon", "s_gift", new Vector2(0f, 1f), new Vector2(76f, -76f), 104f);
        seasonTitle = HearthStyle.Text(season.transform, "Title", "", "", TextLevel.Body, V.heading, new Vector2(0f, 1f), new Vector2(150f + 360f, -46f), new Vector2(720f, 56f), TextAlignmentOptions.Left);
        seasonTitle.fontStyle = FontStyles.Bold;
        seasonText = HearthStyle.Text(season.transform, "Text", "", "", TextLevel.Caption, V.ink, new Vector2(0f, 1f), new Vector2(150f + 360f, -118f), new Vector2(720f, 92f), TextAlignmentOptions.TopLeft);
        seasonDeadline = HearthStyle.Text(season.transform, "Deadline", "", "", TextLevel.Caption, V.inkSoft, new Vector2(0f, 1f), new Vector2(150f + 360f, -188f), new Vector2(720f, 48f), TextAlignmentOptions.Left);
        seasonBar = HearthStyle.Bar(season.transform, "Bar", new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(w - 72f, 44f));
        y -= 290f + 20f;

        // Секретный коробок: условие и две полосы (серия ИЛИ неделя)
        Image secret = HearthStyle.Card(weekContent, "Secret", top, new Vector2(0f, y - 170f), new Vector2(w, 340f), CardKind.Inner);
        secretCard = secret.rectTransform;
        secretIcon = HearthStyle.Icon(secret.transform, "Icon", "ic_secret", new Vector2(0f, 1f), new Vector2(80f, -80f), 112f);
        secretTitle = HearthStyle.Text(secret.transform, "Title", "Секретный коробок", "Secret box", TextLevel.Body, V.heading, new Vector2(0f, 1f), new Vector2(156f + 280f, -46f), new Vector2(560f, 56f), TextAlignmentOptions.Left);
        secretTitle.fontStyle = FontStyles.Bold;
        secretText = HearthStyle.Text(secret.transform, "Text", "", "", TextLevel.Caption, V.ink, new Vector2(0f, 1f), new Vector2(156f + 360f, -118f), new Vector2(720f, 96f), TextAlignmentOptions.Left);
        HearthStyle.Text(secret.transform, "StreakLabel", "Серия", "Streak", TextLevel.Caption, V.inkSoft, new Vector2(0f, 0f), new Vector2(36f + 90f, 114f), new Vector2(180f, 46f), TextAlignmentOptions.Left);
        secretStreakBar = HearthStyle.Bar(secret.transform, "StreakBar", new Vector2(0f, 0f), new Vector2(230f + (w - 266f) * 0.5f, 114f), new Vector2(w - 266f, 44f));
        HearthStyle.Text(secret.transform, "WeekLabel", "Неделя", "Week", TextLevel.Caption, V.inkSoft, new Vector2(0f, 0f), new Vector2(36f + 90f, 50f), new Vector2(180f, 46f), TextAlignmentOptions.Left);
        secretWeekBar = HearthStyle.Bar(secret.transform, "WeekBar", new Vector2(0f, 0f), new Vector2(230f + (w - 266f) * 0.5f, 50f), new Vector2(w - 266f, 44f));
        secretButton = HearthStyle.Button(secret.transform, "Open", "Открыть", "Open", new Vector2(1f, 1f), new Vector2(-24f - 140f, -24f - 66f), 280f, OpenSecret, ButtonKind.Primary, 132f, TextLevel.Caption);
        y -= 340f + 24f;
        weekContent.sizeDelta = new Vector2(0f, -y);
    }

    private void OpenSecret()
    {
        RetPop.On(secretIcon).ShakeThenPop();
        Retention.Instance.OpenSecret();
    }

    // ------------------------------------------------------------------ Календарь

    private void BuildCalendar(float w)
    {
        Vector2 top = new Vector2(0.5f, 1f);
        float y = 0f;
        // Серия и щиты: заголовок, «лучшая · щиты» одной строкой, пояснение (или «Вернуть серию», когда можно)
        Image st = HearthStyle.Card(calContent, "Streak", top, new Vector2(0f, y - 145f), new Vector2(w, 290f), CardKind.Accent);
        calTitle = HearthStyle.Text(st.transform, "Title", "", "", TextLevel.Body, V.ink, new Vector2(0f, 1f), new Vector2(36f + 280f, -46f), new Vector2(560f, 56f), TextAlignmentOptions.Left);
        calTitle.fontStyle = FontStyles.Bold;
        shieldIcons = new Image[Mathf.Max(1, C.shieldMax)];
        for (int i = 0; i < shieldIcons.Length; i++)
            shieldIcons[i] = HearthStyle.Icon(st.transform, "Shield" + i, "s_shield", new Vector2(1f, 1f), new Vector2(-48f - i * 64f, -46f), 56f);
        shieldText = HearthStyle.Text(st.transform, "Shields", "", "", TextLevel.Caption, V.inkSoft, new Vector2(0f, 1f), new Vector2(36f + 400f, -100f), new Vector2(800f, 48f), TextAlignmentOptions.Left);
        streakText = HearthStyle.Text(st.transform, "Note", "", "", TextLevel.Caption, V.ink, new Vector2(0f, 1f), new Vector2(36f + (w - 72f) * 0.5f, -186f), new Vector2(w - 72f, 96f), TextAlignmentOptions.TopLeft);
        restoreButton = HearthStyle.Button(st.transform, "Restore", "Вернуть серию (видео)", "Restore streak (video)", new Vector2(0f, 1f), new Vector2(36f + 230f, -140f - 66f), 460f,
            () => Retention.Instance.RestoreStreakWithAd(), ButtonKind.Secondary, 132f, TextLevel.Caption);
        y -= 290f + 24f;

        // Месяц
        monthTitle = HearthStyle.Text(calContent, "Month", "", "", TextLevel.Body, V.heading, top, new Vector2(0f, y - 30f), new Vector2(w, 56f));
        monthTitle.fontStyle = FontStyles.Bold;
        y -= 66f;
        string[] wdRU = { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" }, wdEN = { "Mo", "Tu", "We", "Th", "Fr", "Sa", "Su" };
        float cell = 126f, step = (w - cell) / 6f;
        for (int k = 0; k < 7; k++)
            HearthStyle.Text(calContent, "WD" + k, wdRU[k], wdEN[k], TextLevel.Caption, V.inkSoft, top, new Vector2(-w * 0.5f + cell * 0.5f + k * step, y - 24f), new Vector2(110f, 46f));
        y -= 52f;
        gridTop = y;
        dayCells = new Image[42]; dayNums = new TextMeshProUGUI[42]; dayIcons = new Image[42]; dayRings = new Image[42];
        for (int i = 0; i < 42; i++)
        {
            float x = -w * 0.5f + cell * 0.5f + (i % 7) * step, cy = y - cell * 0.5f - (i / 7) * (cell + 8f);
            dayCells[i] = HearthStyle.Card(calContent, "Day" + i, top, new Vector2(x, cy), new Vector2(cell, cell), CardKind.Inner);
            dayRings[i] = RetUi.Img(dayCells[i].transform, "Today", RetUi.Art("ui_ring"), new Color(0.97f, 0.59f, 0.18f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(66f, 66f));
            dayNums[i] = HearthStyle.Num(dayCells[i].transform, "Num", TextLevel.Caption, V.ink, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(110f, 46f));
            dayIcons[i] = HearthStyle.Icon(dayCells[i].transform, "Icon", "s_flame_on", new Vector2(0.5f, 0f), new Vector2(0f, 36f), 52f);
        }
        // Легенда в одну строку: значок + слово (позиции — в Refresh, по числу недель месяца и ширине слов)
        legendRt = RetUi.Rect(calContent, "Legend", top, top, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(w, 56f));
        string[] li = { "s_flame_on", "s_star", "s_shield", "s_gift" };
        string[] lRU = { "победа", "3 звезды", "щит", "праздник" }, lEN = { "win", "3 stars", "shield", "holiday" };
        for (int k = 0; k < 4; k++)
        {
            legendIcons[k] = HearthStyle.Icon(legendRt, "LI" + k, li[k], new Vector2(0.5f, 0.5f), Vector2.zero, 44f).rectTransform;
            TextMeshProUGUI t = HearthStyle.Text(legendRt, "LT" + k, lRU[k], lEN[k], TextLevel.Caption, V.inkSoft, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170f, 46f), TextAlignmentOptions.Left);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            legendTexts[k] = t.rectTransform;
        }
        // Ближайшие праздники
        TextMeshProUGUI et = HearthStyle.Text(calContent, "EventsTitle", "Ближайшие праздники", "Upcoming holidays", TextLevel.Body, V.heading, top, Vector2.zero, new Vector2(w, 56f));
        et.fontStyle = FontStyles.Bold;
        eventsTitleRt = et.rectTransform;
        Image evCard = HearthStyle.Card(calContent, "Events", top, Vector2.zero, new Vector2(w, 190f), CardKind.Inner);
        eventsCardRt = evCard.rectTransform;
        eventsText = HearthStyle.Text(evCard.transform, "Text", "", "", TextLevel.Caption, V.ink, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w - 60f, 170f));
    }

    /// <summary>Под сеткой месяца (5 или 6 недель): легенда по ширине слов, по центру; затем праздники; высота прокрутки.</summary>
    private void LayoutCalendarTail(int rows)
    {
        float y = gridTop - rows * (126f + 8f) - 10f;
        legendRt.anchoredPosition = new Vector2(0f, y);
        float total = 0f;
        float[] tw = new float[4];
        for (int k = 0; k < 4; k++)
        {
            var t = legendTexts[k].GetComponent<TextMeshProUGUI>();
            var loc = t.GetComponent<LocalizeText>();
            tw[k] = Mathf.Max(t.GetPreferredValues(loc.textRU, 100000f, 100000f).x, t.GetPreferredValues(loc.textEN, 100000f, 100000f).x) + 6f;
            total += 44f + 8f + tw[k];
        }
        float gap = Mathf.Max(16f, (legendRt.rect.width - total) / 3f), x = -legendRt.rect.width * 0.5f;
        if (gap * 3f + total > legendRt.rect.width) gap = 16f;
        x = -(total + gap * 3f) * 0.5f;
        for (int k = 0; k < 4; k++)
        {
            legendIcons[k].anchoredPosition = new Vector2(x + 22f, -28f);
            legendTexts[k].sizeDelta = new Vector2(tw[k], 46f);
            legendTexts[k].anchoredPosition = new Vector2(x + 52f + tw[k] * 0.5f, -28f);
            x += 44f + 8f + tw[k] + gap;
        }
        y -= 56f + 24f;
        eventsTitleRt.anchoredPosition = new Vector2(0f, y - 28f);
        y -= 56f + 12f;
        eventsCardRt.anchoredPosition = new Vector2(0f, y - 95f);
        y -= 190f + 24f;
        calContent.sizeDelta = new Vector2(0f, -y);
    }

    // ------------------------------------------------------------------ обновление

    private void Refresh()
    {
        if (panel == null || !panel.gameObject.activeSelf || !Retention.Active) return;
        todayTab.gameObject.SetActive(tab == 0);
        weekTab.gameObject.SetActive(tab == 1);
        calTab.gameObject.SetActive(tab == 2);
        for (int k = 0; k < 3; k++) HearthStyle.SetButton(tabs[k], k == tab ? ButtonKind.Primary : ButtonKind.Secondary);
        RetentionCore core = Core;
        RetState s = core.s;
        if (tab == 0) RefreshToday(core, s);
        else if (tab == 1) RefreshWeek(core, s);
        else RefreshCalendar(core, s);
    }

    private void RefreshToday(RetentionCore core, RetState s)
    {
        if (s.questsShownDay != core.Today) { s.questsShownDay = core.Today; s.questsShownMask = 0; }
        int doneCount = 0;
        for (int i = 0; i < 3; i++)
        {
            bool has = i < s.questIds.Length;
            questCard[i].gameObject.SetActive(has);
            if (!has) continue;
            RetQuest q = C.questPool[s.questIds[i]];
            int need = RetentionCore.Needed(q);
            bool done = s.questClaimed[i] != 0;
            if (done) doneCount++;
            bool seen = (s.questsShownMask & (1 << i)) != 0;
            string tRU = string.Format(q.textRU, q.target), tEN = string.Format(q.textEN, q.target);
            RetUi.Set(questText[i], done && seen ? tRU + $"\nПолучено +{C.questEmbers}" : tRU, done && seen ? tEN + $"\nReceived +{C.questEmbers}" : tEN);
            questBar[i].Set((float)s.questProgress[i] / Mathf.Max(1, need), $"{Mathf.Min(s.questProgress[i], need)} / {need}");
            questReward[i].text = "+" + C.questEmbers;
            questFuel[i].sprite = RetUi.Art("fuel_" + (int)q.fuel);
            HearthStyle.SetCard(questCard[i], done ? CardKind.Done : CardKind.Inner);
            bool canSwap = !done && core.CanReroll(i);
            swap[i].gameObject.SetActive(canSwap);
            questRewardPill[i].anchoredPosition = new Vector2(canSwap ? -152f - 80f : -24f - 75f, 0f);
            questRewardPill[i].gameObject.SetActive(!done);
            collect[i].gameObject.SetActive(done && !seen);    // награда уже начислена — «Забрать» её показывает
            questCheck[i].enabled = done && seen;
            questBar[i].gameObject.SetActive(!(done && seen));
        }
        RetUi.Set(allText, s.questAllClaimed ? $"Все три задания выполнены: +{RetText.EmbersRu(C.questAllBonus)} получено" : $"Все три задания — ещё +{RetText.EmbersRu(C.questAllBonus)}",
                  s.questAllClaimed ? $"All three tasks done: +{RetText.EmbersEn(C.questAllBonus)} received" : $"All three tasks: +{RetText.EmbersEn(C.questAllBonus)} more");
        allBar.Set(doneCount / 3f, $"{doneCount} / 3");
    }

    private void RefreshWeek(RetentionCore core, RetState s)
    {
        RetUi.Set(weekTitle, s.weekGoalClaimed ? "Неделя выполнена! Новая — с понедельника" : $"Неделя: {s.weekWins} из {C.weeklyGoalWins} побед",
                  s.weekGoalClaimed ? "Week complete! A new one starts on Monday" : $"Week: {s.weekWins} of {C.weeklyGoalWins} wins");
        for (int k = 0; k < weekNodes.Length; k++)
        {
            bool reached = s.weekWins > k;
            bool step = Array.IndexOf(C.weekSteps, k + 1) >= 0 || k + 1 == weekNodes.Length;
            weekNodes[k].color = step ? (reached ? Color.white : new Color(1f, 1f, 1f, 0.45f)) : reached ? new Color(1f, 0.6f, 0.2f) : new Color(0.84f, 0.72f, 0.58f);
        }

        RetWeekTheme t = core.Theme;
        eventTitle.transform.parent.gameObject.SetActive(t != null && C.weeklyEvent);
        if (t != null)
        {
            RetUi.Set(eventTitle, t.nameRU, t.nameEN);
            RetUi.Set(eventText, string.Format(t.textRU, t.target), string.Format(t.textEN, t.target));
            RetUi.Set(eventReward, t.outfit >= 0 ? $"Награда: облик «{C.outfits[t.outfit].nameRU}»" : $"Награда: +{RetText.EmbersRu(C.weeklyEventEmbers)}",
                                   t.outfit >= 0 ? $"Reward: the «{C.outfits[t.outfit].nameEN}» outfit" : $"Reward: +{RetText.EmbersEn(C.weeklyEventEmbers)}");
            eventBar.Set((float)s.weekEventProgress / core.ThemeNeeded, s.weekEventClaimed ? RetText.T("готово", "done") : $"{s.weekEventProgress} / {core.ThemeNeeded}");
            eventIcon.sprite = t.outfit >= 0 ? RetUi.Art(C.outfits[t.outfit].sprite) : RetUi.Art("s_star");
        }

        RetSeasonal ev = core.ActiveSeasonal(core.Today, out string key);
        bool seasonOn = C.seasonal && Retention.Instance.ExtrasUnlocked;
        if (ev != null)
        {
            bool got = Array.IndexOf(s.eventClaimed, key) >= 0;
            int have = Mathf.Min(s.eventDailyCount, ev.dailyTarget);
            RetUi.Set(seasonTitle, $"Праздник: {ev.nameRU}", $"Holiday: {ev.nameEN}");
            RetUi.Set(seasonText, ev.textRU, ev.textEN);
            bool hasEnd = RetSeasonal.TryParse(ev.end, out int em, out int ed);
            RetUi.Set(seasonDeadline, got ? "Подарок получен" : hasEnd ? $"До {ed} {MonGenRU[em - 1]}" : "", got ? "Gift received" : hasEnd ? $"Until {MonEN[em - 1]} {ed}" : "");
            seasonBar.gameObject.SetActive(true);
            seasonBar.Set((float)have / Mathf.Max(1, ev.dailyTarget), $"{have} / {ev.dailyTarget}");
            seasonIcon.sprite = ev.outfit >= 0 ? RetUi.Art(C.outfits[ev.outfit].sprite) : RetUi.Art("s_gift");
            seasonIcon.color = Color.white;
        }
        else
        {
            RetSeasonal next = ui.NextSeasonal(out int days);
            seasonOn &= next != null;
            if (next != null)
            {
                DateTime when = DayClock.DateOf(core.Today + days);
                RetUi.Set(seasonTitle, $"Скоро: {next.nameRU}", $"Soon: {next.nameEN}");
                RetUi.Set(seasonText, next.textRU, next.textEN);
                RetUi.Set(seasonDeadline, $"С {when.Day} {MonGenRU[when.Month - 1]} — через {RetText.DaysRu(days)}", $"From {MonEN[when.Month - 1]} {when.Day} — in {(days == 1 ? "1 day" : days + " days")}");
                seasonBar.gameObject.SetActive(false);
                seasonIcon.sprite = next.outfit >= 0 ? RetUi.Art(C.outfits[next.outfit].sprite) : RetUi.Art("s_gift");
                seasonIcon.color = new Color(1f, 1f, 1f, 0.55f);
            }
        }
        seasonCard.gameObject.SetActive(seasonOn);

        secretCard.gameObject.SetActive(C.secretBox);
        bool visible = core.SecretVisible;
        secretStreakBar.Set((float)Mathf.Min(s.streak, C.secretStreak) / Mathf.Max(1, C.secretStreak), $"{Mathf.Min(s.streak, C.secretStreak)} / {C.secretStreak}");
        secretWeekBar.Set((float)Mathf.Min(s.weekWins, C.weeklyGoalWins) / Mathf.Max(1, C.weeklyGoalWins), $"{Mathf.Min(s.weekWins, C.weeklyGoalWins)} / {C.weeklyGoalWins}");
        if (visible)
        {
            int daysLeft = Mathf.Max(0, s.secretOpenDay - core.Today);
            if (core.SecretReady) RetUi.Set(secretText, "Готов! Внутри что-то особенное", "Ready! Something special inside");
            else RetUi.Set(secretText,
                $"Откроется {(daysLeft > 0 ? "через " + RetText.DaysRu(daysLeft) : "сегодня")}, если набрать серию или неделю{(core.SecretConditionMet ? " — условие выполнено!" : "")}. Он подождёт тебя.",
                $"Opens {(daysLeft > 0 ? "in " + (daysLeft == 1 ? "1 day" : daysLeft + " days") : "today")} with a streak or a full week{(core.SecretConditionMet ? " — condition met!" : "")}. It will wait for you.");
            secretButton.gameObject.SetActive(core.SecretReady);
            secretIcon.color = core.SecretReady ? Color.white : new Color(1f, 1f, 1f, 0.8f);
        }
        else
        {
            RetUi.Set(secretText, "Новый секретный коробок придёт в понедельник", "A new secret box arrives on Monday");
            secretButton.gameObject.SetActive(false);
        }
    }

    private void RefreshCalendar(RetentionCore core, RetState s)
    {
        RetUi.Set(calTitle, s.streak > 0 ? $"Серия: {RetText.DaysRu(s.streak)}" : "Серия: пока нет", s.streak > 0 ? $"Streak: {(s.streak == 1 ? "1 day" : s.streak + " days")}" : "Streak: not yet");
        for (int i = 0; i < shieldIcons.Length; i++) shieldIcons[i].color = i < s.shields ? Color.white : new Color(1f, 1f, 1f, 0.3f);
        RetUi.Set(shieldText, $"Лучшая: {s.bestStreak} · щиты: {s.shields} из {C.shieldMax}", $"Best: {s.bestStreak} · shields: {s.shields} of {C.shieldMax}");
        bool restore = core.CanRestoreStreak;
        RetUi.Set(streakText, s.streak > 0 ? "Щит сам закроет пропущенный день. Новый — каждый понедельник" : "Победа в любом уровне начнёт серию. Щит закроет пропущенный день",
                  s.streak > 0 ? "A shield covers a missed day by itself. A new one every Monday" : "A win in any level starts a streak. A shield covers a missed day");
        streakText.gameObject.SetActive(!restore);
        restoreButton.gameObject.SetActive(restore);

        DateTime today = DayClock.DateOf(core.Today);
        RetUi.Set(monthTitle, $"{MonRU[today.Month - 1]} {today.Year}", $"{MonEN[today.Month - 1]} {today.Year}");
        int first = core.Today - (today.Day - 1);
        int offset = DayClock.Weekday(first);
        int daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
        LayoutCalendarTail((offset + daysInMonth + 6) / 7);
        for (int i = 0; i < 42; i++)
        {
            int dn = i - offset;
            bool inMonth = dn >= 0 && dn < daysInMonth;
            dayCells[i].gameObject.SetActive(inMonth);
            if (!inMonth) continue;
            int day = first + dn;
            int f = core.HistoryFlags(day);
            dayNums[i].text = (dn + 1).ToString();
            bool future = day > core.Today, isToday = day == core.Today;
            RetSeasonal ev = core.ActiveSeasonal(day, out _);
            string icon = (f & 2) != 0 ? "s_star" : (f & 1) != 0 ? "s_flame_on" : (f & 4) != 0 ? "s_shield" : ev != null ? "s_gift" : null;
            dayIcons[i].sprite = icon != null ? RetUi.Art(icon) : null;
            dayIcons[i].enabled = icon != null;
            dayIcons[i].color = future ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
            dayRings[i].enabled = isToday;
            HearthStyle.SetCard(dayCells[i], isToday ? CardKind.Accent : future ? CardKind.Muted : CardKind.Inner);   // пропуск — нейтрально, без красного
            dayNums[i].color = future ? V.inkSoft : V.ink;
        }

        var sb = new System.Text.StringBuilder();
        var sbEn = new System.Text.StringBuilder();
        var list = new System.Collections.Generic.List<(int, RetSeasonal)>();
        foreach (RetSeasonal e in core.seasonal) list.Add(((int)(e.NextStart(today) - today).TotalDays, e));
        list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        for (int i = 0; i < Mathf.Min(3, list.Count); i++)
        {
            var (d, e) = list[i];
            DateTime when = today.AddDays(d);
            if (i > 0) { sb.Append('\n'); sbEn.Append('\n'); }
            sb.Append($"{e.nameRU} — {when.Day} {MonGenRU[when.Month - 1]} ({(d == 0 ? "сегодня" : "через " + RetText.DaysRu(d))})");
            sbEn.Append($"{e.nameEN} — {MonEN[when.Month - 1]} {when.Day} ({(d == 0 ? "today" : "in " + d + (d == 1 ? " day" : " days"))})");
        }
        RetUi.Set(eventsText, sb.ToString(), sbEn.ToString());
    }
}
