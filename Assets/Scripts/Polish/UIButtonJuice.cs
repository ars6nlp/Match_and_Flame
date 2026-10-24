using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Отклик кнопки: при нажатии сжимается, при отпускании отскакивает с лёгкой пружиной.
/// Работает на паузе (unscaled time). Параметры — в VisualTheme («Кнопки»).
/// Для тумблеров анимируется их картинка (targetGraphic), а не маленький корневой RectTransform.
/// </summary>
[RequireComponent(typeof(Selectable))]
public class UIButtonJuice : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Tooltip("Что сжимать. Пусто — targetGraphic кнопки, а если его нет — сам объект")]
    [SerializeField] private RectTransform target;

    private Selectable selectable;
    private UIScaleDriver driver;
    private bool pressed;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
        if (target == null)
            target = selectable.targetGraphic != null ? selectable.targetGraphic.rectTransform : (RectTransform)transform;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        VisualTheme theme = VisualTheme.Current;
        if (!theme.buttonJuice || !selectable.IsInteractable()) return;
        if (driver == null) driver = UIScaleDriver.For(target);
        pressed = true;
        driver.AnimatePress(theme.pressScale, theme.pressDuration, Ease.Type.OutQuad);
    }

    public void OnPointerUp(PointerEventData eventData) => Release();

    public void OnPointerExit(PointerEventData eventData) => Release();

    private void Release()
    {
        if (!pressed || driver == null) return;
        pressed = false;
        VisualTheme theme = VisualTheme.Current;
        driver.AnimatePress(1f, theme.releaseDuration, Ease.Type.OutBack, theme.releaseOvershoot);
    }

    private void OnDisable()
    {
        pressed = false;
        if (driver != null) driver.ResetToRest();
    }
}
