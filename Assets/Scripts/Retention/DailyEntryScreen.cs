using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Вход в уровень дня: крупная дата, коробок дня и сложность огоньками с подписью; механики значками с подписями;
/// награда — три строки со значками; дни недели — круглые отметки (сегодня выделен, пройденный с галочкой, будущие приглушены);
/// последние 7 дней — огоньки с датой; «Завтра» одной строкой с отсчётом. «Назад» и «Играть» одной высоты внизу окна.
/// Если уровень уже пройден — «Переиграть» (без награды).
/// </summary>
public class DailyEntryScreen
{
    private readonly RetentionMenuUI ui;
    private RectTransform panel, chips, lower, boxCardRt, mechTitle;
    private TextMeshProUGUI date, status, tierText, tomorrow, rewardNote, noMechanics;
    private readonly Image[] tierFlames = new Image[4];
    private readonly Image[] wdCircle = new Image[7], wdCheck = new Image[7];
    private readonly TextMeshProUGUI[] wdName = new TextMeshProUGUI[7];
    private readonly Image[][] wdFlames = new Image[7][];
    private readonly Image[] histIcon = new Image[7], histCheck = new Image[7];
    private readonly TextMeshProUGUI[] histDate = new TextMeshProUGUI[7];
    private readonly RectTransform[] chip = new RectTransform[5];
    private readonly TextMeshProUGUI[] rewardLines = new TextMeshProUGUI[3];
    private Image rewardCard, boxIcon;
    private Button play;

