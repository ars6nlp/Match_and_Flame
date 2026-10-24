using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Огонёк главного меню прежнего размера: небольшой, стоит на полу по центру над кнопкой «Играть».
///
/// Кадры огонька (FLAME_MAIN, 1280x720) непрозрачные — в них стена, пол и сам огонёк. Раньше AspectRatioFitter
/// (Envelope) растягивал кадр на весь высокий экран, и огонёк становился огромным. Теперь ширина кадра считается
/// от ширины Canvas так, чтобы огонёк занимал <see cref="flameWidthOfScreen"/> экрана на любом соотношении сторон,
/// а сверху и снизу кадр продолжают зеркальные полосы стены и пола (Tools/menu_flame/make_backdrop.py) — стык
/// совпадает с краем кадра по пикселям. Свечение и искры привязаны к размеру огонька.
/// Пересчёт только при смене размера экрана, положения «Играть», скина или оттенка — без выделений в кадре.
/// </summary>
[ExecuteAlways]
public class MenuFlameLayout : MonoBehaviour
{
    [Header("Огонёк")]
    [SerializeField] private RectTransform frame;             // Image с кадрами (UISkinSequenceAnimator)
    [SerializeField] private UISkinSequenceAnimator animator;
    [SerializeField] private RectTransform standAbove;        // кнопка «Играть»: огонёк стоит над ней
    [Tooltip("Ширина огонька — доля ширины экрана (Canvas)")]
    [Range(0.05f, 0.3f)] public float flameWidthOfScreen = 0.13f;
    [Tooltip("Зазор между верхом «Играть» и основанием огонька, в высотах огонька")]
    public float gapAbove = 0.3f;
    [Tooltip("Магазин: пока панель открыта, огонёк (тот же размер) стоит между стрелками выбора скина")]
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private RectTransform shopArrow;

    [Header("Где огонёк внутри кадра (доли кадра 1280x720, кадр 25)")]
    public float flameCenterX = 0.506f;
    public float flameWidthInFrame = 0.107f;
    public float flameBaseInFrame = 0.183f;     // основание от низа кадра
    public float flameHeightInFrame = 0.268f;

    [Header("Продолжение фона")]
    [SerializeField] private Image wallExtension;             // Tiled, тайл тянется вверх от кадра
    [SerializeField] private Image floorExtension;            // Tiled, отражён по Y, тянется вниз от кадра
    [SerializeField] private Sprite[] floorBySkin;            // по индексу скина UISkinSequenceAnimator
    [SerializeField] private Image floorShade;                // тень поверх продолжения пола: градиент от стыка
    [SerializeField] private Image floorShadeSolid;           // и ровная тень ниже градиента
    [Tooltip("Коробок спичек на полу у правого края — закрывает водяной знак Gemini, вшитый в кадры. " +
             "Дочерний объект кадра с якорями в долях кадра (Tools/menu_flame/make_backdrop.py, COVER_RECT): двигается и масштабируется вместе с ним")]
    [SerializeField] private Image watermarkCover;
    [Tooltip("Длина градиента тени под кадром, в высотах огонька")]
    public float floorShadeLength = 0.8f;
    [Tooltip("Насколько тень заходит на низ кадра, в долях высоты кадра")]
    public float floorShadeOverlap = 0.14f;

    [Header("Свечение и искры (размеры — от огонька)")]
    [SerializeField] private RectTransform glow;
    public float glowSize = 3.2f;                // диаметр свечения в ширинах огонька
    [SerializeField] private RectTransform embers;
    public Vector2 embersArea = new Vector2(2.6f, 2.4f);   // ширина и высота зоны искр в размерах огонька

    private Image frameImage;
    private Vector2 lastSize = new Vector2(-1f, -1f);
    private Vector3 lastStand;
    private bool lastShop;
    private int lastSkin = -1;
    private Color lastColor = new Color(-1f, 0f, 0f, 0f);
    private readonly Vector3[] corners = new Vector3[4];

    // Размеры и позиции ведёт этот компонент (как LayoutGroup): в редакторе они не сохраняются как правки сцены
    private DrivenRectTransformTracker tracker;

    private void OnEnable()
    {
        lastSize = new Vector2(-1f, -1f);
        Apply();
    }

    private void OnDisable() => tracker.Clear();

    private void Drive(RectTransform rt, DrivenTransformProperties props)
    {
        if (rt != null) tracker.Add(this, rt, props);
    }

    private void LateUpdate()
    {
        if (frame == null) return;
        var parent = (RectTransform)frame.parent;
        Vector3 stand = standAbove != null ? standAbove.position : Vector3.zero;
        if (parent.rect.size != lastSize || stand != lastStand || ShopOpen != lastShop) Apply();
        SyncSkin();
    }

