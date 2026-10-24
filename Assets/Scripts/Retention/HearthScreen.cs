using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Очаг — главное «место». Комната на весь экран (HearthScene: задник продолжается по бокам, свет камина и виньетка поверх).
/// Сверху — «Назад» и плашки монет и угольков; карточка: имя огонька, плашка «Стадия: …» и одна полоса «N угольков до «…»».
/// Снизу — одна строка подсказки «что дальше», кучка угольков с числом (перетащить к огоньку) и «Всё в огонь»;
/// навигация значками с подписями (Дневник, Коробки, Календарь, Облики) и мягкими бейджами «новое».
/// Декор: светлые ценники у силуэтов (по карману — подсвечены), тап → карточка предмета → «Купить» → подтверждение.
/// Пузырь реплики — над огоньком, ≤2 строк, гаснет сам. Знакомство — в карточке внизу; до первого кормления — рука-подсказка.
/// Рост стадии — маленькое событие: удивление, пауза, танец, искры, звук, вибрация, реплика, затем окно награды.
/// </summary>
public class HearthScreen
{
    private readonly RetentionMenuUI ui;
    private RectTransform panel, content, sceneHost, sheet, sheetOutfits, sheetAtlas, bottom, nav, intro, hand, info;
    private HearthScene scene;
    private RetBubble bubble;
    private TextMeshProUGUI nameText, stageText, hintText, coinsText, embersText, pileText, introText, atlasTitle;
    private RetBar progress;
    private readonly RetBar[] aromaBars = new RetBar[3];
    private Image pile;
    private Button feedAll, rebirth, introNext, introSkip, tabOutfits, tabAtlas;
    private Image[] outfitCards, outfitIcons, atlasCells;
    private readonly Image[] badges = new Image[4];
    private CanvasGroup handGroup;
    private float lastInput, nextIdle, lastLine, lastHeight, handAt = -1f;
    private int idleStep;

    // Высоты полос интерфейса (единицы Canvas): шапка + карточка сверху, панель + навигация снизу
    private const float PanelH = 250f, BarW = 1008f;
    private static float TopH => 16f + V.buttonHeight + 12f + 140f + 12f;
    private static float BottomH => 12f + V.buttonHeight + 12f + PanelH + 12f;
    // Насколько комната может уйти под карточку сверху и под панель снизу (там пустая стена и пол)
    private const float OverTop = 0.04f, OverBottom = 0.03f;

    private static RetentionSettings C => RetentionSettings.Current;
    private static HearthVisualSettings V => HearthVisualSettings.Current;
    private static RetentionCore Core => Retention.Instance.core;

    public HearthScreen(RetentionMenuUI ui) { this.ui = ui; }

    public bool IsOpen => panel != null && panel.gameObject.activeSelf;
    public bool SheetOpen => sheet != null && sheet.gameObject.activeSelf;

    public void Open()
    {
        if (panel == null) Build();
        panel.gameObject.SetActive(true);
        panel.SetAsLastSibling();
        Retention.Changed -= Refresh;
        Retention.Changed += Refresh;
        LanguageInit.LanguageChanged -= OnLanguage;
        LanguageInit.LanguageChanged += OnLanguage;
        lastHeight = 0f;
        Refresh();
        Fit();
        lastInput = Time.unscaledTime;
        nextIdle = lastInput + V.idleSeconds;
        scene.Flame.Hop();
        if (!Core.s.introDone && V.onboarding) { StartIntro(); return; }
        if (intro != null && intro.gameObject.activeSelf) EndIntro();   // знакомство уже пройдено — обычный экран
        if (Core.s.warmth == 0 && Core.s.embers > 0) handAt = Time.unscaledTime;
        // Приветствие места: по времени суток, после долгого перерыва — тёплое «с возвращением»
        RetState s = Core.s;
        LineKind k = s.welcomeDay == Core.Today ? LineKind.GreetReturn : s.streak >= 5 ? LineKind.GreetStreak
                   : (LineKind)((int)LineKind.GreetMorning + HearthVisualSettings.TimeOfDay(HearthVisualSettings.Hour));
        Say(k);
    }

    public void Close()
    {
        Retention.Changed -= Refresh;
        LanguageInit.LanguageChanged -= OnLanguage;
        if (panel != null) panel.gameObject.SetActive(false);
        ui.Popups.DrainUnseen();
    }

    private void OnLanguage() { if (IsOpen) Refresh(); }

    public void CloseSheet() { if (sheet != null) sheet.gameObject.SetActive(false); }
    public void OpenSheet(int tab) { if (panel == null) Build(); sheet.gameObject.SetActive(true); sheet.SetAsLastSibling(); SheetTab(tab); }
    public void Refresh(bool force) { lastHeight = 0f; Refresh(); Fit(); }

