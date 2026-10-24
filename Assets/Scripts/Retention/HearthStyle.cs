using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public enum TextLevel { Title, Subtitle, Body, Caption }
public enum CardKind { Window, Inner, Accent, Done, Muted }
public enum ButtonKind { Primary, Secondary, Done }

/// <summary>
/// Дизайн-система «Очага»: 4 уровня текста, светлые карточки, главная/вторичная кнопки одной высоты (56 dp),
/// полоса прогресса (светлая дорожка, амбер с бликом, число поверх), плашки ресурса «значок + число», мягкий бейдж,
/// разделитель, прокрутка и окно одной ширины. Цвета, кегли и размеры — в <see cref="HearthVisualSettings"/> («Оформление»).
/// Тексты RU/EN — через LocalizeText (RetUi.Text); числа — отдельным TMP без LocalizeText (смена языка их не стирает).
/// </summary>
public static class HearthStyle
{
    private static HearthVisualSettings V => HearthVisualSettings.Current;
    private static RetentionSettings C => RetentionSettings.Current;

    public static float Size(TextLevel l) => l == TextLevel.Title ? V.titleSize : l == TextLevel.Subtitle ? V.subtitleSize : l == TextLevel.Body ? V.bodySize : V.captionSize;

    /// <summary>Текст уровня l: заголовки — жирные; автоподбор не мельче 14 sp.</summary>
    public static TextMeshProUGUI Text(Transform parent, string name, string ru, string en, TextLevel l, Color color, Vector2 anchor, Vector2 pos, Vector2 box,
        TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        float size = Size(l);
        TextMeshProUGUI t = RetUi.Text(parent, name, ru, en, size, color, anchor, pos, box, align, bold: l == TextLevel.Title || l == TextLevel.Subtitle);
        t.fontSizeMin = Mathf.Min(size, V.minAutoSize);
        return t;
    }

    /// <summary>Число или строка из кода без перевода (счётчики, даты): без LocalizeText.</summary>
    public static TextMeshProUGUI Num(Transform parent, string name, TextLevel l, Color color, Vector2 anchor, Vector2 pos, Vector2 box,
        TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = true)
    {
        RectTransform rt = RetUi.At(parent, name, anchor, pos, box);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (C.font != null) t.font = C.font;
        float size = Size(l);
        t.fontSize = size; t.enableAutoSizing = true; t.fontSizeMin = Mathf.Min(size, V.minAutoSize); t.fontSizeMax = size;
        t.color = color; t.alignment = align; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        if (bold) t.fontStyle = FontStyles.Bold;
        return t;
    }

    public static Image Sliced(Transform parent, string name, string sprite, Color color, Vector2 anchor, Vector2 pos, Vector2 size, bool raycast = false)
    {
        Image img = RetUi.Img(parent, name, RetUi.Art(sprite), color, anchor, pos, size, sliced: true, raycast: raycast);
        img.preserveAspect = false;
        return img;
    }

    public static Image Icon(Transform parent, string name, string sprite, Vector2 anchor, Vector2 pos, float size, Color? color = null) =>
        RetUi.Img(parent, name, RetUi.Art(sprite), color ?? Color.white, anchor, pos, new Vector2(size, size));

    /// <summary>Карточка: окно (крем с медной рамкой), вложенная (светлая), акцент (тёплая), готово (мятная), приглушённая.</summary>
    public static Image Card(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, CardKind kind = CardKind.Inner, bool raycast = false)
    {
        string sprite = kind == CardKind.Window ? "ui_card" : "ui_inner";
        Color tint = kind == CardKind.Accent ? V.accentTint : kind == CardKind.Done ? V.mintTint : kind == CardKind.Muted ? V.mutedTint : Color.white;
        return Sliced(parent, name, sprite, tint, anchor, pos, size, raycast);
    }

    public static void SetCard(Image card, CardKind kind) =>
        card.color = kind == CardKind.Accent ? V.accentTint : kind == CardKind.Done ? V.mintTint : kind == CardKind.Muted ? V.mutedTint : Color.white;

