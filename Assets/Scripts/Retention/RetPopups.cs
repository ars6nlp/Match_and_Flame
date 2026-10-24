using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Окна «Очага» в меню: «Что нового» (раз за версию), подарок дня (первый заход за день, до 5 с, закрывается тапом),
/// окна наград (коробок, облик, веха серии, неделя, праздник, секретный коробок, набор), переход стадии, подтверждение,
/// короткие всплывашки. Одно окно за раз, у каждого — видимая кнопка.
/// </summary>
public class RetPopups
{
    private readonly RetentionMenuUI ui;
    private RectTransform popup;
    private Image popupIcon;
    private RetFlame popupFlame;
    private TextMeshProUGUI popupTitle, popupText, popupCount;
    private readonly RectTransform[] flyers = new RectTransform[6];
    private float countFrom, countTo, countAt = -1f, textAt = -1f;
    private Button popupOk, popupSecond;
    private UIParticleField popupBurst;
    private UnityAction onOk, onSecond;
    private bool secondIsCancel = true;   // вторая кнопка — «Нет/Позже»; у подарка она — необязательное действие (видео)
    private float autoCloseAt = -1f;
    private float shownAt = -10f;
    // Двойной тап: следующее окно из очереди открывается на том же месте — второй тап не должен его «подтвердить»
    private bool TapAllowed() => Busy && Time.unscaledTime - shownAt > 0.35f;
    private bool giftOpen;

    private RectTransform toast;
    private TextMeshProUGUI toastText;
    private float toastUntil;

    private static RetentionSettings C => RetentionSettings.Current;
    private static RetentionCore Core => Retention.Instance.core;

    public RetPopups(RetentionMenuUI ui) { this.ui = ui; }

    public bool Busy => popup != null && popup.gameObject.activeSelf;

    /// <summary>
    /// Системная «Назад»: всегда безопасный вариант. Если у окна есть вторая кнопка («Нет, позже», «Позже», «Не нужно»),
    /// срабатывает она; иначе окно просто закрывается как прочитанное (у одиночных окон «Отлично» ничего не тратит).
    /// Подтверждение траты, отката стадии или надевания облика — только явным тапом по кнопке.
    /// </summary>
    public void CloseCurrent()
    {
        if (!Busy) return;
        if (onSecond != null && secondIsCancel) Second();
        else Ok();
    }

    private const float CardH = 1180f;
    private RectTransform toastLayer, priceRow;
    private TextMeshProUGUI priceText, haveText;
    private Image priceIcon, haveIcon, countIcon;

    private static HearthVisualSettings V => HearthVisualSettings.Current;

