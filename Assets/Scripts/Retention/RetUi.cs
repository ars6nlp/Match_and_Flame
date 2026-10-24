using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Тексты RU/EN по текущему языку.</summary>
public static class RetText
{
    public static bool Ru => LanguageInit.CurrentLang == "ru";
    public static string T(string ru, string en) => Ru ? ru : en;

    public static string Days(int n) => Ru ? $"{n} {Plural(n, "день", "дня", "дней")}" : $"{n} {(n == 1 ? "day" : "days")}";
    // Число и слово в нужной форме (R-09): «1 уголёк, 3 уголька, 5 угольков», «1 coin, 2 coins»
    public static string EmbersRu(int n) => $"{n} {Plural(n, "уголёк", "уголька", "угольков")}";
    public static string CoinsRu(int n) => $"{n} {Plural(n, "монета", "монеты", "монет")}";
    public static string WinsRu(int n) => $"{n} {Plural(n, "победа", "победы", "побед")}";
    public static string DaysRu(int n) => $"{n} {Plural(n, "день", "дня", "дней")}";
    public static string EmbersEn(int n) => n == 1 ? "1 ember" : $"{n} embers";
    public static string CoinsEn(int n) => n == 1 ? "1 coin" : $"{n} coins";
    public static string WinsEn(int n) => n == 1 ? "1 win" : $"{n} wins";
    public static string Plural(int n, string one, string few, string many)
    {
        int a = Math.Abs(n) % 100, b = a % 10;
        if (a > 10 && a < 20) return many;
        if (b == 1) return one;
        return b >= 2 && b <= 4 ? few : many;
    }
}

/// <summary>
/// Построение интерфейса «Очага» кодом в общем стиле игры (ui_card, ui_button, LiberationSans RU SDF).
/// Все тексты — через LocalizeText (RU/EN), чтобы смена языка в настройках обновляла их как остальные надписи.
/// </summary>
public static class RetUi
{
    public static RetentionSettings Cfg => RetentionSettings.Current;