    /// <summary>Кнопка: главная (амбер), вторичная (крем с медью), «готово» (мята). Высота по умолчанию — 56 dp.</summary>
    public static Button Button(Transform parent, string name, string ru, string en, Vector2 anchor, Vector2 pos, float width, UnityAction onClick,
        ButtonKind kind = ButtonKind.Primary, float height = -1f, TextLevel level = TextLevel.Body)
    {
        if (height <= 0f) height = V.buttonHeight;
        string sprite = kind == ButtonKind.Primary ? "ui_btn_amber" : kind == ButtonKind.Done ? "ui_btn_mint" : "ui_btn_cream";
        Image img = Sliced(parent, name, sprite, Color.white, anchor, pos, new Vector2(width, height), raycast: true);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var colors = b.colors; colors.disabledColor = new Color(0.82f, 0.8f, 0.78f, 0.8f); b.colors = colors;
        if (onClick != null) b.onClick.AddListener(onClick);
        img.gameObject.AddComponent<UIButtonJuice>();
        img.gameObject.AddComponent<ButtonClickSound>();
        Color ink = V.ink;   // тёмный текст и на мятной кнопке (контраст ≥ 4.5:1)
        TextMeshProUGUI label = Text(img.transform, "Label", ru, en, level, ink, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(width - 48f, height - 36f));
        label.fontStyle = FontStyles.Bold;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return b;
    }

    public static void SetButton(Button b, ButtonKind kind)
    {
        var img = (Image)b.targetGraphic;
        img.sprite = RetUi.Art(kind == ButtonKind.Primary ? "ui_btn_amber" : kind == ButtonKind.Done ? "ui_btn_mint" : "ui_btn_cream");
        TextMeshProUGUI l = RetUi.Label(b);
        if (l != null) l.color = V.ink;
    }

    /// <summary>Полоса прогресса: светлая дорожка, амбер с бликом, число поверх полосы.</summary>
    public static RetBar Bar(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, bool label = true)
    {
        Image track = Sliced(parent, name, "ui_track", Color.white, anchor, pos, size);
        float inset = Mathf.Round(size.y * 0.12f);
        RectTransform f = RetUi.Rect(track.transform, "Fill", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(inset, 0f), new Vector2(0f, -2f * inset));
        var fi = f.gameObject.AddComponent<Image>();
        fi.sprite = RetUi.Art("ui_fill"); fi.type = Image.Type.Sliced; fi.raycastTarget = false;
        var bar = track.gameObject.AddComponent<RetBar>();
        bar.fill = fi; bar.inset = inset;
        if (label)
            bar.label = Num(track.transform, "Value", size.y >= 52f ? TextLevel.Caption : TextLevel.Caption, V.ink, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(size.x - 24f, Mathf.Max(size.y, 44f)));
        return bar;
    }

    /// <summary>Плашка ресурса: значок + число (подпись ресурса — один раз, рядом или в подсказке).</summary>
    public static TextMeshProUGUI Plaque(Transform parent, string name, string icon, Vector2 anchor, Vector2 pos, float width, out Image plaque, float height = 96f)
    {
        plaque = Sliced(parent, name, "ui_tag", Color.white, anchor, pos, new Vector2(width, height));
        Icon(plaque.transform, "Icon", icon, new Vector2(0f, 0.5f), new Vector2(height * 0.5f + 6f, 0f), height * 0.72f);
        return Num(plaque.transform, "Value", TextLevel.Body, V.ink, new Vector2(0f, 0.5f), new Vector2(height + (width - height) * 0.5f - 8f, 2f),
            new Vector2(width - height - 20f, height - 20f));
    }

    /// <summary>Мягкий бейдж «новое» в углу.</summary>
    public static Image Badge(Transform parent, Vector2 anchor, Vector2 pos, float size = 40f)
    {
        Image b = RetUi.Img(parent, "Badge", RetUi.Art("ui_badge"), Color.white, anchor, pos, new Vector2(size, size));
        b.gameObject.AddComponent<RetBadgePulse>();
        return b;
    }

