using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Кнопка «Настройки конфиденциальности» (согласие GDPR через UMP).
/// Google требует такую точку входа там, где форма согласия применима (ЕЭЗ, UK, Швейцария),
/// поэтому кнопка видна только в этом случае. В WebGL-сборке всегда скрыта.
/// Повесить на Button в панели настроек; кнопке нужен CanvasGroup (добавится сам).
/// </summary>
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(CanvasGroup))]
public class PrivacyOptionsButton : MonoBehaviour
{
    private CanvasGroup group;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    private void OnEnable()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (GoogleAdsManager.Instance != null)
            GoogleAdsManager.Instance.ConsentFlowFinished += Refresh;
#endif
        Refresh();
    }

    private void OnDisable()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (GoogleAdsManager.Instance != null)
            GoogleAdsManager.Instance.ConsentFlowFinished -= Refresh;
#endif
    }

    // Прячем через CanvasGroup, а не SetActive: иначе OnEnable больше не вызовется
    // и кнопка не появится, если статус согласия придёт позже.
    private void Refresh()
    {
        bool visible = false;
#if UNITY_ANDROID || UNITY_IOS
        visible = GoogleAdsManager.Instance != null && GoogleAdsManager.Instance.PrivacyOptionsRequired;
#endif
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    private void OnClick()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (GoogleAdsManager.Instance != null)
            GoogleAdsManager.Instance.ShowPrivacyOptions();
#endif
    }
}
