using UnityEngine;

/// <summary>
/// Часы уровня для механик (капля, вентилятор, Oscillator с useLevelClock): секунды с момента появления уровня
/// по игровому (масштабируемому) времени — на паузе (timeScale = 0) часы стоят. Всё, что от них зависит,
/// детерминировано: капли и циклы вентилятора каждый раз идут по одному расписанию от старта уровня.
/// GameManager вызывает Begin() при появлении уровня и MarkFirstIgnite() при первом поджоге.
/// </summary>
public static class LevelClock
{
    private static float startTime;
    private static float firstIgnite = -1f;

    /// <summary>Секунды с начала уровня.</summary>
    public static float Now => Time.time - startTime;

    /// <summary>Время первого поджога от начала уровня или −1, если ещё не поджигали.</summary>
    public static float FirstIgniteTime => firstIgnite;

    public static bool HasIgnited => firstIgnite >= 0f;

    public static void Begin()
    {
        startTime = Time.time;
        firstIgnite = -1f;
    }

    public static void MarkFirstIgnite()
    {
        if (firstIgnite < 0f) firstIgnite = Now;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { startTime = 0f; firstIgnite = -1f; }
}
