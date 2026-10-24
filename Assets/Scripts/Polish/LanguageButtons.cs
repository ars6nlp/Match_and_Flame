using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Подсвечивает кнопку текущего языка в настройках (RU / EN). Нажатия обрабатывает
/// SettingsManager.SetLanguage; здесь только вид. Цвета — из VisualTheme.
/// </summary>
public class LanguageButtons : MonoBehaviour
{
    [SerializeField] private Image ruBackground;
    [SerializeField] private Image enBackground;
    [SerializeField] private TMP_Text ruLabel;
    [SerializeField] private TMP_Text enLabel;

    private void OnEnable()
    {
        LanguageInit.LanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        LanguageInit.LanguageChanged -= Refresh;
    }

    private void Refresh()
    {
        VisualTheme theme = VisualTheme.Current;
        bool ru = LanguageInit.CurrentLang == "ru";
        if (ruBackground != null) ruBackground.color = ru ? theme.ember : theme.wood;
        if (enBackground != null) enBackground.color = ru ? theme.wood : theme.ember;
        if (ruLabel != null) ruLabel.color = ru ? Color.white : theme.cream;
        if (enLabel != null) enLabel.color = ru ? theme.cream : Color.white;
    }
}