    public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    public static RectTransform Fill(Transform parent, string name, float inset = 0f) =>
        Rect(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-2f * inset, -2f * inset));

    public static RectTransform At(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size) =>
        Rect(parent, name, anchor, anchor, new Vector2(0.5f, 0.5f), pos, size);

    public static Image Img(Transform parent, string name, Sprite sprite, Color color, Vector2 anchor, Vector2 pos, Vector2 size, bool sliced = false, bool raycast = false)
    {
        RectTransform rt = At(parent, name, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite; img.color = color; img.raycastTarget = raycast;
        img.type = sliced && sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        if (!sliced) img.preserveAspect = true;
        return img;
    }

    public static Image Panel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color? tint = null)
    {
        Image img = Img(parent, name, Cfg.card, tint ?? Color.white, anchor, pos, size, sliced: true, raycast: true);
        img.preserveAspect = false;
        return img;
    }

    public static TextMeshProUGUI Text(Transform parent, string name, string ru, string en, float size, Color color, Vector2 anchor, Vector2 pos, Vector2 box,
        TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false)
    {
        RectTransform rt = At(parent, name, anchor, pos, box);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (Cfg.font != null) t.font = Cfg.font;
        t.fontSize = size; t.enableAutoSizing = true; t.fontSizeMin = size * 0.55f; t.fontSizeMax = size;
        t.color = color; t.alignment = align; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        if (bold) t.fontStyle = FontStyles.Bold;
        var loc = rt.gameObject.AddComponent<LocalizeText>();
        loc.textRU = ru; loc.textEN = en;
        loc.UpdateText();
        return t;
    }

    /// <summary>Сменить текст (обе версии) — через LocalizeText, чтобы смена языка не возвращала старое.</summary>
    public static void Set(TextMeshProUGUI t, string ru, string en)
    {
        if (t == null) return;
        var loc = t.GetComponent<LocalizeText>();
        if (loc == null) { t.text = RetText.T(ru, en); return; }
        if (loc.textRU == ru && loc.textEN == en) return;
        loc.textRU = ru; loc.textEN = en;
        loc.UpdateText();
    }

    public static Button Button(Transform parent, string name, string ru, string en, Vector2 anchor, Vector2 pos, Vector2 size, UnityAction onClick, bool primary = true, float fontSize = 46f)
    {
        Image img = Img(parent, name, primary ? Cfg.button : Cfg.buttonLight, primary ? Color.white : new Color(0.56f, 0.31f, 0.18f), anchor, pos, size, sliced: true, raycast: true);
        img.preserveAspect = false;
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        if (onClick != null) b.onClick.AddListener(onClick);
        img.gameObject.AddComponent<UIButtonJuice>();
        img.gameObject.AddComponent<ButtonClickSound>();
        TextMeshProUGUI label = Text(img.transform, "Label", ru, en, fontSize, primary ? Cfg.textDark : Cfg.textLight, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(40f, 16f));
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return b;
    }

    public static TextMeshProUGUI Label(Button b) => b != null ? b.GetComponentInChildren<TextMeshProUGUI>(true) : null;

    /// <summary>Ловец касаний для фона окна (тап в любом месте закрывает подарок): не кнопка, не мешает навигации.</summary>
    public static void TapCatcher(GameObject go, UnityAction onTap)
    {
        Graphic g = go.GetComponent<Graphic>();
        if (g == null) { var img = go.AddComponent<Image>(); img.color = new Color(0, 0, 0, 0); g = img; }
        g.raycastTarget = true;
        go.AddComponent<RetTapCatcher>().onTap = onTap;
    }

    /// <summary>Невидимая кнопка поверх области (карточки-кнопки).</summary>
    public static Button HitArea(GameObject go, UnityAction onClick)
    {
        Graphic g = go.GetComponent<Graphic>();
        if (g == null) { var img = go.AddComponent<Image>(); img.color = new Color(0, 0, 0, 0); g = img; }
        g.raycastTarget = true;
        var b = go.AddComponent<Button>();
        b.targetGraphic = g;
        b.onClick.AddListener(onClick);
        go.AddComponent<UIButtonJuice>();
        go.AddComponent<ButtonClickSound>();
        return b;
    }

    /// <summary>Полоса прогресса: фон + заливка (возвращает заливку; ширину задаёт SetBar).</summary>
    public static Image Bar(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color fill)
    {
        Image bg = Img(parent, name, Cfg.pill, new Color(0.16f, 0.07f, 0.04f, 0.85f), anchor, pos, size, sliced: true);
        bg.preserveAspect = false;
        RectTransform f = Rect(bg.transform, "Fill", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(0f, -8f));
        var fi = f.gameObject.AddComponent<Image>();
        fi.sprite = Cfg.pill; fi.type = Image.Type.Sliced; fi.color = fill; fi.raycastTarget = false;
        return fi;
    }

    public static void SetBar(Image fill, float t)
    {
        if (fill == null) return;
        var bg = (RectTransform)fill.transform.parent;
        float w = Mathf.Max(0f, (bg.rect.width - 8f) * Mathf.Clamp01(t));
        var rt = (RectTransform)fill.transform;
        rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
        fill.enabled = w >= bg.rect.height;   // короче высоты полосы — скругление выглядит овалом, лучше не рисовать
    }

    /// <summary>Корневой Canvas «Очага» поверх сцены (ниже дверей перехода).</summary>
    public static Canvas Overlay(string name, int order)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    /// <summary>Окно на весь экран: затемнение (тап по нему не закрывает), карточка, заголовок, видимая кнопка «Назад».</summary>
    public static RectTransform Screen(Transform root, string name, string titleRU, string titleEN, UnityAction onClose, out RectTransform body, Vector2? cardSize = null)
    {
        RectTransform panel = Fill(root, name);
        var dim = panel.gameObject.AddComponent<Image>();
        dim.color = new Color(0.1f, 0.04f, 0.02f, 0.88f);
        dim.raycastTarget = true;
        panel.gameObject.AddComponent<CanvasGroup>();
        panel.gameObject.AddComponent<UIPanelAnimator>();
        RectTransform safe = Fill(panel, "Safe");
        safe.gameObject.AddComponent<SafeArea>();
        RectTransform content = Fill(safe, "Content");
        content.gameObject.AddComponent<BannerReserve>();   // карточка всегда над рекламным баннером
        Vector2 size = cardSize ?? new Vector2(1000f, 1460f);
        Image card = Panel(content, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, size);
        card.gameObject.AddComponent<RetFit>().design = size + new Vector2(40f, 30f);
        Text(card.transform, "Title", titleRU, titleEN, 72f, Cfg.title, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(size.x - 120f, 110f), bold: true);
        body = Rect(card.transform, "Body", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(-80f, -340f));
        Button(card.transform, "Close", "Назад", "Back", new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(420f, 128f), onClose, primary: false);
        return panel;
    }

    public static Sprite Art(string name) => Cfg.Art(name);
}
