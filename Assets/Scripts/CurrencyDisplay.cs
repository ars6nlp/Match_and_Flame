using UnityEngine;
using TMPro;

public class CurrencyDisplay : MonoBehaviour
{
    private TextMeshProUGUI currencyText;

    // «Тиканье» при увеличении баланса (VisualTheme.coinTick)
    private int shown = -1, target;
    private float tickTime = -1f, tickFrom;

    void Awake()
    {
        currencyText = GetComponent<TextMeshProUGUI>();
    }

    void OnEnable()
    {
        shown = -1;
        UpdateDisplay();
        CurrencyManager.OnCurrencyChanged += UpdateDisplay;
    }

    void OnDisable()
    {
        CurrencyManager.OnCurrencyChanged -= UpdateDisplay;
    }

    private void UpdateDisplay()
    {
        if (currencyText == null) return;
        target = CurrencyManager.GetGoldenMatches();
        VisualTheme theme = VisualTheme.Current;

        if (shown >= 0 && target > shown && theme.coinTick && isActiveAndEnabled)
        {
            tickFrom = shown;
            tickTime = 0f;
            return;
        }
        SetShown(target);
    }

    private void Update()
    {
        if (tickTime < 0f) return;
        tickTime += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(tickTime / VisualTheme.Current.coinTickDuration);
        int value = Mathf.RoundToInt(Mathf.Lerp(tickFrom, target, k));
        if (value != shown)
        {
            SetShown(value);
            UIScaleDriver.For(this).PlayAppear(1.2f, 0.15f, null, 0f, Ease.Type.OutQuad);
        }
        if (k >= 1f) tickTime = -1f;
    }

    private void SetShown(int value)
    {
        shown = value;
        currencyText.text = value.ToString();
    }
}
