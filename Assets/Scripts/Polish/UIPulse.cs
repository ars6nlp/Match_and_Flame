using UnityEngine;

/// <summary>
/// Пульсация масштаба (кнопка «Играть», «дыхание» логотипа, текущий уровень).
/// Амплитуда 0 и период 0 — взять значения из VisualTheme («Пульсация»).
/// Совместима с UIButtonJuice: оба работают через общий UIScaleDriver.
/// </summary>
public class UIPulse : MonoBehaviour
{
    public float amplitude;
    public float period;

    private void OnEnable()
    {
        VisualTheme theme = VisualTheme.Current;
        UIScaleDriver.For(this).SetPulse(true,
            amplitude > 0f ? amplitude : theme.pulseAmplitude,
            period > 0f ? period : theme.pulsePeriod);
    }

    private void OnDisable()
    {
        UIScaleDriver driver = GetComponent<UIScaleDriver>();
        if (driver != null) driver.SetPulse(false, 0f, 1f);
    }
}
