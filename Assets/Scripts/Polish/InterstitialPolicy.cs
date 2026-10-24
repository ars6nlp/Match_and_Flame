using UnityEngine;

/// <summary>
/// Когда можно показать межстраничную рекламу (Android). Правила AdMob: не при запуске/выходе, не неожиданно,
/// не сразу после другого interstitial — поэтому:
///  • один общий счётчик «естественных пауз» (пройденный уровень, возврат в меню);
///  • показ только между уровнями (кнопка «Далее»), никогда в момент появления меню;
///  • не чаще раза в <see cref="CooldownSeconds"/> секунд реального времени;
///  • в первой сессии игрока — не раньше <see cref="FirstSessionMinLevels"/>-го пройденного уровня;
///  • после уровня дня (там окно награды «Сегодня») — не показываем;
///  • счётчик и кулдаун сбрасываются только при реальном показе.
/// Rewarded сюда не относится.
/// </summary>
public static class InterstitialPolicy
{
    public const float CooldownSeconds = 90f;
    public const int BreaksBetweenAds = 4;
    public const int FirstSessionMinLevels = 3;
    public const string SessionsKey = "Ads_Sessions";   // новый ключ: число запусков игры

    private static int breaks;
    private static int levelsThisSession;
    private static float lastShownAt = float.NegativeInfinity;
    private static bool firstSession;
    private static bool started;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void StartSession()
    {
        int sessions = PlayerPrefs.GetInt(SessionsKey, 0);
        Reset(sessions == 0);
        PlayerPrefs.SetInt(SessionsKey, sessions + 1);
        PlayerPrefs.Save();
    }

    /// <summary>Сброс состояния (запуск игры и тесты).</summary>
    public static void Reset(bool isFirstSession)
    {
        breaks = 0; levelsThisSession = 0; lastShownAt = float.NegativeInfinity;
        firstSession = isFirstSession; started = true;
    }

    /// <summary>Возврат в меню — пауза засчитывается, но реклама здесь не показывается.</summary>
    public static void MenuEntered() { if (started) breaks++; }

    /// <summary>Пройден уровень и нажато «Далее». true — сейчас можно показать interstitial.</summary>
    public static bool LevelCompleted(float now, bool daily)
    {
        breaks++;
        levelsThisSession++;
        if (daily) return false;
        if (firstSession && levelsThisSession < FirstSessionMinLevels) return false;
        if (now - lastShownAt < CooldownSeconds) return false;
        return breaks >= BreaksBetweenAds;
    }

    /// <summary>Реклама действительно показана.</summary>
    public static void Shown(float now) { breaks = 0; lastShownAt = now; }
}