    private void Build()
    {
        popup = RetUi.Fill(ui.OverlayRoot, "Popup");
        var dim = popup.gameObject.AddComponent<Image>();
        dim.color = V.dim;
        popup.gameObject.AddComponent<CanvasGroup>();
        popup.gameObject.AddComponent<UIPanelAnimator>();
        RetUi.TapCatcher(popup.gameObject, () => { if (giftOpen && TapAllowed()) Ok(); });   // подарок закрывается тапом в любом месте
        RectTransform safe = RetUi.Fill(popup, "Safe");
        safe.gameObject.AddComponent<SafeArea>();
        RectTransform content = RetUi.Fill(safe, "Content");
        content.gameObject.AddComponent<BannerReserve>();
        Vector2 size = new Vector2(V.windowWidth, CardH);
        Image shadow = HearthStyle.Sliced(content, "Shadow", "ui_shadow", new Color(1f, 1f, 1f, 0.8f), new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), size + new Vector2(36f, 36f));
        Image card = HearthStyle.Card(content, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, size, CardKind.Window, raycast: true);
        var fit = card.gameObject.AddComponent<RetFit>(); fit.design = size + new Vector2(40f, 30f); fit.follower = shadow.rectTransform;
        popupBurst = RetUi.Fill(card.transform, "Burst").gameObject.AddComponent<UIParticleField>();
        popupBurst.raycastTarget = false; popupBurst.emissionRate = 0f; popupBurst.maxParticles = 18;   // вместе с огоньком окна — не больше 30 частиц
        popupBurst.material = HearthScene.Additive;
        popupTitle = HearthStyle.Text(card.transform, "Title", "", "", TextLevel.Title, V.heading, new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(size.x - 100f, 120f));
        popupIcon = RetUi.Img(card.transform, "Icon", null, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 236f), new Vector2(240f, 240f));
        float fu = V.popupFlameUnit;
        popupFlame = RetFlame.Create(card.transform, new Vector2(0f, 80f + fu * 0.13f), fu);   // основание огонька — над числом и текстом
        popupText = HearthStyle.Text(card.transform, "Text", "", "", TextLevel.Body, V.ink, new Vector2(0.5f, 0.5f), new Vector2(0f, -172f), new Vector2(size.x - 120f, 330f));
        countIcon = HearthStyle.Icon(card.transform, "CountIcon", "s_ember", new Vector2(0.5f, 0.5f), new Vector2(-80f, 40f), 72f);
        popupCount = HearthStyle.Num(card.transform, "Count", TextLevel.Title, V.heading, new Vector2(0.5f, 0.5f), new Vector2(70f, 40f), new Vector2(220f, 90f), TextAlignmentOptions.Left);
        // Цена предмета: две строки со значками («Цена», «У тебя»)
        priceRow = RetUi.Rect(card.transform, "Price", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(440f, 150f));
        priceIcon = HearthStyle.Icon(priceRow, "PriceIcon", "s_ember", new Vector2(0f, 1f), new Vector2(36f, -36f), 60f);
        priceText = HearthStyle.Text(priceRow, "PriceText", "", "", TextLevel.Body, V.ink, new Vector2(0f, 1f), new Vector2(80f + 170f, -36f), new Vector2(340f, 60f), TextAlignmentOptions.Left);
        priceText.fontStyle = FontStyles.Bold;
        haveIcon = HearthStyle.Icon(priceRow, "HaveIcon", "s_ember", new Vector2(0f, 1f), new Vector2(36f, -112f), 60f);
        haveText = HearthStyle.Text(priceRow, "HaveText", "", "", TextLevel.Body, V.inkSoft, new Vector2(0f, 1f), new Vector2(80f + 170f, -112f), new Vector2(340f, 60f), TextAlignmentOptions.Left);
        priceRow.gameObject.SetActive(false);
        for (int i = 0; i < flyers.Length; i++)
        {
            flyers[i] = RetUi.Img(card.transform, "Fly" + i, RetUi.Art("s_ember"), Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f)).rectTransform;
            flyers[i].gameObject.SetActive(false);
        }
        float by = 28f + V.buttonHeight * 0.5f;
        popupOk = HearthStyle.Button(card.transform, "Ok", "Отлично!", "Great!", new Vector2(0.5f, 0f), new Vector2(0f, by), 520f, () => { if (TapAllowed()) Ok(); });
        popupSecond = HearthStyle.Button(card.transform, "Second", "", "", new Vector2(0.5f, 0f), new Vector2(-232f, by), 440f, () => { if (TapAllowed()) Second(); }, ButtonKind.Secondary);

        // Подсказка: светлая плашка сверху, в безопасной зоне, нажатия не перехватывает
        toastLayer = RetUi.Fill(ui.OverlayRoot, "ToastLayer");
        toastLayer.gameObject.AddComponent<SafeArea>();
        toast = HearthStyle.Card(toastLayer, "Toast", new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(980f, 150f), CardKind.Window).rectTransform;
        toast.GetComponent<Image>().raycastTarget = false;
        toastText = HearthStyle.Text(toast, "Text", "", "", TextLevel.Body, V.ink, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(920f, 120f));
        toast.gameObject.SetActive(false);
        popup.gameObject.SetActive(false);
    }

    private void Show(string titleRU, string titleEN, string textRU, string textEN, Sprite icon, string okRU = "Отлично!", string okEN = "Great!",
        UnityAction ok = null, string secondRU = null, string secondEN = null, UnityAction second = null, bool flame = false, bool secondCancels = true)
    {
        if (popup == null) Build();
        popup.gameObject.SetActive(true);
        popup.SetAsLastSibling();
        RetUi.Set(popupTitle, titleRU, titleEN);
        RetUi.Set(popupText, textRU, textEN);
        // Списки (строки с «•») — слева, остальное — по центру
        bool list = textRU != null && textRU.StartsWith("•");
        popupText.alignment = list ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Top;
        Arrange(236f, 240f, -172f, 330f);
        popupIcon.sprite = icon; popupIcon.enabled = icon != null && !flame;
        popupFlame.gameObject.SetActive(flame);
        priceRow.gameObject.SetActive(false);
        countIcon.enabled = false;
        RetUi.Set(RetUi.Label(popupOk), okRU, okEN);
        onOk = ok; onSecond = second; secondIsCancel = secondCancels;
        shownAt = Time.unscaledTime;
        popupSecond.gameObject.SetActive(second != null);
        if (second != null) RetUi.Set(RetUi.Label(popupSecond), secondRU, secondEN);
        // Две кнопки — рядом, одной высоты (вторичная слева); одна — по центру
        var okRt = (RectTransform)popupOk.transform;
        okRt.anchoredPosition = new Vector2(second != null ? 232f : 0f, okRt.anchoredPosition.y);
        okRt.sizeDelta = new Vector2(second != null ? 440f : 520f, okRt.sizeDelta.y);
        ((RectTransform)RetUi.Label(popupOk).transform).sizeDelta = new Vector2(okRt.sizeDelta.x - 48f, okRt.sizeDelta.y - 36f);
        autoCloseAt = -1f; giftOpen = false;
        countAt = -1f; popupCount.text = ""; textAt = -1f; popupText.alpha = 1f;
        foreach (RectTransform f in flyers) f.gameObject.SetActive(false);
        if (popupBurst.isActiveAndEnabled) popupBurst.Burst(14, new Vector2(0f, 120f));
        if (C.rewardSound != null && SoundManager.Instance != null) SoundManager.Instance.PlaySound(C.rewardSound);
    }

    private void Ok()
    {
        UnityAction a = onOk;
        onOk = null; onSecond = null; giftOpen = false; autoCloseAt = -1f;
        popup.gameObject.SetActive(false);
        a?.Invoke();
        DrainUnseen();
    }

    private void Second()
    {
        UnityAction a = onSecond;
        onSecond = null;
        onOk = null;          // «Нет» не должно выполнять действие «Да» (раньше Ok() ниже вызывал onOk)
        a?.Invoke();
        Ok();
    }

    public void Tick()
    {
        if (textAt > 0f && Busy) { popupText.alpha = Mathf.Clamp01((Time.unscaledTime - textAt) / 0.3f); if (popupText.alpha >= 1f) textAt = -1f; }
        // Счётчик награды тикает, угольки летят из очага (огонька) к числу
        if (countAt > 0f && Busy)
        {
            float k = Mathf.Clamp01((Time.unscaledTime - countAt) / 1.1f);
            popupCount.text = "+" + Mathf.RoundToInt(Mathf.Lerp(countFrom, countTo, 1f - (1f - k) * (1f - k)));
            for (int i = 0; i < flyers.Length; i++)
            {
                float fk = Mathf.Clamp01(k * 1.6f - i * 0.1f);
                flyers[i].gameObject.SetActive(fk > 0f && fk < 1f);
                Vector2 a = new Vector2(0f, 230f), b = new Vector2(-80f, 40f);
                flyers[i].anchoredPosition = Vector2.Lerp(a, b, fk) + new Vector2(Mathf.Sin(fk * Mathf.PI) * (i - 2.5f) * 60f, Mathf.Sin(fk * Mathf.PI) * 80f);
            }
            if (k >= 1f) countAt = -1f;
        }
        if (autoCloseAt > 0f && Time.unscaledTime >= autoCloseAt && Busy) Ok();
        if (toast != null && toast.gameObject.activeSelf && Time.unscaledTime >= toastUntil) toast.gameObject.SetActive(false);
    }

    public void Toast(string ru, string en)
    {
        if (popup == null) Build();
        RetUi.Set(toastText, ru, en);
        toast.gameObject.SetActive(true);
        toastLayer.SetAsLastSibling();
        toastUntil = Time.unscaledTime + 2.5f;
    }

    public void Confirm(string titleRU, string titleEN, string textRU, string textEN, UnityAction yes, Sprite icon = null) =>
        Show(titleRU, titleEN, textRU, textEN, icon != null ? icon : RetUi.Art("fl_body"), "Да", "Yes", yes, "Нет, позже", "Not now", () => { });

    /// <summary>Справка (шансы, правила): маленький значок, больше места тексту; одна кнопка «Понятно».</summary>
    public void Info(string titleRU, string titleEN, string textRU, string textEN, Sprite icon)
    {
        Show(titleRU, titleEN, textRU, textEN, icon, "Понятно", "Got it");
        Arrange(330f, 110f, -64f, 600f);
    }

    /// <summary>Раскладка карточки: центр и размер картинки, центр и высота текста (единицы от центра карточки).</summary>
    private void Arrange(float iconY, float iconSize, float textY, float textH)
    {
        var irt = popupIcon.rectTransform;
        irt.anchoredPosition = new Vector2(0f, iconY);
        irt.sizeDelta = new Vector2(iconSize, iconSize);
        var trt = popupText.rectTransform;
        trt.anchoredPosition = new Vector2(0f, textY);
        trt.sizeDelta = new Vector2(trt.sizeDelta.x, textH);
    }

    /// <summary>
    /// Карточка предмета для комнаты: картинка, цена и сколько есть. Хватает — «Купить» (дальше — прежнее подтверждение)
    /// и «Закрыть»; не хватает — сколько ещё и где взять, одна кнопка «Понятно». «Назад» — закрыть.
    /// </summary>
    public void Item(string nameRU, string nameEN, Sprite icon, bool coins, int price, int have, UnityAction buy)
    {
        bool can = have >= price;
        string ru = can ? "Поставить в комнату?" : $"Не хватает {price - have}. {(coins ? "Монеты дают обычные уровни" : "Угольки дают уровень дня и задания")}";
        string en = can ? "Place it in the room?" : $"{price - have} more needed. {(coins ? "Coins come from regular levels" : "Embers come from the daily level and tasks")}";
        if (can) Show(nameRU, nameEN, ru, en, icon, "Купить", "Buy", buy, "Закрыть", "Close", () => { });
        else Show(nameRU, nameEN, ru, en, icon, "Понятно", "Got it");
        // Цена и запас — значком и числом
        priceRow.gameObject.SetActive(true);
        string sprite = coins ? "ic_coin" : "s_ember";
        priceIcon.sprite = haveIcon.sprite = RetUi.Art(sprite);
        RetUi.Set(priceText, $"Цена: {price}", $"Price: {price}");
        RetUi.Set(haveText, $"У тебя: {have}", $"You have: {have}");
        haveText.color = can ? V.inkSoft : V.lowRed;
        Arrange(268f, 200f, -200f, 300f);
    }

    // ------------------------------------------------------------------ вход в меню

    public void Startup()
    {
        if (C.whatsNew && ShowWhatsNew()) return;
        if (ShowGift()) return;
        if (AskReminders()) return;
        DrainUnseen();
    }

    /// <summary>
    /// Предложение напоминаний (Android 13+ — системный запрос POST_NOTIFICATIONS). Только если подключён пакет уведомлений,
    /// игрок уже получил первую награду «Очага» и ещё не отвечал. Отказ ничего не отнимает.
    /// </summary>
    private bool AskReminders()
    {
        RetState s = Core.s;
        INotificationService svc = NotificationPlanner.Service;
        if (!C.notifications || !svc.Available || s.notifAsked || !s.firstRewardGiven) return false;
        s.notifAsked = true;
        Retention.Instance.Save();
        Show("Напоминания", "Reminders",
             "Можно раз в день напоминать, что готов новый уровень дня. Не чаще одного раза, не ночью (22:00–9:00), выключается в настройках.",
             "We can remind you once a day when a new daily level is ready. At most once, never at night (22:00–9:00), off in settings.",
             RetUi.Art("s_calendar"), "Напоминать", "Remind me",
             () => svc.RequestPermission(ok => { s.notifEnabled = ok; Retention.Instance.Save(); NotificationPlanner.Reschedule(Core); }),
             "Не нужно", "No, thanks", () => { s.notifEnabled = false; Retention.Instance.Save(); });
        return true;
    }

    private bool ShowWhatsNew()
    {
        RetState s = Core.s;
        string v = Application.version;
        if (s.seenWhatsNew == v) return false;
        // Новый игрок: «что нового» ему ни к чему — просто запоминаем версию
        bool veteran = PlayerPrefs.GetInt("Level_0_Stars", 0) > 0 || !string.IsNullOrEmpty(s.seenWhatsNew);
        s.seenWhatsNew = v;
        Retention.Instance.Save();
        RetWhatsNew w = Retention.WhatsNewFor(v);
        if (w == null || !veteran) return false;
        Show(w.titleRU, w.titleEN, "• " + string.Join("\n• ", w.linesRU), "• " + string.Join("\n• ", w.linesEN), RetUi.Art("hearth_fireplace"),
             "Посмотреть", "Take a look", () => { if (!ShowGift()) DrainUnseen(); });
        Arrange(318f, 170f, -96f, 560f);   // список — больше места
        return true;
    }

    /// <summary>Коробок «раскрывается»: потряхивание, вспышка-пружинка, искры; текст появляется после.</summary>
    private void Reveal()
    {
        RetPop.On(popupIcon).ShakeThenPop();
        popupText.alpha = 0f;
        textAt = Time.unscaledTime + 0.7f;
        if (popupBurst.isActiveAndEnabled) popupBurst.Burst(36, new Vector2(0f, 170f));
        HearthAudio.Crackle();
    }

    private bool ShowGift()
    {
        if (!Core.CanClaimGift || !Retention.Instance.Unlocked) return false;
        RetState s = Core.s;
        int e = Core.GiftEmbers, c = Core.GiftCoins;
        bool welcome = s.welcomeDay == Core.Today;
        bool restore = Core.CanRestoreStreak;
        string ru, en;
        if (welcome && s.streak == 0) { ru = "Огонёк ждал тебя! Начнём новую серию — щит при нас."; en = "Your flame waited for you! Let's start a new streak — the shield is with us."; }
        else if (welcome && s.restoreDay == Core.Today && s.restoreValue > s.streak)
        { ru = $"С возвращением! Серия теперь {s.streak} — половина сохранилась."; en = $"Welcome back! Your streak is now {s.streak} — half of it was kept."; }
        else if (welcome) { ru = $"С возвращением! Щит сберёг серию: {s.streak}."; en = $"Welcome back! A shield kept your streak: {s.streak}."; }
        else if (s.streak == 0) { ru = "Пройди сегодня любой уровень — начнётся серия."; en = "Win any level today to start a streak."; }
        else { ru = $"Серия {s.streak} — огонёк светит ярче."; en = $"Streak {s.streak} — your flame glows brighter."; }
        if (c > 0) { ru += $"\n+{RetText.CoinsRu(c)} за неделю подряд!"; en += $"\n+{RetText.CoinsEn(c)} for a full week!"; }
        if (restore) { ru += $"\nСерию можно вернуть до {s.restoreValue} — по желанию, за видео."; en += $"\nYou can restore your streak to {s.restoreValue} — optional, with a video."; }
        Retention.Instance.ClaimGift();   // начислено и сохранено сразу: закрыть окно = забрать, убийство процесса ничего не теряет
        Show("Подарок дня", "Daily gift", ru, en, null, "Забрать", "Collect", () => AskReminders(),
             restore ? "Вернуть серию (видео)" : null, restore ? "Restore streak (video)" : null, restore ? (UnityAction)(() => Retention.Instance.RestoreStreakWithAd()) : null, flame: true, secondCancels: false);   // «Назад» — просто забрать, без видео
        popupFlame.Setup(s.stage, Core.LineOf(s.stage), s.equippedOutfit, Core.Glow);
        popupFlame.Celebrate();
        countFrom = 0; countTo = e; countAt = Time.unscaledTime + 0.3f;
        countIcon.enabled = true;
        HearthAudio.Reward();
        giftOpen = true;
        if (!restore) autoCloseAt = Time.unscaledTime + C.giftPopupSeconds;
        return true;
    }

    /// <summary>Показать накопленные награды по одной.</summary>
    public void DrainUnseen()
    {
        if (Busy || Retention.Unseen.Count == 0 || ui.Hearth.IsOpen) return;
        RetEvent e = Retention.Unseen.Dequeue();
        string[] rRU = { "обычный", "редкий", "легендарный" }, rEN = { "common", "rare", "legendary" };
        switch (e.kind)
        {
            case RetEventKind.Box:
                RetBox b = C.boxes[e.a];
                bool dup = e.b != 0;
                Show(dup ? "Такой уже есть" : "Новый коробок!", dup ? "Already in the collection" : "New matchbox!",
                     dup ? $"{b.nameRU}\nПовтор: +{RetText.EmbersRu(C.dupEmbers[(int)b.rarity])}" : $"{b.nameRU}\n({rRU[(int)b.rarity]})",
                     dup ? $"{b.nameEN}\nRepeat: +{RetText.EmbersEn(C.dupEmbers[(int)b.rarity])}" : $"{b.nameEN}\n({rEN[(int)b.rarity]})", RetUi.Art(b.sprite));
                Reveal();
                break;
            case RetEventKind.Outfit:
                RetOutfit o = C.outfits[e.a];
                Show("Новый облик!", "New outfit!", $"{o.nameRU}\nНадень его огоньку в Очаге.", $"{o.nameEN}\nPut it on your flame in the Hearth.", RetUi.Art(o.sprite), "Надеть", "Wear it",
                     () => Retention.Instance.Equip(e.a), "Позже", "Later", () => { });
                break;
            case RetEventKind.Milestone:
                Show($"Серия {e.a}!", $"{e.a}-day streak!", $"+{RetText.EmbersRu(e.b)} и коробок в подарок. Спасибо, что заходишь!", $"+{RetText.EmbersEn(e.b)} and a box. Thanks for stopping by!", RetUi.Art("ic_ember"));
                break;
            case RetEventKind.WeekGoal:
                Show("Неделя выполнена!", "Week complete!", $"+{RetText.EmbersRu(e.a)}, +{RetText.CoinsRu(e.b)} и коробок", $"+{RetText.EmbersEn(e.a)}, +{RetText.CoinsEn(e.b)} and a box", RetUi.Art("s_chest"));
                break;
            case RetEventKind.WeekEvent:
                RetWeekTheme t = C.weekThemes[e.a];
                Show(t.nameRU, t.nameEN, $"Событие недели пройдено: +{RetText.EmbersRu(C.weeklyEventEmbers)}" + (e.b >= 0 ? $" и облик «{C.outfits[e.b].nameRU}»" : ""),
                     $"Weekly event complete: +{RetText.EmbersEn(C.weeklyEventEmbers)}" + (e.b >= 0 ? $" and the «{C.outfits[e.b].nameEN}» outfit" : ""), e.b >= 0 ? RetUi.Art(C.outfits[e.b].sprite) : RetUi.Art("s_star"));
                break;
            case RetEventKind.SeasonReward:
                Show("Праздничный подарок!", "Holiday gift!", "Костюм для огонька и праздничный коробок.", "An outfit for your flame and a holiday box.",
                     e.a >= 0 ? RetUi.Art(C.outfits[e.a].sprite) : RetUi.Art("s_gift"));
                break;
            case RetEventKind.SecretOpened:
                Show("Секретный коробок", "Secret box", e.a == 0 ? $"Внутри — облик «{C.outfits[e.b].nameRU}»!" : e.a == 1 ? $"Внутри — легендарный коробок «{C.boxes[e.b].nameRU}»!" : $"+{RetText.EmbersRu(e.b)}",
                     e.a == 0 ? $"Inside: the «{C.outfits[e.b].nameEN}» outfit!" : e.a == 1 ? $"Inside: the legendary «{C.boxes[e.b].nameEN}» box!" : $"+{RetText.EmbersEn(e.b)}",
                     e.a == 0 ? RetUi.Art(C.outfits[e.b].sprite) : RetUi.Art("ic_secret"));
                Reveal();
                break;
            case RetEventKind.SetDone:
                Show("Набор собран!", "Set complete!", $"+{RetText.EmbersRu(e.b)}", $"+{RetText.EmbersEn(e.b)}", RetUi.Art("s_star"));
                break;
            default:
                DrainUnseen();
                break;
        }
    }

    public void ShowStage(int stage, int line)
    {
        RetStage st = C.stages[stage];
        string outfit = st.outfit >= 0 ? $"\nНовый облик: «{C.outfits[st.outfit].nameRU}»" : "";
        string outfitEn = st.outfit >= 0 ? $"\nNew outfit: «{C.outfits[st.outfit].nameEN}»" : "";
        Show($"Огонёк вырос: {st.nameRU}!", $"Your flame grew: {st.nameEN}!",
             $"Линия: {C.fuelNamesRU[line]}. «{st.lineRU}»\n+{RetText.CoinsRu(st.coins)}{outfit}",
             $"Line: {C.fuelNamesEN[line]}. “{st.lineEN}”\n+{RetText.CoinsEn(st.coins)}{outfitEn}", null, flame: true);
        popupFlame.Setup(stage, line, Core.s.equippedOutfit, Core.Glow);
        popupFlame.Hop();
    }
}
