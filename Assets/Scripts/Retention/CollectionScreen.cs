using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Коллекция спичечных коробков: «Собрано N из 32» с полосой и кнопкой «Шансы и наборы» (честная таблица шансов);
/// наборы секциями (имя, прогресс, «+100»), карточки одного размера с рамкой редкости и лёгким блеском, неоткрытые —
/// светлые силуэты, тап — название; праздничные — свёрнутым блоком с замком и датой ближайшего праздника.
/// Список прокручивается, «Назад» закреплена внизу окна. Без платных паков.
/// </summary>
public class CollectionScreen
{
    private readonly RetentionMenuUI ui;
    private RectTransform panel, content, holidayBlock, holidayGrid;
    private Image[] cards, boxes, glows, countPills;
    private TextMeshProUGUI[] counts;
    private RetBar[] setBars;
    private Image[] setDone;
    private RectTransform[] setRewards;
    private TextMeshProUGUI header, holidayText, holidayNext;
    private RetBar total;
    private Button holidayToggle;
    private bool holidayOpen;
    private float setsHeight;

    private const float CardW = 214f, CardH = 250f, Gap = 24f, SectionH = 60f + 12f + CardH + 30f;
    private static readonly string[] SetRU = { "Лес", "Море", "Город", "Кухня", "Цирк", "Космос" }, SetEN = { "Forest", "Sea", "City", "Kitchen", "Circus", "Space" };
    private static readonly string[] RarRU = { "обычный", "редкий", "легендарный" }, RarEN = { "common", "rare", "legendary" };

    private static RetentionSettings C => RetentionSettings.Current;
    private static HearthVisualSettings V => HearthVisualSettings.Current;
    private static RetentionCore Core => Retention.Instance.core;

