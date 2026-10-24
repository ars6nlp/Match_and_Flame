using UnityEngine;

/// <summary>
/// Плавное появление панели (fade + масштаб) при каждом включении объекта.
/// Код, который показывает панель через SetActive(true), менять не нужно.
/// Параметры — в VisualTheme («Панели»). Работает на паузе.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class UIPanelAnimator : MonoBehaviour
{
    private CanvasGroup group;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        VisualTheme theme = VisualTheme.Current;
        if (!theme.panelAnimation)
        {
            group.alpha = 1f;
            return;
        }
        UIScaleDriver.For(this).PlayAppear(theme.panelStartScale, theme.panelInDuration, group);
    }
}
