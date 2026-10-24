using TMPro;
using UnityEngine;

/// <summary>
/// Панель «Почти получилось» (H-03): сгорело меньше VisualTheme.oneStarShare спичек.
/// Звёзды и монеты не начисляются; кнопки «Заново», «К уровням» и «Подсказка».
/// </summary>
public class AlmostPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text subtitle;

    public void Show(int burnt, int total, int goldenCollected = 0)
    {
        gameObject.SetActive(true);
        if (subtitle == null) return;
        bool ru = LanguageInit.CurrentLang == "ru";
        subtitle.text = ru ? $"Сгорело {burnt} из {total}" : $"Burnt {burnt} of {total}";
        // Монета уже «прилетела» во время горения, но засчитывается только со звездой (K-14)
        if (goldenCollected > 0)
            subtitle.text += ru ? "\nМонеты за золото — при победе" : "\nGold coins count only on a win";
    }
}
