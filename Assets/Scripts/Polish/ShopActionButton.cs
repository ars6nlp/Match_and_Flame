using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Кнопка-спичка магазина: пустая спичка + надпись (TMP, RU/EN через LocalizeText) + бейдж цены (значок золотой
/// спички и число) слева от надписи. Раскладка считается в коде по ширине тела спички (без головки), поэтому
/// надпись и цена не пересекаются ни на каком языке и ни в каком состоянии.
/// Состояния: Купить (с ценой; не хватает монет — цена красная), Выбрать, Выбрано (другой оттенок, без цены),
/// Смотреть рекламу (значок видео вместо цены).
/// </summary>
public class ShopActionButton : MonoBehaviour
{
    public enum State { Buy, Equip, Equipped, WatchAd }

    [Header("Части кнопки")]
    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private LocalizeText labelLocalize;
    [SerializeField] private RectTransform badge;
    [SerializeField] private Image badgeIcon;
    [SerializeField] private TextMeshProUGUI badgeText;
    [SerializeField] private Sprite goldMatchIcon;
    [SerializeField] private Sprite videoIcon;

    [Header("Тексты")]
    [SerializeField] private string buyEN = "Buy", buyRU = "Купить";
    [SerializeField] private string equipEN = "Equip", equipRU = "Выбрать";
    [SerializeField] private string equippedEN = "Equipped", equippedRU = "Выбрано";
    [SerializeField] private string adEN = "Watch", adRU = "Смотреть";

    [Header("Цвета (тёмный текст со светлым контуром — контраст к дереву ≥ 4.5:1)")]
    [SerializeField] private Color textColor = new Color(0.2f, 0.09f, 0.03f, 1f);
    [SerializeField] private Color outlineColor = new Color(1f, 0.93f, 0.78f, 0.6f);
    [Range(0f, 0.4f)] [SerializeField] private float outlineWidth = 0.16f;
    [SerializeField] private Color priceShortColor = new Color(0.78f, 0.08f, 0.04f, 1f);
    [SerializeField] private Color equippedTint = new Color(0.9f, 0.86f, 0.8f, 1f);
    [SerializeField] private Color equippedTextColor = new Color(0.16f, 0.08f, 0.03f, 1f);

    [Header("Раскладка (доли прямоугольника кнопки)")]
    [Tooltip("Тело спички без головки: от и до по ширине")]
    [SerializeField] private Vector2 bodyX = new Vector2(0.05f, 0.79f);
    [SerializeField] private Vector2 bodyY = new Vector2(0.14f, 0.86f);
    [SerializeField] private float gap = 16f;
    [SerializeField] private float labelMaxSize = 60f, labelMinSize = 28f, priceSize = 50f;

    public State Current { get; private set; }
    public RectTransform Badge => badge;
    public TextMeshProUGUI Label => label;
    public TextMeshProUGUI PriceText => badgeText;

    public void Show(State state, int price, bool affordable)
    {
        Current = state;
        bool showBadge = state == State.Buy || state == State.WatchAd;
        if (badge != null) badge.gameObject.SetActive(showBadge);
        if (badgeIcon != null) badgeIcon.sprite = state == State.WatchAd ? videoIcon : goldMatchIcon;
        if (badgeText != null)
        {
            badgeText.gameObject.SetActive(state == State.Buy);
            badgeText.text = price.ToString();
            badgeText.color = affordable ? textColor : priceShortColor;
        }
        string en = state == State.Buy ? buyEN : state == State.Equip ? equipEN : state == State.Equipped ? equippedEN : adEN;
        string ru = state == State.Buy ? buyRU : state == State.Equip ? equipRU : state == State.Equipped ? equippedRU : adRU;
        if (labelLocalize != null) { labelLocalize.textEN = en; labelLocalize.textRU = ru; labelLocalize.UpdateText(); }
        else if (label != null) label.text = LanguageInit.CurrentLang == "ru" ? ru : en;
        if (label != null) label.color = state == State.Equipped ? equippedTextColor : textColor;
        if (background != null) background.color = state == State.Equipped ? equippedTint : Color.white;
        ApplyStyle();
        Layout();
    }

    private void OnEnable() { LanguageInit.LanguageChanged += Layout; }
    private void OnDisable() { LanguageInit.LanguageChanged -= Layout; }

    private void ApplyStyle()
    {
        foreach (TextMeshProUGUI t in new[] { label, badgeText })
        {
            if (t == null) continue;
            t.characterSpacing = 0f;
            t.outlineWidth = outlineWidth;
            t.outlineColor = outlineColor;
        }
    }

    /// <summary>Бейдж цены слева, надпись — в оставшейся части тела спички, авторазмер с минимумом.</summary>
    public void Layout()
    {
        var rt = (RectTransform)transform;
        float w = rt.rect.width, h = rt.rect.height;
        if (w < 1f || h < 1f || label == null) return;
        float left = bodyX.x * w, right = bodyX.y * w, bodyH = (bodyY.y - bodyY.x) * h;
        float x = left;
        if (badge != null && badge.gameObject.activeSelf)
        {
            float icon = bodyH * 0.92f, numW = 0f;
            if (badgeText != null && badgeText.gameObject.activeSelf)
            {
                badgeText.enableAutoSizing = false;
                badgeText.fontSize = priceSize;
                numW = badgeText.GetPreferredValues(badgeText.text, 1000f, bodyH).x + 4f;
            }
            float bw = icon + (numW > 0f ? 6f + numW : 0f);
            Place(badge, x, bw, bodyH, w, h);
            if (badgeIcon != null) Place((RectTransform)badgeIcon.transform, 0f, icon, icon, bw, bodyH, true);
            if (badgeText != null && numW > 0f) Place(badgeText.rectTransform, icon + 6f, numW, bodyH, bw, bodyH, true);
            x += bw + gap;
        }
        label.enableAutoSizing = true;
        label.fontSizeMax = labelMaxSize;
        label.fontSizeMin = labelMinSize;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.alignment = TextAlignmentOptions.Center;
        Place(label.rectTransform, x, Mathf.Max(10f, right - x), bodyH, w, h);
    }

    /// <summary>Прямоугольник шириной width от x (единицы родителя), по вертикали — тело спички (или весь родитель).</summary>
    private void Place(RectTransform r, float x, float width, float height, float parentW, float parentH, bool inParent = false)
    {
        r.pivot = new Vector2(0f, 0.5f);
        r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
        float cy = inParent ? 0f : ((bodyY.x + bodyY.y) * 0.5f - 0.5f) * parentH;
        r.anchoredPosition = new Vector2(x, cy);
        r.sizeDelta = new Vector2(width, height);
        r.localScale = Vector3.one;
    }

    private void Place(RectTransform r, float x, float width, float height, float parentW, float parentH) => Place(r, x, width, height, parentW, parentH, false);
}