    public static Image Divider(Transform parent, Vector2 anchor, Vector2 pos, float width) =>
        RetUi.Img(parent, "Divider", RetUi.Art("ui_divider"), Color.white, anchor, pos, new Vector2(width, 8f));

    /// <summary>Вертикальная прокрутка внутри области (маска RectMask2D): возвращает содержимое, его высоту задаёт вызывающий.</summary>
    public static RectTransform Scroll(RectTransform area, string name = "Scroll")
    {
        RectTransform view = RetUi.Fill(area, name);
        var hit = view.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        view.gameObject.AddComponent<RectMask2D>();
        RectTransform content = RetUi.Rect(view, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 100f));
        var sr = view.gameObject.AddComponent<ScrollRect>();
        sr.content = content; sr.viewport = view; sr.horizontal = false; sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Elastic; sr.inertia = true; sr.decelerationRate = 0.12f; sr.scrollSensitivity = 30f;
        return content;
    }

    /// <summary>
    /// Окно «Очага»: затемнение, безопасная зона, место под баннер, карточка одной ширины (крем с медной рамкой),
    /// заголовок, тело и видимая кнопка «Назад», закреплённая внизу карточки. RetFit уменьшает карточку на коротких экранах.
    /// </summary>
    public static RectTransform Window(Transform root, string name, string titleRU, string titleEN, UnityAction onClose, out RectTransform body, float height = -1f)
    {
        if (height <= 0f) height = V.windowMaxHeight;
        RectTransform panel = RetUi.Fill(root, name);
        var dim = panel.gameObject.AddComponent<Image>();
        dim.color = V.dim;
        dim.raycastTarget = true;
        panel.gameObject.AddComponent<CanvasGroup>();
        panel.gameObject.AddComponent<UIPanelAnimator>();
        RectTransform safe = RetUi.Fill(panel, "Safe");
        safe.gameObject.AddComponent<SafeArea>();
        RectTransform content = RetUi.Fill(safe, "Content");
        content.gameObject.AddComponent<BannerReserve>();
        Vector2 size = new Vector2(V.windowWidth, height);
        Image shadow = Sliced(content, "Shadow", "ui_shadow", new Color(1f, 1f, 1f, 0.8f), new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), size + new Vector2(36f, 36f));
        Image card = Card(content, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, size, CardKind.Window, raycast: true);
        var fit = card.gameObject.AddComponent<RetFit>(); fit.design = size + new Vector2(40f, 30f); fit.follower = shadow.rectTransform;
        Text(card.transform, "Title", titleRU, titleEN, TextLevel.Title, V.heading, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(size.x - 120f, 96f));
        float bottom = V.buttonHeight + 56f;
        body = RetUi.Rect(card.transform, "Body", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, (bottom - 140f) * 0.5f), new Vector2(-2f * V.padding, -(140f + bottom)));
        Button(card.transform, "Close", "Назад", "Back", new Vector2(0.5f, 0f), new Vector2(0f, 28f + V.buttonHeight * 0.5f), 440f, onClose, ButtonKind.Secondary);
        return panel;
    }

    /// <summary>Текст про награду: «+25» со значком — для строк наград (значок слева, число и подпись справа).</summary>
    public static TextMeshProUGUI IconLine(Transform parent, string name, string icon, string ru, string en, Vector2 anchor, Vector2 pos, float width, TextLevel l = TextLevel.Body, Color? color = null)
    {
        RectTransform row = RetUi.At(parent, name, anchor, pos, new Vector2(width, 64f));
        Icon(row, "Icon", icon, new Vector2(0f, 0.5f), new Vector2(30f, 0f), 56f);
        return Text(row, "Text", ru, en, l, color ?? V.ink, new Vector2(0f, 0.5f), new Vector2(72f + (width - 72f) * 0.5f, 0f), new Vector2(width - 72f, 64f), TextAlignmentOptions.Left);
    }
}

