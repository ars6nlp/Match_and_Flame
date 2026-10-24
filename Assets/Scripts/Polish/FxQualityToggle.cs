using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Кнопка «Эффекты: высокие/низкие» в настройках. Хранит выбор в PlayerPrefs "FxQuality" (FxQuality).
/// Подпись — через LocalizeText (RU/EN), тексты подставляются по состоянию.
/// </summary>
[RequireComponent(typeof(Button))]
public class FxQualityToggle : MonoBehaviour
{
    [SerializeField] private LocalizeText label;
    [SerializeField] private Image background;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Toggle);
    }

    private void OnEnable()
    {
        LanguageInit.LanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        LanguageInit.LanguageChanged -= Refresh;
    }

    private void Toggle()
    {
        FxQuality.High = !FxQuality.High;
        Refresh();
    }

    private void Refresh()
    {
        bool high = FxQuality.High;
        if (label != null)
        {
            label.textEN = high ? "Effects: high" : "Effects: low";
            label.textRU = high ? "Эффекты: высокие" : "Эффекты: низкие";
            label.UpdateText();
        }
        VisualTheme theme = VisualTheme.Current;
        if (background != null) background.color = high ? theme.woodLight : theme.wood;
    }
}