    public CollectionScreen(RetentionMenuUI ui) { this.ui = ui; }
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
        panel.gameObject.SetActive(false);
        ui.Popups.DrainUnseen();
    }

    private static Color RarityTint(BoxRarity r) => r == BoxRarity.Legendary ? new Color(1f, 0.86f, 0.5f) : r == BoxRarity.Rare ? new Color(0.72f, 0.84f, 1f) : Color.white;

    private void Build()
    {
        panel = HearthStyle.Window(ui.OverlayRoot, "CollectionScreen", "Спичечные коробки", "Matchboxes", Close, out RectTransform body);
        float w = body.rect.width;
        Vector2 top = new Vector2(0.5f, 1f);

        // Шапка: собрано, полоса, «Шансы и наборы»
        header = HearthStyle.Text(body, "Header", "", "", TextLevel.Subtitle, V.ink, new Vector2(0f, 1f), new Vector2(270f, -63f), new Vector2(540f, 60f), TextAlignmentOptions.Left);
        Image info = HearthStyle.Sliced(body, "Odds", "ui_btn_cream", Color.white, new Vector2(1f, 1f), new Vector2(-200f, -66f), new Vector2(400f, 132f), raycast: true);
        RetUi.HitArea(info.gameObject, ShowOdds);
        HearthStyle.Icon(info.transform, "Icon", "ic_info", new Vector2(0f, 0.5f), new Vector2(50f, 4f), 54f);
        TextMeshProUGUI il = HearthStyle.Text(info.transform, "Label", "Шансы и наборы", "Odds & sets", TextLevel.Caption, V.ink, new Vector2(0f, 0.5f), new Vector2(86f + 150f, 4f), new Vector2(300f, 60f), TextAlignmentOptions.Left);
        il.textWrappingMode = TextWrappingModes.NoWrap;
        total = HearthStyle.Bar(body, "Total", top, new Vector2(0f, -126f - 16f - 24f), new Vector2(w, 48f));

        // Прокрутка: наборы, затем праздничные
        RectTransform area = RetUi.Rect(body, "List", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -206f), new Vector2(0f, -206f));
        content = HearthStyle.Scroll(area);
        int n = C.boxes.Count;
        cards = new Image[n]; boxes = new Image[n]; glows = new Image[n]; counts = new TextMeshProUGUI[n]; countPills = new Image[n];
        setBars = new RetBar[6]; setDone = new Image[6]; setRewards = new RectTransform[6];
        for (int set = 0; set < 6; set++)
        {
            float y = -set * SectionH;
            HearthStyle.Text(content, "Set" + set, SetRU[set], SetEN[set], TextLevel.Body, V.heading, new Vector2(0f, 1f), new Vector2(150f, y - 30f), new Vector2(300f, 54f), TextAlignmentOptions.Left).fontStyle = FontStyles.Bold;
            setBars[set] = HearthStyle.Bar(content, "SetBar" + set, top, new Vector2(40f, y - 30f), new Vector2(260f, 44f));
            Image reward = HearthStyle.Sliced(content, "SetReward" + set, "ui_tag", Color.white, new Vector2(1f, 1f), new Vector2(-90f, y - 30f), new Vector2(180f, 58f));
            HearthStyle.Icon(reward.transform, "Icon", "s_ember", new Vector2(0f, 0.5f), new Vector2(32f, 0f), 42f);
            HearthStyle.Num(reward.transform, "Value", TextLevel.Caption, V.ink, new Vector2(0f, 0.5f), new Vector2(110f, 2f), new Vector2(110f, 48f)).text = "+" + C.setEmbers;
            setRewards[set] = reward.rectTransform;
            setDone[set] = HearthStyle.Icon(content, "SetDone" + set, "ic_check", new Vector2(1f, 1f), new Vector2(-214f, y - 30f), 48f);
            int k = 0;
            for (int i = 0; i < n; i++)
                if (C.boxes[i].set == set && !C.boxes[i].seasonal)
                    MakeCard(content, i, new Vector2(-w * 0.5f + CardW * 0.5f + k++ * (CardW + Gap), y - 72f - CardH * 0.5f));
        }
        setsHeight = 6f * SectionH;

        // Праздничные: свёрнутый блок с замком; «Показать» раскрывает сетку
        holidayBlock = HearthStyle.Card(content, "Holiday", top, new Vector2(0f, -setsHeight - 90f), new Vector2(w, 180f), CardKind.Muted).rectTransform;
        HearthStyle.Icon(holidayBlock, "Lock", "s_lock", new Vector2(0f, 0.5f), new Vector2(60f, 0f), 72f);
        holidayText = HearthStyle.Text(holidayBlock, "Title", "", "", TextLevel.Body, V.ink, new Vector2(0f, 1f), new Vector2(110f + 250f, -42f), new Vector2(500f, 52f), TextAlignmentOptions.Left);
        holidayText.fontStyle = FontStyles.Bold;
        holidayNext = HearthStyle.Text(holidayBlock, "Next", "", "", TextLevel.Caption, V.inkSoft, new Vector2(0f, 0f), new Vector2(110f + 250f, 62f), new Vector2(500f, 96f), TextAlignmentOptions.Left);
        holidayToggle = HearthStyle.Button(holidayBlock, "Toggle", "Показать", "Show", new Vector2(1f, 0.5f), new Vector2(-24f - 115f, 0f), 230f, ToggleHoliday, ButtonKind.Secondary, 132f, TextLevel.Caption);
        holidayGrid = RetUi.Rect(content, "HolidayGrid", top, top, new Vector2(0.5f, 1f), new Vector2(0f, -setsHeight - 204f), new Vector2(w, 2f * CardH + Gap));
        int h = 0;
        for (int i = 0; i < n; i++)
            if (C.boxes[i].seasonal)
            {
                MakeCard(holidayGrid, i, new Vector2(-w * 0.5f + CardW * 0.5f + (h % 4) * (CardW + Gap), -CardH * 0.5f - (h / 4) * (CardH + Gap)));
                h++;
            }
    }

    private void MakeCard(RectTransform parent, int i, Vector2 pos)
    {
        RetBox b = C.boxes[i];
        int ii = i;
        Image card = HearthStyle.Sliced(parent, "Box" + i, "ui_card", RarityTint(b.rarity), new Vector2(0.5f, 1f), pos, new Vector2(CardW, CardH), raycast: true);
        RetUi.HitArea(card.gameObject, () => Tap(ii));
        glows[i] = RetUi.Img(card.transform, "Shine", RetUi.Art("ui_soft"), b.rarity == BoxRarity.Legendary ? new Color(1f, 0.8f, 0.35f, 0.6f) : new Color(0.55f, 0.75f, 1f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardW * 1.05f, CardH * 0.9f));
        glows[i].material = HearthScene.Additive;
        boxes[i] = RetUi.Img(card.transform, "Art", RetUi.Art(b.sprite), Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(140f, 186f));
        countPills[i] = HearthStyle.Sliced(card.transform, "Count", "ui_pill", V.accentTint, new Vector2(1f, 1f), new Vector2(-46f, -36f), new Vector2(76f, 48f));
        counts[i] = HearthStyle.Num(countPills[i].transform, "Value", TextLevel.Caption, V.ink, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(72f, 46f));
        cards[i] = card;
    }

    private void ToggleHoliday() { holidayOpen = !holidayOpen; Refresh(); }

    private void Refresh()
    {
        if (!IsOpen || !Retention.Active) return;
        RetState s = Core.s;
        int owned = Core.BoxesOwned;
        RetUi.Set(header, $"Собрано {owned} из {C.boxes.Count}", $"{owned} of {C.boxes.Count} collected");
        total.Set((float)owned / Mathf.Max(1, C.boxes.Count), $"{owned} / {C.boxes.Count}");
        for (int i = 0; i < cards.Length; i++)
        {
            int c = s.BoxCount(i);
            bool own = c > 0;
            BoxRarity r = C.boxes[i].rarity;
            cards[i].sprite = RetUi.Art(own ? "ui_card" : "ui_inner");
            cards[i].color = own ? RarityTint(r) : V.mutedTint;
            boxes[i].color = own ? Color.white : new Color(0.55f, 0.42f, 0.32f, 0.28f);   // светлый силуэт: видно, что здесь будет коробок
            glows[i].enabled = own && r != BoxRarity.Common;
            countPills[i].gameObject.SetActive(c > 1);
            counts[i].text = "×" + c;
        }
        for (int set = 0; set < 6; set++)
        {
            int have = 0, all = 0;
            for (int i = 0; i < C.boxes.Count; i++) if (C.boxes[i].set == set && !C.boxes[i].seasonal) { all++; if (s.BoxCount(i) > 0) have++; }
            setBars[set].Set((float)have / Mathf.Max(1, all), $"{have} / {all}");
            bool done = have >= all && all > 0;
            setDone[set].enabled = done;
            setRewards[set].GetComponent<Image>().color = done ? V.mintTint : Color.white;
        }
        // Праздничные
        int hHave = 0, hAll = 0;
        for (int i = 0; i < C.boxes.Count; i++) if (C.boxes[i].seasonal) { hAll++; if (s.BoxCount(i) > 0) hHave++; }
        RetUi.Set(holidayText, $"Праздничные: {hHave} из {hAll}", $"Holiday boxes: {hHave} of {hAll}");
        RetSeasonal now = Core.ActiveSeasonal(Core.Today, out _);
        if (now != null) RetUi.Set(holidayNext, $"Сейчас праздник «{now.nameRU}»: коробок — за {now.dailyTarget} уровня дня", $"«{now.nameEN}» is on: a box for {now.dailyTarget} daily levels");
        else
        {
            RetSeasonal next = ui.NextSeasonal(out int days);
            if (next != null)
            {
                DateTime when = DayClock.DateOf(Core.Today + days);
                RetUi.Set(holidayNext, $"Ближайший: «{next.nameRU}», {when.Day} {MonthRu(when.Month)} — через {RetText.DaysRu(days)}",
                                       $"Next: «{next.nameEN}», {MonthEn(when.Month)} {when.Day} — in {(days == 1 ? "1 day" : days + " days")}");
            }
            else RetUi.Set(holidayNext, "Появляются на праздниках", "They appear during holidays");
        }
        holidayGrid.gameObject.SetActive(holidayOpen);
        RetUi.Set(RetUi.Label(holidayToggle), holidayOpen ? "Скрыть" : "Показать", holidayOpen ? "Hide" : "Show");
        content.sizeDelta = new Vector2(0f, setsHeight + 204f + (holidayOpen ? 2f * CardH + Gap + 30f : 0f));
    }

    private static string MonthRu(int m) => new[] { "января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря" }[m - 1];
    private static string MonthEn(int m) => new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" }[m - 1];

    /// <summary>Честная таблица: шансы редкостей, что даёт повтор, награда за набор, откуда берутся коробки.</summary>
    private void ShowOdds()
    {
        int[] o = C.rarityOdds, d = C.dupEmbers;
        ui.Popups.Info("Шансы и наборы", "Odds & sets",
            $"Обычный — {o[0]}%, повтор: +{RetText.EmbersRu(d[0])}\nРедкий — {o[1]}%, повтор: +{RetText.EmbersRu(d[1])}\nЛегендарный — {o[2]}%, повтор: +{RetText.EmbersRu(d[2])}\n" +
            $"Повторов нет, пока в редкости есть недостающие. Полный набор из 4: +{RetText.EmbersRu(C.setEmbers)}.\nКоробки дают: неделя, вехи серии, секретный коробок, праздники.",
            $"Common — {o[0]}%, repeat: +{RetText.EmbersEn(d[0])}\nRare — {o[1]}%, repeat: +{RetText.EmbersEn(d[1])}\nLegendary — {o[2]}%, repeat: +{RetText.EmbersEn(d[2])}\n" +
            $"No repeats while a rarity has missing boxes. A full set of 4: +{RetText.EmbersEn(C.setEmbers)}.\nBoxes come from: the week, streak milestones, the secret box, holidays.",
            RetUi.Art("ic_info"));
    }

    private void Tap(int i)
    {
        RetBox b = C.boxes[i];
        int r = (int)b.rarity;
        bool own = Core.s.BoxCount(i) > 0;
        ui.Popups.Toast(own ? $"{b.nameRU} · {RarRU[r]}" : $"Ещё не найден · {RarRU[r]}", own ? $"{b.nameEN} · {RarEN[r]}" : $"Not found yet · {RarEN[r]}");
    }
}
