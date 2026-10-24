using UnityEngine;
using UnityEngine.UI;

/// <summary>Кнопка «Подсказка»: видна, только если для уровня есть решение и подсказки включены.</summary>
[RequireComponent(typeof(Button))]
public class HintButton : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            if (HintController.Instance != null) HintController.Instance.RequestHint();
        });
    }

    private void OnEnable()
    {
        bool available = HintController.Instance != null && HintController.Instance.Available;
        GetComponent<Button>().interactable = available;
        var group = GetComponent<CanvasGroup>();
        if (group != null) group.alpha = available ? 1f : 0f;
        if (group != null) group.blocksRaycasts = available;
    }
}
