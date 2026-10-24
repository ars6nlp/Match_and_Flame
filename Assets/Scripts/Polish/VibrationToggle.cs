using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Кнопка «Вибрация: вкл/выкл» в настройках. Хранит выбор в PlayerPrefs "VibrationEnabled"
/// (Haptics.Enabled, по умолчанию включено). Подпись RU/EN обновляется при смене языка.
/// </summary>
[RequireComponent(typeof(Button))]
public class VibrationToggle : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
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
        Haptics.Enabled = !Haptics.Enabled;
        if (Haptics.Enabled) Haptics.Success(); // сразу почувствовать, что включилось
        Refresh();
    }

    private void Refresh()
    {
        bool on = Haptics.Enabled;
        bool ru = LanguageInit.CurrentLang == "ru";
        if (label != null)
            label.text = ru ? (on ? "Вибрация: вкл" : "Вибрация: выкл") : (on ? "Vibration: on" : "Vibration: off");
        VisualTheme theme = VisualTheme.Current;
        if (background != null) background.color = on ? theme.woodLight : theme.wood;
        if (label != null) label.color = on ? theme.cream : WithAlpha(theme.cream, 0.6f);
    }

    private static Color WithAlpha(Color c, float a) { c.a = a; return c; }
}
