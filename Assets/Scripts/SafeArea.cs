using UnityEngine;

/// <summary>
/// Подгоняет RectTransform под безопасную зону экрана (вырез камеры, скруглённые углы,
/// жестовая панель). Вешать на пустой растянутый контейнер внутри Canvas и класть в него
/// элементы, привязанные к краям экрана. Фоны оставлять снаружи — они должны уходить под вырез.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect appliedArea;
    private Vector2Int appliedScreen;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        Apply();
    }

    // Безопасная зона меняется при повороте, складывании и смене режима окна
    private void Update()
    {
        if (Screen.safeArea != appliedArea || Screen.width != appliedScreen.x || Screen.height != appliedScreen.y)
            Apply();
    }

    private void Apply()
    {
        appliedArea = Screen.safeArea;
        appliedScreen = new Vector2Int(Screen.width, Screen.height);
        if (Screen.width <= 0 || Screen.height <= 0) return;

        Vector2 min = appliedArea.position;
        Vector2 max = appliedArea.position + appliedArea.size;
        min.x /= Screen.width;
        min.y /= Screen.height;
        max.x /= Screen.width;
        max.y /= Screen.height;

        rectTransform.anchorMin = min;
        rectTransform.anchorMax = max;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}
