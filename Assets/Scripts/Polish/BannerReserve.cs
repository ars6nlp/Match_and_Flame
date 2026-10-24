using UnityEngine;

/// <summary>
/// Поднимает нижний край контейнера над рекламным баннером: отступ = VisualTheme.bannerReserveDp + bannerGapDp,
/// переведённые из dp в единицы Canvas (dp → пиксели по Screen.dpi, пиксели → единицы по scaleFactor Canvas).
/// Вешать на растянутый контейнер внутри SafeArea (он уже выше жестовой панели). Пересчёт — при смене размера экрана.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class BannerReserve : MonoBehaviour
{
    private RectTransform rectTransform;
    private Canvas canvas;
    private Vector2Int appliedScreen;
    private float appliedScale;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
    }

    private void Start() => Apply();

    private void Update()
    {
        if (Screen.width != appliedScreen.x || Screen.height != appliedScreen.y || (canvas != null && !Mathf.Approximately(canvas.scaleFactor, appliedScale)))
            Apply();
    }

    private void Apply()
    {
        appliedScreen = new Vector2Int(Screen.width, Screen.height);
        appliedScale = canvas != null ? canvas.scaleFactor : 1f;
        rectTransform.offsetMin = new Vector2(rectTransform.offsetMin.x, ReserveInCanvasUnits());
    }

    /// <summary>Высота баннера с зазором в единицах Canvas.</summary>
    public float ReserveInCanvasUnits()
    {
        VisualTheme theme = VisualTheme.Current;
        float dpi = Screen.dpi > 1f ? Screen.dpi : 160f * Screen.width / 411f; // нет данных — считаем телефон шириной 411 dp
        float pixels = (theme.bannerReserveDp + theme.bannerGapDp) * dpi / 160f;
        float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        return pixels / scale;
    }
}