/// <summary>Полоса прогресса «Очага»: плавное заполнение (unscaled, без выделений), число поверх.</summary>
public class RetBar : MonoBehaviour
{
    public Image fill;
    public TextMeshProUGUI label;
    public float inset;
    private float shown = -1f, from, target, startAt = -1f;

    public void Set(float t, string text = null, bool animate = true)
    {
        t = Mathf.Clamp01(t);
        if (label != null && text != null && label.text != text) label.text = text;
        if (Mathf.Approximately(t, target) && shown >= 0f) return;
        HearthVisualSettings v = HearthVisualSettings.Current;
        if (!animate || !v.uiAnimations || shown < 0f || !isActiveAndEnabled) { shown = target = t; startAt = -1f; Apply(t); return; }
        from = shown; target = t; startAt = Time.unscaledTime;
    }

    private void OnRectTransformDimensionsChange() { if (shown >= 0f) Apply(shown); }

    private void Update()
    {
        if (startAt < 0f) return;
        float k = Mathf.Clamp01((Time.unscaledTime - startAt) / Mathf.Max(0.01f, HearthVisualSettings.Current.barSeconds));
        shown = Mathf.Lerp(from, target, 1f - (1f - k) * (1f - k));
        Apply(shown);
        if (k >= 1f) startAt = -1f;
    }

    private void Apply(float t)
    {
        if (fill == null) return;
        var bg = (RectTransform)transform;
        float full = Mathf.Max(0f, bg.rect.width - 2f * inset), h = Mathf.Max(0f, bg.rect.height - 2f * inset);
        float w = t <= 0f ? 0f : Mathf.Max(h, full * t);   // короче высоты — круглая «точка», чтобы малый прогресс был виден
        var rt = (RectTransform)fill.transform;
        rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
        fill.enabled = w > 0f;
    }
}

/// <summary>Мягкий пульс бейджа «новое» (масштаб 1 → 1.12), выключается флагом анимаций.</summary>
public class RetBadgePulse : MonoBehaviour
{
    private float phase;
    private void OnEnable() { phase = Random.value * 6f; }
    private void Update()
    {
        if (!HearthVisualSettings.Current.uiAnimations) { if (transform.localScale.x != 1f) transform.localScale = Vector3.one; return; }
        float s = 1f + 0.06f * (1f + Mathf.Sin(Time.unscaledTime * 3.2f + phase));
        transform.localScale = new Vector3(s, s, 1f);
    }
}

/// <summary>Появление элемента: fade + scale за appearSeconds (unscaled). Запуск — Play(); без флага анимаций — сразу виден.</summary>
public class RetAppear : MonoBehaviour
{
    private CanvasGroup group;
    private float at = -1f;
    private float from = 0.9f;

    public static RetAppear On(Component c)
    {
        var a = c.GetComponent<RetAppear>();
        if (a == null) a = c.gameObject.AddComponent<RetAppear>();
        return a;
    }

    public void Play(float startScale = 0.9f)
    {
        if (group == null) { group = GetComponent<CanvasGroup>(); if (group == null) group = gameObject.AddComponent<CanvasGroup>(); }
        from = startScale;
        if (!HearthVisualSettings.Current.uiAnimations) { group.alpha = 1f; transform.localScale = Vector3.one; at = -1f; return; }
        at = Time.unscaledTime;
        group.alpha = 0f;
        transform.localScale = Vector3.one * from;
    }

    private void Update()
    {
        if (at < 0f) return;
        float k = Mathf.Clamp01((Time.unscaledTime - at) / Mathf.Max(0.01f, HearthVisualSettings.Current.appearSeconds));
        float e = 1f - (1f - k) * (1f - k);
        group.alpha = e;
        float s = Mathf.Lerp(from, 1f, e) + (k < 1f ? 0.03f * Mathf.Sin(k * Mathf.PI) : 0f);
        transform.localScale = new Vector3(s, s, 1f);
        if (k >= 1f) { at = -1f; transform.localScale = Vector3.one; }
    }
}