    private static readonly string[] WdRU = { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" }, WdEN = { "Mo", "Tu", "We", "Th", "Fr", "Sa", "Su" };
    private static readonly string[] WdFullRU = { "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота", "Воскресенье" };
    private static readonly string[] WdFullEN = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
    private static readonly string[] TierRU = { "лёгкий", "средний", "сложный", "особый" }, TierEN = { "easy", "medium", "hard", "special" };
    private static readonly string[] MonRU = { "января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря" };
    private static readonly string[] MonEN = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
    // Механики: флаг, значок, подпись
    private static readonly RetTags[] MechTags = { RetTags.Wind, RetTags.Water, RetTags.Pendulum, RetTags.Immune, RetTags.Gold };
    private static readonly string[] MechIcon = { "ic_fan", "ic_water", "ic_pendulum", "ic_bluematch", "ic_goldmatch" };
    private static readonly string[] MechRU = { "Вентилятор", "Кран", "Маятник", "Голубые спички", "Золото" };
    private static readonly string[] MechEN = { "Fan", "Water tap", "Pendulum", "Blue matches", "Gold" };

    private static RetentionSettings C => RetentionSettings.Current;
    private static HearthVisualSettings V => HearthVisualSettings.Current;
    private static RetentionCore Core => Retention.Instance.core;

    public DailyEntryScreen(RetentionMenuUI ui) { this.ui = ui; }
    public bool IsOpen => panel != null && panel.gameObject.activeSelf;

    public void Open()
    {
        if (panel == null) Build();
        panel.gameObject.SetActive(true);
        panel.SetAsLastSibling();
        LanguageInit.LanguageChanged -= Refresh;
        LanguageInit.LanguageChanged += Refresh;
        Refresh();
    }

    public void Close()
    {
        LanguageInit.LanguageChanged -= Refresh;
        if (panel != null) panel.gameObject.SetActive(false);
        ui.Popups.DrainUnseen();
    }

    private void Build()
    {
        panel = HearthStyle.Window(ui.OverlayRoot, "DailyEntry", "Уровень дня", "Daily level", Close, out RectTransform body);
        // «Назад» и «Играть» рядом, одной высоты
        var close = (RectTransform)panel.Find("Safe/Content/Card/Close");
        close.anchoredPosition = new Vector2(-232f, close.anchoredPosition.y);
        close.sizeDelta = new Vector2(440f, close.sizeDelta.y);
        ((RectTransform)close.Find("Label")).sizeDelta = new Vector2(392f, close.sizeDelta.y - 36f);
        play = HearthStyle.Button(close.parent, "Play", "Играть", "Play", new Vector2(0.5f, 0f), new Vector2(232f, close.anchoredPosition.y), 440f, Play);

        float w = body.rect.width > 0f ? body.rect.width : V.windowWidth - 2f * V.padding;
        Vector2 top = new Vector2(0.5f, 1f);
        date = HearthStyle.Text(body, "Date", "", "", TextLevel.Body, V.inkSoft, top, new Vector2(0f, -26f), new Vector2(w, 52f));

        // Коробок дня и сложность
        Image boxCard = HearthStyle.Card(body, "BoxCard", top, new Vector2(0f, -66f - 115f), new Vector2(w, 230f), CardKind.Accent);
        boxCardRt = boxCard.rectTransform;
        boxIcon = RetUi.Img(boxCard.transform, "Box", RetUi.Art("box_00"), Color.white, new Vector2(0f, 0.5f), new Vector2(120f, 0f), new Vector2(150f, 200f));
        status = HearthStyle.Text(boxCard.transform, "Status", "", "", TextLevel.Subtitle, V.heading, new Vector2(0f, 1f), new Vector2(240f + 320f, -58f), new Vector2(640f, 60f), TextAlignmentOptions.Left);
        HearthStyle.Text(boxCard.transform, "TierTitle", "Сложность:", "Difficulty:", TextLevel.Caption, V.inkSoft, new Vector2(0f, 0f), new Vector2(240f + 110f, 66f), new Vector2(220f, 48f), TextAlignmentOptions.Left);
        for (int i = 0; i < 4; i++)
            tierFlames[i] = HearthStyle.Icon(boxCard.transform, "Tier" + i, "s_flame_on", new Vector2(0f, 0f), new Vector2(240f + 240f + i * 50f, 68f), 48f);
        tierText = HearthStyle.Text(boxCard.transform, "Tier", "", "", TextLevel.Caption, V.ink, new Vector2(0f, 0f), new Vector2(240f + 500f + 96f, 66f), new Vector2(190f, 48f), TextAlignmentOptions.Left);
        tierText.fontStyle = FontStyles.Bold;

        // Механики: значки с подписями
        mechTitle = HearthStyle.Text(body, "MechTitle", "Что внутри", "What's inside", TextLevel.Caption, V.inkSoft, top, new Vector2(0f, -318f), new Vector2(w, 46f)).rectTransform;
        chips = RetUi.Rect(body, "Chips", top, top, new Vector2(0.5f, 1f), new Vector2(0f, -346f), new Vector2(w, 80f));
        for (int i = 0; i < chip.Length; i++)
        {
            Image c = HearthStyle.Card(chips, "Chip" + i, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 76f), CardKind.Inner);
            HearthStyle.Icon(c.transform, "Icon", MechIcon[i], new Vector2(0f, 0.5f), new Vector2(36f, 0f), 48f);
            TextMeshProUGUI l = HearthStyle.Text(c.transform, "Label", MechRU[i], MechEN[i], TextLevel.Caption, V.ink, new Vector2(0f, 0.5f), new Vector2(76f + 90f, 1f), new Vector2(180f, 60f), TextAlignmentOptions.Left);
            l.textWrappingMode = TextWrappingModes.NoWrap;
            chip[i] = c.rectTransform;
        }
        noMechanics = HearthStyle.Text(chips, "None", "Обычный уровень, без особых механик", "A regular level, no special mechanics", TextLevel.Caption, V.ink, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, 60f));

