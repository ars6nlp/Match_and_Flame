using UnityEngine;

/// <summary>
/// Общий «двигатель» масштаба и прозрачности для UI-элемента. Несколько источников
/// (нажатие, появление, пульсация) задают свои множители, итог = базовый масштаб × их произведение.
/// Update работает только пока что-то анимируется, потом компонент сам выключается.
/// Время — unscaled, чтобы анимации шли и на паузе (Time.timeScale = 0).
/// </summary>
[DisallowMultipleComponent]
public class UIScaleDriver : MonoBehaviour
{
    private struct Channel
    {
        public float value, from, to, duration, elapsed, overshoot;
        public Ease.Type ease;
        public bool active;

        public void Start(float target, float time, Ease.Type type, float back, float delay = 0f)
        {
            from = value; to = target; duration = Mathf.Max(0.0001f, time); elapsed = -delay;
            ease = type; overshoot = back; active = true;
        }

        public bool Step(float dt)
        {
            if (!active) return false;
            elapsed += dt;
            if (elapsed < 0f) { value = from; return true; } // ещё ждём своей очереди (каскад)
            float k = Ease.Evaluate(ease, elapsed / duration, overshoot);
            value = Mathf.LerpUnclamped(from, to, k);
            if (elapsed >= duration) { value = to; active = false; }
            return true;
        }
    }

    private Vector3 baseScale;
    private bool initialized;
    private Channel press, appear, alpha;
    private CanvasGroup canvasGroup;

    private bool pulsing;
    private float pulseAmplitude, pulsePeriod, pulseTime;

    public static UIScaleDriver For(Component owner)
    {
        UIScaleDriver driver = owner.GetComponent<UIScaleDriver>();
        if (driver == null) driver = owner.gameObject.AddComponent<UIScaleDriver>();
        driver.Init();
        return driver;
    }

    private void Init()
    {
        if (initialized) return;
        initialized = true;
        baseScale = transform.localScale;
        press.value = appear.value = alpha.value = 1f;
        enabled = false;
    }

    public void AnimatePress(float target, float duration, Ease.Type ease, float overshoot = 1.70158f)
    {
        Init();
        press.Start(target, duration, ease, overshoot);
        enabled = true;
    }

    /// <summary>Появление: масштаб из startScale в 1, прозрачность CanvasGroup из 0 в 1.</summary>
    public void PlayAppear(float startScale, float duration, CanvasGroup group, float delay = 0f, Ease.Type ease = Ease.Type.OutCubic, float overshoot = 1.70158f)
    {
        Init();
        canvasGroup = group;
        appear.value = startScale;
        appear.Start(1f, duration, ease, overshoot, delay);
        if (canvasGroup != null)
        {
            alpha.value = 0f;
            alpha.Start(1f, duration, Ease.Type.OutCubic, 0f, delay);
        }
        Apply();
        enabled = true;
    }

    public void SetPulse(bool on, float amplitude, float period)
    {
        Init();
        pulsing = on;
        pulseAmplitude = amplitude;
        pulsePeriod = Mathf.Max(0.1f, period);
        if (on) enabled = true; else Apply();
    }

    /// <summary>Сбросить в покой (например, когда объект прячется посреди анимации).</summary>
    public void ResetToRest()
    {
        if (!initialized) return;
        press.active = appear.active = alpha.active = false;
        press.value = appear.value = 1f;
        if (canvasGroup != null) { alpha.value = 1f; canvasGroup.alpha = 1f; }
        pulseTime = 0f;
        Apply();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        bool any = press.Step(dt) | appear.Step(dt) | alpha.Step(dt);
        if (pulsing) { pulseTime += dt; any = true; }
        Apply();
        if (!any) enabled = false;
    }

    private void Apply()
    {
        float pulse = pulsing ? 1f + pulseAmplitude * 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * pulseTime / pulsePeriod)) : 1f;
        transform.localScale = baseScale * (press.value * appear.value * pulse);
        if (canvasGroup != null) canvasGroup.alpha = alpha.value;
    }

    private void OnDisable()
    {
        // Объект спрятали посреди анимации — следующее появление начнётся из покоя
        if (initialized && !gameObject.activeInHierarchy) ResetToRest();
    }
}
