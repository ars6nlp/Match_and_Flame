using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Не даёт запустить вторую загрузку сцены, пока первая не завершилась:
/// двойной тап по Restart / уровню / «Назад» раньше вызывал LoadScene дважды.
/// Флаг снимается, когда новая сцена загружена. Загрузка асинхронная (M-19).
/// </summary>
public static class SceneLoadGuard
{
    public static bool IsLoading { get; private set; }

    /// <summary>Идёт переход между сценами (загрузка или створки дверей) — пауза и «Назад» в это время не включаются.</summary>
    public static bool IsTransitioning => IsLoading || SceneTransition.Busy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        IsLoading = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        IsLoading = false;
        // Новая сцена всегда начинается с нормальным временем: пауза, включённая во время перехода,
        // раньше переезжала в следующую сцену (timeScale = 0 — уровень «застывал»).
        Time.timeScale = 1f;
    }

    // Сцена грузится в фоне, текущая продолжает рисоваться (двери, анимации) — вместо
    // замирания всего экрана на время синхронной загрузки (риск ANR на слабых телефонах).
    // С дверями (SceneTransition): створки закрываются, сцена включается только после полного закрытия, потом двери
    // открываются. Пока двери движутся, новую загрузку не начинаем (двойной тап, «Назад» во время открытия).
    /// <summary>Загрузка не началась (нет такой сцены) — снять флаг.</summary>
    public static void Cancel() => IsLoading = false;

    public static void Load(string sceneName)
    {
        if (IsLoading || SceneTransition.Busy) return;
        IsLoading = true;
        if (SceneTransition.Enabled) { SceneTransition.Load(sceneName, -1, null); return; }
        if (SceneManager.LoadSceneAsync(sceneName) == null) IsLoading = false;
    }

    public static void Load(int buildIndex)
    {
        if (IsLoading || SceneTransition.Busy) return;
        IsLoading = true;
        if (SceneTransition.Enabled) { SceneTransition.Load(null, buildIndex, null); return; }
        if (SceneManager.LoadSceneAsync(buildIndex) == null) IsLoading = false;
    }
}
