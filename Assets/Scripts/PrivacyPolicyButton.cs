using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Кнопка «Политика конфиденциальности» в настройках: открывает <see cref="AppLinks.PrivacyPolicyUrl"/> в браузере.
/// Видна всегда (в отличие от «Настроек конфиденциальности» UMP, которые нужны только в ЕЭЗ/UK).
/// В WebGL (Яндекс Игры) скрыта: внешние ссылки из игры там запрещены, политика — на странице игры.
/// </summary>
[RequireComponent(typeof(Button))]
public class PrivacyPolicyButton : MonoBehaviour
{
    private void Awake()
    {
#if UNITY_WEBGL
        gameObject.SetActive(false);
#else
        GetComponent<Button>().onClick.AddListener(Open);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (AppLinks.PrivacyPolicyIsPlaceholder)
            Debug.LogWarning("PrivacyPolicyUrl — заглушка (" + AppLinks.PrivacyPolicyUrl + "). Замените в AppLinks.cs до публикации.");
#endif
#endif
    }

    public void Open() => Application.OpenURL(AppLinks.PrivacyPolicyUrl);
}