        // Ниже механик — блок, который сдвигается, если механик два ряда
        lower = RetUi.Rect(body, "Lower", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 0f));
        body = lower;
        // Награда: три строки со значками
        rewardCard = HearthStyle.Card(body, "Reward", top, new Vector2(0f, -446f - 100f), new Vector2(w, 200f), CardKind.Inner);
        string[] icons = { "s_ember", "ic_coin", "s_star" };
        for (int i = 0; i < 3; i++)
            rewardLines[i] = HearthStyle.IconLine(rewardCard.transform, "Line" + i, icons[i], "", "", new Vector2(0.5f, 1f), new Vector2(0f, -40f - i * 60f), w - 80f);
        rewardNote = HearthStyle.Text(rewardCard.transform, "Note", "", "", TextLevel.Body, V.mint, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w - 60f, 140f));
        rewardNote.fontStyle = FontStyles.Bold;

        // Дни недели: круги ≥ 48 dp, под ними — сложность огоньками
        HearthStyle.Text(body, "WeekTitle", "Сложность по дням недели", "Difficulty by weekday", TextLevel.Caption, V.inkSoft, top, new Vector2(0f, -668f), new Vector2(w, 46f));
        float step = (w - 126f) / 6f;
        for (int i = 0; i < 7; i++)
        {
            float x = -w * 0.5f + 63f + i * step;
            wdCircle[i] = RetUi.Img(body, "Wd" + i, RetUi.Art("ui_circle"), Color.white, top, new Vector2(x, -696f - 63f), new Vector2(126f, 126f));
            RetUi.Img(wdCircle[i].transform, "Ring", RetUi.Art("ui_ring"), new Color(0.72f, 0.5f, 0.32f, 0.9f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(126f, 126f));
            wdName[i] = HearthStyle.Text(wdCircle[i].transform, "Name", WdRU[i], WdEN[i], TextLevel.Caption, V.ink, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(110f, 50f));
            wdName[i].fontStyle = FontStyles.Bold;
            wdCheck[i] = HearthStyle.Icon(wdCircle[i].transform, "Check", "ic_check", new Vector2(1f, 1f), new Vector2(-14f, -14f), 46f);
            wdFlames[i] = new Image[4];
            for (int k = 0; k < 4; k++)
                wdFlames[i][k] = HearthStyle.Icon(body, $"WdF{i}_{k}", "s_flame_on", top, new Vector2(x + (k - 1.5f) * 22f, -696f - 126f - 20f), 24f);
        }

        // Последние 7 дней: огонёк — победа, звезда — 3 звезды, пусто — нейтральный кружок; дата под отметкой
        HearthStyle.Text(body, "HistTitle", "Последние 7 дней", "Last 7 days", TextLevel.Caption, V.inkSoft, top, new Vector2(0f, -882f), new Vector2(w, 46f));
        for (int i = 0; i < 7; i++)
        {
            float x = -w * 0.5f + 63f + i * step;
            histIcon[i] = HearthStyle.Icon(body, "H" + i, "s_flame_on", top, new Vector2(x, -912f - 34f), 68f);
            histCheck[i] = HearthStyle.Icon(histIcon[i].transform, "Check", "ic_check", new Vector2(1f, 0f), new Vector2(-4f, 8f), 32f);
            histDate[i] = HearthStyle.Num(body, "HD" + i, TextLevel.Caption, V.inkSoft, top, new Vector2(x, -912f - 68f - 24f), new Vector2(110f, 46f), TextAlignmentOptions.Center, false);
        }

        // Завтра: одна строка с отсчётом
        Image clock = HearthStyle.Icon(body, "Clock", "ic_clock", top, new Vector2(-w * 0.5f + 28f, -1052f), 48f);
        tomorrow = HearthStyle.Text(body, "Tomorrow", "", "", TextLevel.Caption, V.ink, top, new Vector2(28f, -1052f), new Vector2(w - 64f, 50f), TextAlignmentOptions.Left);
        tomorrow.textWrappingMode = TextWrappingModes.NoWrap;
        clock.transform.SetAsLastSibling();
    }

    private void Refresh()
    {
        if (!IsOpen || !Retention.Active) return;
        RetentionCore core = Core;
        int today = core.Today;
        int idx = Retention.Instance.TodayDailyIndex;
        DateTime d = DayClock.DateOf(today);
        int wd = DayClock.Weekday(today), tier = DailyLevelPool.TierOf(today);
        RetUi.Set(date, $"{WdFullRU[wd]}, {d.Day} {MonRU[d.Month - 1]}", $"{WdFullEN[wd]}, {MonEN[d.Month - 1]} {d.Day}");   // месяц не по локали устройства (R-10)
        boxIcon.sprite = RetUi.Art("box_" + DayClock.Mod(today, 24).ToString("00"));
        bool done = core.DailyDoneToday;
        RetUi.Set(status, done ? "Пройден! Огонёк доволен" : "Новый коробок ждёт", done ? "Done! Your flame is happy" : "A new box is waiting");
        for (int i = 0; i < 4; i++) tierFlames[i].sprite = RetUi.Art(i <= tier ? "s_flame_on" : "ic_flame_empty");
        RetUi.Set(tierText, TierRU[tier], TierEN[tier]);

        // Механики: по флагам уровня, ряд по центру
        RetTags tags = idx >= 0 ? (RetTags)DailyLevelPool.Current.entries[idx].tags : RetTags.None;
        float total = 0f; int shown = 0;
        for (int i = 0; i < chip.Length; i++)
        {
            bool on = (tags & MechTags[i]) != 0;
            chip[i].gameObject.SetActive(on);
            if (!on) continue;
            TextMeshProUGUI l = chip[i].Find("Label").GetComponent<TextMeshProUGUI>();
            var loc = l.GetComponent<LocalizeText>();   // ширина — по более длинному из RU/EN: смена языка не обрезает подпись
            float lw = Mathf.Min(320f, Mathf.Max(l.GetPreferredValues(loc.textRU, 100000f, 100000f).x, l.GetPreferredValues(loc.textEN, 100000f, 100000f).x) + 6f);
            l.rectTransform.sizeDelta = new Vector2(lw, 60f);
            l.rectTransform.anchoredPosition = new Vector2(66f + lw * 0.5f, 1f);
            chip[i].sizeDelta = new Vector2(66f + lw + 16f, 76f);
            total += chip[i].sizeDelta.x; shown++;
        }
        // Ряды: всё в один, если помещается; иначе — два (коробок дня ужимается, блок ниже сдвигается)
        float gap = 14f, maxW = chips.rect.width;
        int rows = total + gap * Mathf.Max(0, shown - 1) <= maxW ? 1 : 2;
        int split = shown;
        if (rows == 2)
        {
            float acc = 0f; int n = 0;
            for (int i = 0; i < chip.Length; i++) { if (!chip[i].gameObject.activeSelf) continue; if (acc + chip[i].sizeDelta.x > (total + gap * (shown - 1)) * 0.5f + 1f && n > 0) break; acc += chip[i].sizeDelta.x + gap; n++; }
            split = n;
        }
        for (int r = 0; r < rows; r++)
        {
            float rowW = 0f; int count = 0, k = 0;
            for (int i = 0; i < chip.Length; i++) { if (!chip[i].gameObject.activeSelf) continue; bool inRow = r == 0 ? k < split : k >= split; if (inRow) { rowW += chip[i].sizeDelta.x; count++; } k++; }
            float x = -(rowW + gap * Mathf.Max(0, count - 1)) * 0.5f;
            k = 0;
            for (int i = 0; i < chip.Length; i++)
            {
                if (!chip[i].gameObject.activeSelf) continue;
                bool inRow = r == 0 ? k < split : k >= split;
                k++;
                if (!inRow) continue;
                chip[i].anchoredPosition = new Vector2(x + chip[i].sizeDelta.x * 0.5f, -r * 86f);
                x += chip[i].sizeDelta.x + gap;
            }
        }
        bool two = rows == 2;
        boxCardRt.sizeDelta = new Vector2(boxCardRt.sizeDelta.x, two ? 200f : 230f);
        boxCardRt.anchoredPosition = new Vector2(0f, two ? -66f - 100f : -66f - 115f);
        boxIcon.rectTransform.sizeDelta = two ? new Vector2(130f, 174f) : new Vector2(150f, 200f);
        mechTitle.anchoredPosition = new Vector2(0f, two ? -288f : -318f);
        chips.anchoredPosition = new Vector2(0f, two ? -316f : -346f);
        lower.anchoredPosition = new Vector2(0f, two ? -56f : 0f);
        noMechanics.gameObject.SetActive(shown == 0);

        // Награда
        for (int i = 0; i < 3; i++) rewardLines[i].transform.parent.gameObject.SetActive(!done);
        rewardNote.gameObject.SetActive(done);
        HearthStyle.SetCard(rewardCard, done ? CardKind.Done : CardKind.Inner);
        if (done) RetUi.Set(rewardNote, "Награда получена\nПереиграть можно — уже без награды", "Reward received\nYou can replay — without a reward");
        else
        {
            RetUi.Set(rewardLines[0], $"+{RetText.EmbersRu(C.dailyWinEmbers)} за победу", $"+{RetText.EmbersEn(C.dailyWinEmbers)} for a win");
            RetUi.Set(rewardLines[1], $"+{RetText.CoinsRu(C.dailyWinCoins)}", $"+{RetText.CoinsEn(C.dailyWinCoins)}");
            RetUi.Set(rewardLines[2], $"+{RetText.EmbersRu(C.daily3StarEmbers)} за 3 звезды", $"+{RetText.EmbersEn(C.daily3StarEmbers)} for 3 stars");
        }

        // Дни недели (текущая неделя): сегодня выделен, пройденные — с галочкой, будущие приглушены
        int weekStart = today - wd;
        for (int i = 0; i < 7; i++)
        {
            int day = weekStart + i;
            int t = DailyLevelPool.TierOf(day);
            bool isToday = day == today, future = day > today, won = (core.HistoryFlags(day) & 1) != 0;
            wdCircle[i].color = isToday ? new Color(1f, 0.8f, 0.45f) : future ? V.mutedTint : won ? V.mintTint : Color.white;
            wdName[i].color = future ? V.inkSoft : V.ink;
            wdCheck[i].enabled = won && !future;
            for (int k = 0; k < 4; k++)
            {
                wdFlames[i][k].sprite = RetUi.Art(k <= t ? "s_flame_on" : "ic_flame_empty");
                wdFlames[i][k].color = future ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
            }
        }

        // Последние 7 дней
        for (int i = 0; i < 7; i++)
        {
            int day = today - 6 + i;
            int f = core.HistoryFlags(day);
            bool won = (f & 1) != 0, star = (f & 2) != 0;
            histIcon[i].sprite = RetUi.Art(star ? "s_star" : won ? "s_flame_on" : "ic_flame_empty");
            histCheck[i].enabled = won;
            histDate[i].text = DayClock.DateOf(day).Day.ToString();
            histDate[i].color = day == today ? V.ink : V.inkSoft;
        }

        int tt = DailyLevelPool.TierOf(today + 1);
        TimeSpan left = DayClock.UntilMidnight;
        RetUi.Set(tomorrow, $"Завтра: {TierRU[tt]} коробок · через {left.Hours} ч {left.Minutes:00} мин", $"Tomorrow: {TierEN[tt]} box · in {left.Hours}h {left.Minutes:00}m");
        RetUi.Set(RetUi.Label(play), done ? "Переиграть" : "Играть", done ? "Replay" : "Play");
        play.interactable = idx >= 0;
    }

    private void Play() { Close(); Retention.Instance.StartDaily(); }
}
