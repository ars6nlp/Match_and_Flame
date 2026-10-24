using UnityEngine;

/// <summary>
/// Повторяет прямоугольник другого элемента с запасом (мягкая тень под карточкой, высота которой задаётся содержимым).
/// Пересчёт только при изменении цели; без аллокаций. Работает и в редакторе.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class UIFollowRect : MonoBehaviour
{
    public RectTransform target;
    [Tooltip("Запас со всех сторон, единицы Canvas")]
    public float padding = 30f;
    [Tooltip("Сдвиг (тень вправо-вниз)")]
    public Vector2 offset = new Vector2(10f, -14f);

    private Vector2 lastSize, lastPos;
    private RectTransform self;

    private void LateUpdate() => Sync();

    public void Sync()
    {
        if (target == null) return;
        if (self == null) self = (RectTransform)transform;
        Vector2 size = target.rect.size, pos = target.anchoredPosition;
        if (size == lastSize && pos == lastPos) return;
        lastSize = size; lastPos = pos;
        self.anchorMin = target.anchorMin; self.anchorMax = target.anchorMax; self.pivot = target.pivot;
        self.sizeDelta = target.sizeDelta + Vector2.one * (padding * 2f);
        self.anchoredPosition = pos + offset;
    }
}