    /// <summary>Разложить огонёк, фон, свечение и искры (вызывается и из проверок в редакторе).</summary>
    public void Apply()
    {
        if (frame == null) return;
        var parent = (RectTransform)frame.parent;
        Rect pr = parent.rect;
        lastSize = pr.size;
        lastStand = standAbove != null ? standAbove.position : Vector3.zero;
        lastShop = ShopOpen;
        tracker.Clear();
        const DrivenTransformProperties rect = DrivenTransformProperties.Anchors | DrivenTransformProperties.Pivot | DrivenTransformProperties.SizeDelta | DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.Scale;
        Drive(frame, rect);
        if (wallExtension != null) Drive(wallExtension.rectTransform, rect);
        if (floorExtension != null) Drive(floorExtension.rectTransform, rect);
        if (floorShade != null) Drive(floorShade.rectTransform, rect);
        if (floorShadeSolid != null) Drive(floorShadeSolid.rectTransform, rect);
        Drive(glow, DrivenTransformProperties.Anchors | DrivenTransformProperties.Pivot | DrivenTransformProperties.SizeDelta | DrivenTransformProperties.AnchoredPosition);
        Drive(embers, DrivenTransformProperties.Anchors | DrivenTransformProperties.Pivot | DrivenTransformProperties.SizeDelta | DrivenTransformProperties.AnchoredPosition);

        float frameW = pr.width * flameWidthOfScreen / flameWidthInFrame;
        float frameH = frameW * 720f / 1280f;
        float flameW = frameW * flameWidthInFrame, flameH = frameH * flameHeightInFrame;

        float baseY = pr.yMin + pr.height * 0.25f;
        if (lastShop && shopArrow != null)
        {
            // центр огонька — на высоте центра стрелок
            baseY = parent.InverseTransformPoint(shopArrow.TransformPoint(shopArrow.rect.center)).y - flameH * 0.5f;
        }
        else if (standAbove != null)
        {
            standAbove.GetWorldCorners(corners);
            baseY = parent.InverseTransformPoint(corners[1]).y + flameH * gapAbove;
        }
        float frameBottom = baseY - flameBaseInFrame * frameH;
        float frameTop = frameBottom + frameH;

        Place(frame, pr, frameW, frameH, frameBottom, false);
        if (wallExtension != null)
            Place(wallExtension.rectTransform, pr, frameW, Mathf.Max(0f, pr.yMax - frameTop) + 4f, frameTop, false);
        if (floorExtension != null)
            Place(floorExtension.rectTransform, pr, frameW, Mathf.Max(0f, frameBottom - pr.yMin) + 4f, frameBottom, true);
        FitTile(wallExtension, frameW);
        FitTile(floorExtension, frameW);
        if (floorShade != null)
        {
            // тень начинается чуть выше стыка (в нижней полосе кадра, ниже основания огонька) — стык уходит в плавный переход
            float top = frameBottom + frameH * Mathf.Min(floorShadeOverlap, flameBaseInFrame * 0.8f);
            float len = Mathf.Max(1f, flameH * floorShadeLength);
            PlaceDown(floorShade.rectTransform, pr, frameW, len, top);                    // градиент
            if (floorShadeSolid != null)
                PlaceDown(floorShadeSolid.rectTransform, pr, frameW, Mathf.Max(0f, top - len - pr.yMin) + 4f, top - len);   // дальше — ровная тень
        }

        // Центр и основание огонька в мировых координатах — для свечения и искр (у них свой Canvas)
        Vector3 flameCenter = parent.TransformPoint(new Vector3(pr.center.x, baseY + flameH * 0.5f, 0f));
        Vector3 flameBase = parent.TransformPoint(new Vector3(pr.center.x, baseY, 0f));
        float toWorld = parent.lossyScale.x;
        if (glow != null)
        {
            glow.anchorMin = glow.anchorMax = glow.pivot = new Vector2(0.5f, 0.5f);
            glow.sizeDelta = Vector2.one * (flameW * glowSize * toWorld / Mathf.Max(1e-5f, glow.lossyScale.x));
            glow.position = flameCenter;
        }
        if (embers != null)
        {
            embers.anchorMin = embers.anchorMax = new Vector2(0.5f, 0.5f);
            embers.pivot = new Vector2(0.5f, 0f);
            float k = toWorld / Mathf.Max(1e-5f, embers.lossyScale.x);
            embers.sizeDelta = new Vector2(flameW * embersArea.x, flameH * embersArea.y) * k;
            embers.position = flameBase;
        }
        lastSkin = -1;
        SyncSkin();
    }

    // Прямоугольник шириной w, верхний край на высоте topY, тянется вниз на h
    private void PlaceDown(RectTransform rt, Rect pr, float w, float h, float topY)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(flameCenterX, 1f);
        rt.localScale = Vector3.one;
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(0f, topY - pr.center.y);
    }

    private bool ShopOpen => shopPanel != null && shopPanel.activeInHierarchy;

    // Прямоугольник шириной w по центру огонька; нижний край (или верхний у отражённого) на высоте edgeY
    private void Place(RectTransform rt, Rect pr, float w, float h, float edgeY, bool flipped)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(flameCenterX, 0f);
        rt.localScale = new Vector3(1f, flipped ? -1f : 1f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(0f, edgeY - pr.center.y);
    }

    // Один тайл по ширине кадра: тот же масштаб, что у кадра, — стык совпадает
    private static void FitTile(Image img, float frameW)
    {
        if (img == null || img.sprite == null || img.canvas == null) return;
        img.type = Image.Type.Tiled;
        img.pixelsPerUnitMultiplier = img.sprite.rect.width * img.canvas.referencePixelsPerUnit / (img.sprite.pixelsPerUnit * frameW);
    }

    // Пол под кадром — по скину; оттенок оттеночных скинов красит и продолжение фона
    private void SyncSkin()
    {
        if (frameImage == null && frame != null) frameImage = frame.GetComponent<Image>();
        int skin = animator != null ? animator.CurrentSkin : 0;
        if (skin != lastSkin)
        {
            lastSkin = skin;
            if (floorExtension != null && floorBySkin != null && floorBySkin.Length > 0)
                floorExtension.sprite = floorBySkin[Mathf.Clamp(skin, 0, floorBySkin.Length - 1)];
        }
        Color c = frameImage != null ? frameImage.color : Color.white;
        if (c != lastColor)
        {
            lastColor = c;
            if (wallExtension != null) wallExtension.color = c;
            if (watermarkCover != null) watermarkCover.color = c;   // оттеночный скин красит всю комнату — и коробок
            if (floorExtension != null) floorExtension.color = c;
        }
    }
}
