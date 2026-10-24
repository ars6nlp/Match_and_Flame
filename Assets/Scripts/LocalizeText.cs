using UnityEngine;
using TMPro;

/// <summary>
/// Подставляет текст на языке платформы. Язык приходит из Yandex Games SDK
/// через <see cref="LanguageInit"/>.
///
/// Игра написана на английском, поэтому textEN — основной вариант,
/// а textRU подставляется, только если он заполнен.
/// </summary>
public class LocalizeText : MonoBehaviour
{
    [TextArea(2, 4)]
    [Tooltip("Основной текст — английский")]
    public string textEN;

    [TextArea(2, 4)]
    [Tooltip("Русский перевод. Оставьте пустым, чтобы показывать английский")]
    public string textRU;

    private TextMeshProUGUI uiText;
    private TextMeshPro worldText;

    void Awake()
    {
        uiText = GetComponent<TextMeshProUGUI>();
        worldText = GetComponent<TextMeshPro>();
    }

    void OnEnable()
    {
        LanguageInit.LanguageChanged += UpdateText;
        UpdateText();
    }

    void OnDisable()
    {
        LanguageInit.LanguageChanged -= UpdateText;
    }

    public void UpdateText()
    {
        string textToShow = textEN;

        if (LanguageInit.CurrentLang == "ru" && !string.IsNullOrEmpty(textRU))
        {
            textToShow = textRU;
        }

        // Если английский вариант забыли заполнить — лучше показать русский, чем пустоту
        if (string.IsNullOrEmpty(textToShow))
        {
            textToShow = textRU;
        }

        if (uiText != null) uiText.text = textToShow;
        if (worldText != null) worldText.text = textToShow;
    }
}