    private void Build()
    {
        panel = RetUi.Fill(ui.OverlayRoot, "HearthScreen");
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.05f, 0.03f, 1f);
        panel.gameObject.AddComponent<CanvasGroup>();
        panel.gameObject.AddComponent<UIPanelAnimator>();

        // Слои: задник на весь экран → комната (в той же безопасной зоне, что и интерфейс) → свет и ценники → интерфейс
        RectTransform backdrop = RetUi.Fill(panel, "Backdrop");
        RectTransform sceneSafe = RetUi.Fill(panel, "SceneSafe");
        sceneSafe.gameObject.AddComponent<SafeArea>();
        RectTransform sceneContent = RetUi.Fill(sceneSafe, "SceneContent");
        sceneContent.gameObject.AddComponent<BannerReserve>();
        sceneHost = RetUi.Rect(sceneContent, "SceneHost", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, BottomH), new Vector2(1000f, 700f));
        RectTransform overlay = RetUi.Fill(panel, "Overlay");
        overlay.gameObject.AddComponent<Canvas>();             // свет и ценники мерцают — перестраивается только этот слой
        overlay.gameObject.AddComponent<GraphicRaycaster>();
        scene = HearthScene.Create(sceneHost, backdrop, overlay);
        scene.Root.anchorMin = scene.Root.anchorMax = new Vector2(0.5f, 0.5f);
        scene.OnSlotTap = TapSlot;
        bubble = RetBubble.Create(overlay, new Vector2(0.5f, 0.5f), Vector2.zero, 620f);
        var touchArea = RetUi.Rect(scene.Flame.Rect, "Touch", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 40f));
        var touchImg = touchArea.gameObject.AddComponent<Image>();
        touchImg.color = new Color(0f, 0f, 0f, 0f);
        var touch = touchArea.gameObject.AddComponent<FlameTouch>();
        touch.flame = scene.Flame;
        touch.onTap = () => { Input(); if (Random.value < 0.6f) Say(LineKind.Tap); };
        touch.onSpam = () => { Input(); Say(LineKind.TapSpam, force: true); };
        touch.onHold = () => { Input(); Say(LineKind.Hold); };

        RectTransform safe = RetUi.Fill(panel, "Safe");
        safe.gameObject.AddComponent<SafeArea>();
        content = RetUi.Fill(safe, "Content");
        content.gameObject.AddComponent<BannerReserve>();
        float B = V.buttonHeight;

        // Шапка: «Назад» и плашки ресурсов (монеты, угольки)
        RectTransform top = RetUi.Rect(content, "Top", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(BarW, B));
        HearthStyle.Button(top, "Back", "Назад", "Back", new Vector2(0f, 0.5f), new Vector2(115f, 0f), 230f, Close, ButtonKind.Secondary);
        coinsText = HearthStyle.Plaque(top, "Coins", "ic_coin", new Vector2(1f, 0.5f), new Vector2(-342f, 0f), 216f, out _);
        embersText = HearthStyle.Plaque(top, "Embers", "s_ember", new Vector2(1f, 0.5f), new Vector2(-108f, 0f), 216f, out _);

        // Карточка: имя, стадия, путь до следующей (тап — Атлас: как дерево выбирает облик)
        info = HearthStyle.Card(content, "Info", new Vector2(0.5f, 1f), new Vector2(0f, -(16f + B + 12f + 70f)), new Vector2(BarW, 140f), CardKind.Window, raycast: true).rectTransform;
        RetUi.HitArea(info.gameObject, () => { Input(); OpenSheet(1); });
        nameText = HearthStyle.Text(info, "Name", "", "", TextLevel.Subtitle, V.heading, new Vector2(0f, 1f), new Vector2(36f + 220f, -42f), new Vector2(440f, 56f), TextAlignmentOptions.Left);
        Image pill = HearthStyle.Sliced(info, "StagePill", "ui_pill", V.accentTint, new Vector2(1f, 1f), new Vector2(-36f - 215f, -42f), new Vector2(430f, 62f));
        HearthStyle.Icon(pill.transform, "Icon", "ic_growth", new Vector2(0f, 0.5f), new Vector2(38f, 2f), 48f);
        stageText = HearthStyle.Text(pill.transform, "Text", "", "", TextLevel.Caption, V.ink, new Vector2(0.5f, 0.5f), new Vector2(24f, 1f), new Vector2(350f, 54f));
        stageText.fontStyle = FontStyles.Bold;
        progress = HearthStyle.Bar(info, "Progress", new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(BarW - 72f, 50f));

        // Навигация: Дневник, Коробки, Календарь, Облики
        nav = RetUi.Rect(content, "Nav", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(BarW, B));
        string[] ru = { "Дневник", "Коробки", "Календарь", "Облики" }, en = { "Diary", "Boxes", "Calendar", "Outfits" };
        string[] icons = { "ic_book", "box_06", "s_calendar", "out_tophat" };
        float nw = (BarW - 3f * 16f) / 4f;
        for (int k = 0; k < 4; k++)
        {
            int kk = k;
            Image b = HearthStyle.Sliced(nav, "Nav" + k, "ui_btn_cream", Color.white, new Vector2(0f, 0.5f), new Vector2(nw * 0.5f + k * (nw + 16f), 0f), new Vector2(nw, B), raycast: true);
            RetUi.HitArea(b.gameObject, () => Nav(kk));
            HearthStyle.Icon(b.transform, "Icon", icons[k], new Vector2(0.5f, 0.5f), new Vector2(0f, 24f), 66f);
            TextMeshProUGUI l = HearthStyle.Text(b.transform, "Label", ru[k], en[k], TextLevel.Caption, V.ink, new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(nw - 20f, 46f));
            l.fontStyle = FontStyles.Bold;
            badges[k] = HearthStyle.Badge(b.transform, new Vector2(1f, 1f), new Vector2(-24f, -22f));
        }

        // Панель: подсказка, кучка угольков с числом, «Всё в огонь» / «Новый круг»
        bottom = HearthStyle.Card(content, "Bottom", new Vector2(0.5f, 0f), new Vector2(0f, 12f + B + 12f + PanelH * 0.5f), new Vector2(BarW, PanelH), CardKind.Window, raycast: true).rectTransform;
        hintText = HearthStyle.Text(bottom, "Hint", "", "", TextLevel.Body, V.inkSoft, new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(BarW - 48f, 54f));
        hintText.textWrappingMode = TextWrappingModes.NoWrap;
        pile = RetUi.Img(bottom, "Pile", RetUi.Art("ic_ember"), Color.white, new Vector2(0f, 0f), new Vector2(36f + 70f, 24f + 74f), new Vector2(140f, 140f), raycast: true);
        pileText = HearthStyle.Text(bottom, "Count", "", "", TextLevel.Subtitle, V.ink, new Vector2(0f, 0f), new Vector2(196f + 160f, 24f + 74f), new Vector2(320f, 60f), TextAlignmentOptions.Left);
        feedAll = HearthStyle.Button(bottom, "FeedAll", "Всё в огонь", "Feed all", new Vector2(1f, 0f), new Vector2(-24f - 190f, 24f + B * 0.5f), 380f, FeedAll);
        rebirth = HearthStyle.Button(bottom, "Rebirth", "Новый круг", "New cycle", new Vector2(1f, 0f), new Vector2(-24f - 190f, 24f + B * 0.5f), 380f, Rebirth, ButtonKind.Secondary);
        RectTransform token = RetUi.Img(panel, "EmberToken", RetUi.Art("ic_ember"), Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f)).rectTransform;
        token.gameObject.SetActive(false);   // EmberDrag.Start может не успеть (панель скрыта на время знакомства)
        var drag = pile.gameObject.AddComponent<EmberDrag>();
        drag.token = token; drag.flame = scene.Flame;
        drag.canFeed = () => V.dragFeed && Core.s.embers > 0 && !Core.CanRebirth;
        drag.onFed = () => Feed(Mathf.Max(1, Mathf.Min(V.feedChunk, Core.s.embers)));
        drag.onEmpty = () => { Input(); Say(LineKind.FeedEmpty, force: true); scene.Flame.LookAt(new Vector2(0f, -1f)); };

        // Рука-подсказка: от кучки к огоньку, пока огонёк ни разу не ел
        hand = RetUi.Img(panel, "HandHint", RetUi.Art("ic_hand"), Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 128f)).rectTransform;
        hand.pivot = new Vector2(0.42f, 0.95f);
        handGroup = hand.gameObject.AddComponent<CanvasGroup>();
        handGroup.blocksRaycasts = false;
        hand.gameObject.SetActive(false);

        BuildSheet();
    }

    /// <summary>
    /// Комната занимает место между шапкой и нижними панелями (может уйти на 4% под карточку и на 3% под панель — там пустая
    /// стена и пол); задник, свет, ценники, пузырь — по её положению.
    /// </summary>
    private void Fit()
    {
        float h = content.rect.height, w = content.rect.width;
        if (Mathf.Abs(h - lastHeight) < 1f) return;
        lastHeight = h;
        float avail = Mathf.Max(200f, h - TopH - BottomH);
        float size = HearthScene.Size;
        float s = Mathf.Clamp(Mathf.Min(avail / (size * (1f - OverTop - OverBottom)), (w - 16f) / size), 0.45f, 1.12f);
        float sceneH = size * s;
        float bandCenter = BottomH + avail * 0.5f;
        float cy = sceneH > avail ? (BottomH - OverBottom * sceneH + (h - TopH) + OverTop * sceneH) * 0.5f : bandCenter;
        sceneHost.sizeDelta = new Vector2(w, avail);
        sceneHost.anchoredPosition = new Vector2(0f, BottomH);
        scene.Root.localScale = Vector3.one * s;
        scene.Root.anchoredPosition = new Vector2(0f, cy - bandCenter);
        Canvas.ForceUpdateCanvases();
        scene.Sync();
        PlaceBubble();
    }

    /// <summary>Пузырь — над головой огонька, ниже полки камина (не закрывает предметы на ней), в пределах экрана.</summary>
    private void PlaceBubble()
    {
        if (bubble == null || scene == null) return;
        RectTransform ov = scene.Overlay;
        Vector2 head = ov.InverseTransformPoint(scene.Flame.HeadWorld);
        Vector2 low = ov.InverseTransformPoint(scene.RoomToWorld(0.5f, 0.35f));   // ниже — ценники предметов на полу
        head.y = Mathf.Max(head.y, low.y - 24f);
        float width = Mathf.Min(620f, ov.rect.width - 48f);
        bubble.Place(head, width);
        float half = bubble.Width * 0.5f, edge = ov.rect.width * 0.5f - 24f;
        float x = head.x;
        float dx = x + half > edge ? edge - (x + half) : x - half < -edge ? -edge - (x - half) : 0f;
        bubble.Shift(dx);
    }

    private void BuildSheet()
    {
        sheet = HearthStyle.Window(ui.OverlayRoot, "OutfitsSheet", "Облики и Атлас", "Outfits & Atlas", CloseSheet, out RectTransform body, 1440f);
        float B = V.buttonHeight;
        tabOutfits = HearthStyle.Button(body, "TabOutfits", "Облики", "Outfits", new Vector2(0.5f, 1f), new Vector2(-232f, -66f), 440f, () => SheetTab(0), ButtonKind.Primary, 132f);
        tabAtlas = HearthStyle.Button(body, "TabAtlas", "Атлас", "Atlas", new Vector2(0.5f, 1f), new Vector2(232f, -66f), 440f, () => SheetTab(1), ButtonKind.Secondary, 132f);
        sheetOutfits = RetUi.Rect(body, "Outfits", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -76f), new Vector2(0f, -152f));
        sheetAtlas = RetUi.Rect(body, "Atlas", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -76f), new Vector2(0f, -152f));
        int n = C.outfits.Count;
        outfitCards = new Image[n]; outfitIcons = new Image[n];
        const float cell = 138f, gapC = 16f;
        for (int i = 0; i < n; i++)
        {
            int ii = i;
            float x = (i % 6 - 2.5f) * (cell + gapC), y = -cell * 0.5f - 8f - (i / 6) * (cell + gapC);
            Image c = HearthStyle.Card(sheetOutfits, "Outfit" + i, new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(cell, cell), CardKind.Inner, raycast: true);
            RetUi.HitArea(c.gameObject, () => TapOutfit(ii));
            outfitIcons[i] = HearthStyle.Icon(c.transform, "Icon", C.outfits[i].sprite, new Vector2(0.5f, 0.5f), Vector2.zero, 100f);
            outfitCards[i] = c;
        }
        HearthStyle.Button(sheetOutfits, "NoOutfit", "Без облика", "No outfit", new Vector2(0.5f, 0f), new Vector2(0f, 8f + B * 0.5f), 440f, () => Retention.Instance.Equip(-1), ButtonKind.Secondary);

        atlasCells = new Image[21];
        atlasTitle = HearthStyle.Text(sheetAtlas, "Title", "", "", TextLevel.Body, V.ink, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(920f, 56f));
        atlasTitle.fontStyle = FontStyles.Bold;
        string[] fuel = { "fuel_0", "fuel_1", "fuel_2" };
        // Стадии карточками 4 + 3: имя сверху, три огонька линий (сосна, дуб, вишня) — найденные цветные
        for (int st = 1; st <= 7; st++)
        {
            int row = st <= 4 ? 0 : 1, col = row == 0 ? st - 1 : st - 5;
            float x = row == 0 ? (col - 1.5f) * 232f : (col - 1f) * 232f;
            Image card = HearthStyle.Card(sheetAtlas, "S" + st, new Vector2(0.5f, 1f), new Vector2(x, -150f - row * 152f), new Vector2(216f, 136f), CardKind.Inner);
            HearthStyle.Text(card.transform, "Name", C.stages[st].nameRU, C.stages[st].nameEN, TextLevel.Caption, V.inkSoft, new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(204f, 46f));
            for (int line = 0; line < 3; line++)
                atlasCells[(st - 1) * 3 + line] = RetUi.Img(card.transform, $"A{line}", RetUi.Art("s_flame_on"), Color.white, new Vector2(0.5f, 0f), new Vector2((line - 1) * 60f, 38f), new Vector2(54f, 54f));
        }
        TextMeshProUGUI aromaTitle = HearthStyle.Text(sheetAtlas, "AromaTitle", "Аромат дров — каким станет огонёк", "Wood aroma — what your flame becomes", TextLevel.Body, V.ink,
            new Vector2(0.5f, 1f), new Vector2(0f, -436f), new Vector2(920f, 56f));
        aromaTitle.fontStyle = FontStyles.Bold;
        for (int k = 0; k < 3; k++)
        {
            float y = -516f - k * 76f;
            HearthStyle.Icon(sheetAtlas, "Fuel" + k, fuel[k], new Vector2(0.5f, 1f), new Vector2(-420f, y), 60f);
            HearthStyle.Text(sheetAtlas, "FuelName" + k, C.fuelNamesRU[k], C.fuelNamesEN[k], TextLevel.Caption, V.ink, new Vector2(0.5f, 1f), new Vector2(-290f, y), new Vector2(180f, 50f), TextAlignmentOptions.Left);
            aromaBars[k] = HearthStyle.Bar(sheetAtlas, "Aroma" + k, new Vector2(0.5f, 1f), new Vector2(150f, y), new Vector2(620f, 48f));
            aromaBars[k].fill.color = Color.Lerp(Color.white, C.fuelColors[k], 0.6f);
        }
        HearthStyle.Text(sheetAtlas, "Help", "Задания дня и уровень дня добавляют аромат своего дерева. Какого больше — такой облик получит следующая стадия. После «Солнца» можно начать новый круг.",
            "Daily tasks and the daily level add their wood's aroma. The strongest one picks the next stage's look. After «Sun» you can start a new cycle.", TextLevel.Caption, V.inkSoft,
            new Vector2(0.5f, 0f), new Vector2(0f, 100f), new Vector2(920f, 190f));
        sheet.gameObject.SetActive(false);
    }

    private void SheetTab(int t)
    {
        sheetOutfits.gameObject.SetActive(t == 0);
        sheetAtlas.gameObject.SetActive(t == 1);
        HearthStyle.SetButton(tabOutfits, t == 0 ? ButtonKind.Primary : ButtonKind.Secondary);
        HearthStyle.SetButton(tabAtlas, t == 1 ? ButtonKind.Primary : ButtonKind.Secondary);
        RefreshSheet();
    }

    private void Nav(int k)
    {
        Input();
        RetState s = Core.s;
        switch (k)
        {
            case 0: s.questsSeenDay = Core.Today; ui.Diary.Open(0); break;
            case 1: s.boxesSeen = Core.BoxesOwned; ui.Collection.Open(); break;
            case 2: ui.Diary.Open(DiaryScreen.CalendarTab); break;
            case 3: s.outfitsSeen = s.outfitOwned.Length; OpenSheet(0); break;
        }
        Retention.Instance.Save();
        RefreshBadges();
    }

    private void Refresh()
    {
        if (!IsOpen || !Retention.Active) return;
        RetentionCore core = Core;
        RetState s = core.s;
        int line = s.stage == 0 ? core.DominantFuel : core.LineOf(s.stage);
        scene.Refresh(core);
        scene.Flame.Setup(s.stage, line, s.equippedOutfit, core.Glow);
        bool night = HearthVisualSettings.TimeOfDay(HearthVisualSettings.Hour) == 3 && V.timeOfDay;
        scene.Flame.SetBase(night ? FlameMood.Sleepy : FlameMood.Idle);
        RetUi.Set(nameText, V.flameNameRU, V.flameNameEN);
        coinsText.text = CurrencyManager.GetGoldenMatches().ToString();
        embersText.text = s.embers.ToString();
        RetStage st = C.stages[s.stage];
        RetUi.Set(stageText, $"Стадия: {st.nameRU}", $"Stage: {st.nameEN}");
        int next = core.NextStageWarmth;
        if (next > 0)
        {
            RetStage ns = C.stages[s.stage + 1];
            int left = next - s.warmth;
            progress.Set((float)(s.warmth - st.warmth) / Mathf.Max(1, next - st.warmth),
                RetText.Ru ? $"{RetText.EmbersRu(left)} до стадии «{ns.nameRU}»" : $"{RetText.EmbersEn(left)} to the «{ns.nameEN}» stage");
        }
        else progress.Set(1f, RetText.Ru ? $"Самая большая стадия · Атлас {core.AtlasCount} из 21" : $"The biggest stage · Atlas {core.AtlasCount} of 21");
        RetUi.Set(pileText, RetText.EmbersRu(s.embers), RetText.EmbersEn(s.embers));
        pile.color = s.embers > 0 ? Color.white : new Color(1f, 1f, 1f, 0.45f);
        feedAll.gameObject.SetActive(!core.CanRebirth);
        feedAll.interactable = s.embers > 0;
        rebirth.gameObject.SetActive(core.CanRebirth);
        RefreshHint();
        RefreshBadges();
        if (SheetOpen) RefreshSheet();
        PlaceBubble();
    }

    /// <summary>Одна строка: что сделать дальше (числа — в полосе и у кучки, здесь не повторяются). Без давления.</summary>
    private void RefreshHint()
    {
        if (IntroActive) return;
        RetentionCore core = Core;
        RetState s = core.s;
        int next = core.NextStageWarmth;
        string nextName = next > 0 ? C.stages[s.stage + 1].nameRU : "", nextNameEn = next > 0 ? C.stages[s.stage + 1].nameEN : "";
        int questsLeft = 0; foreach (int q in s.questClaimed) if (q == 0) questsLeft++;
        if (core.CanRebirth)
            RetUi.Set(hintText, "Огонёк стал Солнцем! Можно начать новый круг", "Your flame became the Sun! You can start a new cycle");
        else if (s.embers > 0 && next > 0 && s.embers >= next - s.warmth)
            RetUi.Set(hintText, $"Угольков хватит до стадии «{nextName}»!", $"Enough embers for the «{nextNameEn}» stage!");
        else if (s.embers > 0)
            RetUi.Set(hintText, "Перетащи уголёк к огоньку, чтобы он рос", "Drag an ember to the flame to help it grow");
        else if (!core.DailyDoneToday && Retention.Instance.TodayDailyIndex >= 0)
            RetUi.Set(hintText, $"Уровень дня даст +{RetText.EmbersRu(C.dailyWinEmbers)}", $"The daily level gives +{RetText.EmbersEn(C.dailyWinEmbers)}");
        else if (questsLeft > 0)
            RetUi.Set(hintText, $"Задания дня дадут угольки: осталось {questsLeft}", $"Daily tasks give embers: {questsLeft} left");
        else if (core.SecretReady)
            RetUi.Set(hintText, "Секретный коробок готов — загляни в Дневник", "The secret box is ready — check the Diary");
        else
        {
            System.TimeSpan left = DayClock.UntilMidnight;
            RetUi.Set(hintText, $"На сегодня всё! Новый уровень через {left.Hours} ч {left.Minutes:00} мин", $"All done for today! A new level in {left.Hours}h {left.Minutes:00}m");
        }
    }

    private void RefreshBadges()
    {
        RetState s = Core.s;
        badges[0].enabled = s.questsSeenDay != Core.Today || Core.SecretReady;
        badges[1].enabled = Core.BoxesOwned > s.boxesSeen;
        badges[2].enabled = false;
        badges[3].enabled = s.outfitOwned.Length > s.outfitsSeen;
    }

    private void RefreshSheet()
    {
        RetentionCore core = Core;
        RetState s = core.s;
        for (int i = 0; i < outfitCards.Length; i++)
        {
            bool own = s.HasOutfit(i);
            outfitIcons[i].color = own ? Color.white : new Color(0.55f, 0.45f, 0.38f, 0.35f);   // светлый силуэт, не чёрная дыра
            HearthStyle.SetCard(outfitCards[i], s.equippedOutfit == i ? CardKind.Accent : own ? CardKind.Inner : CardKind.Muted);
        }
        RetUi.Set(atlasTitle, $"Атлас огоньков: {core.AtlasCount} из 21 · круг {s.cycles + 1}", $"Flame atlas: {core.AtlasCount} of 21 · cycle {s.cycles + 1}");
        for (int st = 1; st <= 7; st++)
            for (int l = 0; l < 3; l++)
                atlasCells[(st - 1) * 3 + l].color = core.AtlasHas(st, l) ? Color.Lerp(Color.white, C.fuelColors[l], 0.7f) : new Color(0.55f, 0.45f, 0.38f, 0.28f);
        int aromaMax = Mathf.Max(30, Mathf.Max(s.aroma[0], Mathf.Max(s.aroma[1], s.aroma[2])));
        for (int k = 0; k < 3; k++) aromaBars[k].Set((float)s.aroma[k] / aromaMax, s.aroma[k].ToString());
    }

    // ------------------------------------------------------------------ действия

    private void Feed(int amount)
    {
        Input();
        int before = Core.s.stage;
        int n = Retention.Instance.Feed(amount);
        if (n <= 0) return;
        scene.Flame.Eat();
        HearthAudio.Feed();
        handAt = -1f;
        if (hand != null) hand.gameObject.SetActive(false);
        if (IntroActive && introStep == 1) { introStep = 2; ShowIntroStep(); }
        if (Core.s.stage > before) ui.StartCoroutine(StageUp(Core.s.stage));
        else if (Random.value < 0.5f) Say(LineKind.Feed);
    }

    private void FeedAll() => Feed(Core.s.embers);

    private IEnumerator StageUp(int stage)
    {
        RetFlame f = scene.Flame;
        f.Surprise();
        yield return new WaitForSecondsRealtime(0.5f);
        f.Celebrate();
        f.Burst(12);
        HearthAudio.Crackle();
        HearthAudio.Haptic();
        Say(LineKind.StageUp, force: true);
        yield return new WaitForSecondsRealtime(1.8f);
        ui.Popups.ShowStage(stage, Core.LineOf(stage));
        Retention.Instance.MarkStageSeen();
    }

    private void Rebirth() => ui.Popups.Confirm("Новый круг", "New cycle",
        "Огонёк снова станет искрой и сможет вырасти в другие облики Атласа. Облики, декор и коробки останутся.",
        "Your flame becomes a spark again and can grow into other Atlas forms. Outfits, decor and boxes stay.",
        () => Retention.Instance.Rebirth());

    /// <summary>Тап по силуэту или ценнику: карточка предмета (цена, сколько есть) → «Купить» → прежнее подтверждение.</summary>
    private void TapSlot(int i)
    {
        Input();
        RetDecor d = C.decor[i];
        if (Core.s.HasDecor(i)) return;
        bool coins = d.priceCoins > 0;
        int price = coins ? d.priceCoins : d.priceEmbers;
        int have = coins ? CurrencyManager.GetGoldenMatches() : Core.s.embers;
        scene.Flame.LookAt(new Vector2((d.slot.x - 0.5f) * 2f, (d.slot.y - 0.3f) * 2f), 2f);
        string priceRu = coins ? $"{price} {RetText.Plural(price, "монету", "монеты", "монет")}" : RetText.EmbersRu(price);
        string priceEn = coins ? RetText.CoinsEn(price) : RetText.EmbersEn(price);
        ui.Popups.Item(d.nameRU, d.nameEN, RetUi.Art(d.sprite), coins, price, have, () =>
            ui.Popups.Confirm(d.nameRU, d.nameEN, $"Поставить в комнату за {priceRu}?", $"Place it in the room for {priceEn}?", () =>
            {
                if (!Retention.Instance.BuyDecor(i)) return;
                scene.PlayAppear(i);
                scene.Flame.Surprise();
                HearthAudio.Reward();
                Say(LineKind.Decor, force: true);
            }, RetUi.Art(d.sprite)));
    }

    private void TapOutfit(int i)
    {
        if (Core.s.HasOutfit(i)) { Retention.Instance.Equip(i); scene.Flame.Tap(); return; }
        RetOutfit o = C.outfits[i];
        ui.Popups.Toast($"{o.nameRU}: {o.sourceRU}", $"{o.nameEN}: {o.sourceEN}");
    }

    // ------------------------------------------------------------------ знакомство (3 шага, пропускаемое)

    private int introStep;

    /// <summary>Шаги 0 и 2 — карточка внизу (вместо панели и навигации); шаг 1 — обычный экран, рука и пузырь: перетащи уголёк.</summary>
    private void StartIntro()
    {
        if (intro == null)
        {
            float B = V.buttonHeight;
            intro = HearthStyle.Card(content, "Intro", new Vector2(0.5f, 0f), new Vector2(0f, 12f + (BottomH - 24f) * 0.5f), new Vector2(BarW, BottomH - 24f), CardKind.Window, raycast: true).rectTransform;
            introText = HearthStyle.Text(intro, "Text", "", "", TextLevel.Body, V.ink, new Vector2(0.5f, 1f), new Vector2(0f, -40f - 75f), new Vector2(960f, 150f));
            introSkip = HearthStyle.Button(intro, "Skip", "Пропустить", "Skip", new Vector2(0.5f, 0f), new Vector2(-250f, 24f + B * 0.5f), 440f, EndIntro, ButtonKind.Secondary);
            introNext = HearthStyle.Button(intro, "Next", "Дальше", "Next", new Vector2(0.5f, 0f), new Vector2(250f, 24f + B * 0.5f), 440f, NextIntro);
        }
        introStep = 0;
        ShowIntroStep();
    }

    private void ShowIntroStep()
    {
        bool card = introStep != 1;
        intro.gameObject.SetActive(card);
        bottom.gameObject.SetActive(!card);
        nav.gameObject.SetActive(!card);
        string nameRU = V.flameNameRU, nameEN = V.flameNameEN;
        switch (introStep)
        {
            case 0:
                RetUi.Set(introText, $"Привет! Я — {nameRU}, огонёк этого очага. Я расту от угольков, а угольки дают уровень дня и задания.",
                                     $"Hi! I'm {nameEN}, the little flame of this hearth. I grow from embers; the daily level and tasks give them.");
                RetUi.Set(RetUi.Label(introNext), "Дальше", "Next");
                bubble.Say("Привет! Будем дружить?", "Hi! Friends?", 9999f);
                scene.Flame.Celebrate();
                break;
            case 1:
                Retention.Instance.GiveStarter();
                RetUi.Set(hintText, "Перетащи уголёк из кучки к огоньку", "Drag an ember from the pile to the flame");
                bubble.Say("Угостишь угольком?", "Got an ember for me?", 9999f);
                scene.Flame.SetMood(FlameMood.Curious, 4f);
                handAt = Time.unscaledTime;
                break;
            default:
                RetUi.Set(introText, "Вкусно! Заходи каждый день: подарок, новый коробок и задания. А я буду расти и ждать тебя!",
                                     "Yum! Come back every day: a gift, a new box and tasks. I'll grow and wait for you!");
                RetUi.Set(RetUi.Label(introNext), "Готово", "Done");
                bubble.Say("Спасибо!", "Thank you!", 9999f);
                scene.Flame.Tap();
                break;
        }
        introSkip.gameObject.SetActive(introStep == 0);
        var nrt = (RectTransform)introNext.transform;
        nrt.anchoredPosition = new Vector2(introStep == 0 ? 250f : 0f, nrt.anchoredPosition.y);
    }

    private void NextIntro() { if (++introStep > 2) EndIntro(); else ShowIntroStep(); }

    private void EndIntro()
    {
        if (intro != null) intro.gameObject.SetActive(false);
        bottom.gameObject.SetActive(true);
        nav.gameObject.SetActive(true);
        bubble.Hide();
        Core.s.introDone = true;
        Retention.Instance.Save();
        RefreshHint();
        if (Core.s.warmth == 0 && Core.s.embers > 0) handAt = Time.unscaledTime;
    }

    // ------------------------------------------------------------------ реплики, рука-подсказка и идле

    private bool IntroActive => intro != null && (intro.gameObject.activeSelf || introStep == 1) && !Core.s.introDone;

    private void Input() { lastInput = Time.unscaledTime; nextIdle = lastInput + V.idleSeconds; idleStep = 0; }

    private void Say(LineKind k, bool force = false)
    {
        if (!V.lines || bubble == null || IntroActive) return;   // реплики не перебивают знакомство
        if (!force && Time.unscaledTime - lastLine < V.lineCooldown) return;
        lastLine = Time.unscaledTime;
        PlaceBubble();
        bubble.Say(FlameLines.Pick(k), V.bubbleSeconds);
    }

    /// <summary>Рука ведёт от кучки к огоньку (1.1 с), исчезает, пауза; до первого кормления.</summary>
    private void TickHand()
    {
        if (handAt < 0f || !V.onboarding || ui.Popups.Busy || SheetOpen) { if (hand.gameObject.activeSelf) hand.gameObject.SetActive(false); return; }
        if (!pile.gameObject.activeInHierarchy) return;
        if (!hand.gameObject.activeSelf) hand.gameObject.SetActive(true);
        float k = Mathf.Repeat(Time.unscaledTime - handAt, 2.2f);
        float move = Mathf.Clamp01((k - 0.25f) / 1.1f);
        float e = move * move * (3f - 2f * move);
        Vector3 a = pile.rectTransform.position, b = scene.Flame.Rect.TransformPoint(new Vector3(0f, scene.Flame.Rect.rect.height * 0.35f, 0f));
        hand.position = Vector3.Lerp(a, b, e);
        float alpha = k < 0.25f ? k / 0.25f : k > 1.6f ? Mathf.Clamp01(1f - (k - 1.6f) / 0.35f) : 1f;
        handGroup.alpha = alpha;
        float press = k < 0.25f ? 1f - k : 0.92f;
        hand.localScale = new Vector3(press, press, 1f);
    }

    /// <summary>Вызывается из RetentionMenuUI.Update, пока экран открыт.</summary>
    public void Tick()
    {
        if (!IsOpen) return;
        Fit();
        TickHand();
        scene.PauseAmbient(ui.Popups.Busy || SheetOpen);
        if (UnityEngine.Input.touchCount > 0 || UnityEngine.Input.GetMouseButtonDown(0)) Input();
        if (!V.idleActions || Time.unscaledTime < nextIdle || SheetOpen || ui.Popups.Busy) return;
        nextIdle = Time.unscaledTime + V.idleSeconds * (1f + idleStep * 0.5f);
        RetFlame f = scene.Flame;
        bool night = f.Mood == FlameMood.Sleepy;
        switch ((idleStep++ + (int)(Time.unscaledTime)) % 4)
        {
            case 0: f.SetMood(FlameMood.Sleepy, 1.6f); if (!night) Say(LineKind.Idle); break;            // зевает
            case 1: f.LookAt(new Vector2(Random.Range(-1f, 1f), Random.Range(0f, 1f)), 2.5f); break;     // смотрит на предмет
            case 2: f.Burst(6); f.Hop(); if (Random.value < 0.4f) Say(night ? LineKind.Night : LineKind.Hint); break;
            case 3: if (Random.value < 0.5f) Say(LineKind.Tease); break;
        }
    }
}
