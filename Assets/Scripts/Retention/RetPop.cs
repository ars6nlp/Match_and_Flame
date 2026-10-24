using UnityEngine;

/// <summary>Касание по фону окна (без Selectable).</summary>
public class RetTapCatcher : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
{
    public UnityEngine.Events.UnityAction onTap;
    public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e) => onTap?.Invoke();
}

/// <summary>Вписать карточку в родителя (над баннером, в безопасной зоне): масштаб ≤ 1, пересчёт только при смене размера.</summary>
public class RetFit : MonoBehaviour
{
    public Vector2 design;
    [Tooltip("Тот же масштаб (тень под карточкой)")] public RectTransform follower;
    private Vector2 last;
    private void LateUpdate()
    {
        var parent = (RectTransform)transform.parent;
        Vector2 size = parent.rect.size;
        if (size == last) return;
        last = size;
        float s = Mathf.Min(1f, Mathf.Min(size.x / Mathf.Max(1f, design.x), size.y / Mathf.Max(1f, design.y)));
        transform.localScale = new Vector3(s, s, 1f);
        if (follower != null) follower.localScale = transform.localScale;
    }
}

/// <summary>Маленькие анимации интерфейса без аллокаций: пружинка, потряхивание (раскрытие коробка), мягкий пульс (подсветка нового).</summary>
public class RetPop : MonoBehaviour
{
    private float popAt = -1f, shakeAt = -1f, pulseUntil = -1f;
    private Vector3 baseScale = Vector3.one;
    private bool captured, dirty;

    public static RetPop On(Component c)
    {
        var p = c.GetComponent<RetPop>();
        return p != null ? p : c.gameObject.AddComponent<RetPop>();
    }

    private void Capture() { if (!captured) { baseScale = transform.localScale; captured = true; } }

    /// <summary>Пружинка: 1.25 → 1.</summary>
    public void Pop() { Capture(); popAt = Time.unscaledTime; }
    /// <summary>Потряхивание 0.6 с, затем пружинка (открытие коробка).</summary>
    public void ShakeThenPop() { Capture(); shakeAt = Time.unscaledTime; }
    /// <summary>Мягкий пульс на несколько секунд (одноразовая подсветка нового).</summary>
    public void Pulse(float seconds) { Capture(); pulseUntil = Time.unscaledTime + seconds; }

    private void Update()
    {
        float t = Time.unscaledTime;
        float s = 1f, rot = 0f;
        if (shakeAt > 0f)
        {
            float k = t - shakeAt;
            if (k < 0.6f) rot = Mathf.Sin(k * 45f) * 10f * (1f - k / 0.6f * 0.5f);
            else { shakeAt = -1f; popAt = t; }
        }
        if (popAt > 0f)
        {
            float k = (t - popAt) / 0.45f;
            if (k >= 1f) popAt = -1f;
            else s = k < 0.4f ? Mathf.Lerp(1f, 1.25f, k / 0.4f) : Mathf.Lerp(1.25f, 1f, (k - 0.4f) / 0.6f);
        }
        bool pulsing = pulseUntil > t;
        if (pulsing) s *= 1f + 0.03f * (1f + Mathf.Sin(t * 6f));   // только больше исходного: текст не мельчает
        if (!captured) return;
        bool animating = shakeAt > 0f || popAt > 0f || pulsing;
        if (!animating && !dirty) return;   // покой: transform не трогаем, Canvas не перестраивается (R-06)
        dirty = animating;                  // последний кадр анимации вернёт исходные масштаб и поворот
        transform.localScale = baseScale * s;
        transform.localRotation = Quaternion.Euler(0f, 0f, rot);
    }
}
