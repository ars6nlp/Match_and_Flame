using TMPro;
using UnityEngine;

/// <summary>Номер уровня в HUD («Уровень 5» / «Level 5»); игроку — с единицы. Обновляется только при смене уровня или языка.</summary>
[RequireComponent(typeof(TMP_Text))]
public class LevelNumberLabel : MonoBehaviour
{
    [SerializeField] private string formatRU = "Уровень {0}";
    [SerializeField] private string formatEN = "Level {0}";

    private TMP_Text label;
    private int shownIndex = -1;
    private string shownLang;

    private void Awake() => label = GetComponent<TMP_Text>();

    private void LateUpdate()
    {
        if (GameManager.Instance == null) return;
        int index = GameManager.Instance.currentLevelIndex;
        string lang = LanguageInit.CurrentLang;
        if (index == shownIndex && lang == shownLang) return;
        shownIndex = index;
        shownLang = lang;
        label.text = GameManager.Instance.IsDaily
            ? (lang == "ru" ? "Уровень дня" : "Daily level")
            : string.Format(lang == "ru" ? formatRU : formatEN, index + 1);
    }
}
