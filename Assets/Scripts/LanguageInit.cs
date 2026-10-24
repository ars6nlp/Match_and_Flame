using System;
using UnityEngine;
#if UNITY_WEBGL
using YG;
#endif

/// <summary>
/// Мост между источником языка и текстами игры.
///
/// WebGL (Яндекс Игры): язык не хранится в PlayerPrefs — его отдаёт плагин в YG2.lang,
/// а тот читает ysdk.environment.i18n.lang (см. PluginYourGames/Modules/Localization/Plugins/Language.jslib).
/// Именно этого требует платформа (§2.14).
///
/// Остальные платформы (Android): язык системы при первом запуске, ручной выбор
/// из настроек сохраняется в PlayerPrefs. Плагин YG2 здесь не участвует —
/// его JS-функций на Android нет.
/// </summary>
public static class LanguageInit
{
    /// <summary>Язык оригинала. На него откатываемся, если перевода нет.</summary>
    public const string DefaultLang = "en";

    /// <summary>Язык сменился — текстам пора перерисоваться.</summary>
    public static event Action LanguageChanged;

#if UNITY_WEBGL
    private static bool subscribed;
#else
    private const string LangPrefsKey = "Lang";
    // Android-версия до перехода на YG2 хранила ручной выбор как "RU"/"EN" под этим ключом
    private const string LegacyLangPrefsKey = "Language";
#endif

    /// <summary>Текущий язык в нижнем регистре: "en", "ru"...</summary>
    public static string CurrentLang
    {
        get
        {
#if UNITY_WEBGL
            EnsureSubscribed();
            return Normalize(YG2.lang);
#else
            if (cachedLang == null) cachedLang = ResolveAndStore();
            return cachedLang;
#endif
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        // Сброс статики на случай, когда в редакторе отключён Domain Reload
        LanguageChanged = null;
#if !UNITY_WEBGL
        cachedLang = null;
#endif
#if UNITY_WEBGL
        subscribed = false;

        EnsureSubscribed();
#endif
    }

#if UNITY_WEBGL
    private static void EnsureSubscribed()
    {
        if (subscribed) return;
        subscribed = true;

        YG2.onSwitchLang += OnSwitchLang;
    }

    private static void OnSwitchLang(string lang)
    {
        LanguageChanged?.Invoke();
    }
#else
    private static string cachedLang;

    // Ключи, которые появляются только после игры в прежней версии (v4 и раньше): по ним узнаём обновившегося игрока
    private static readonly string[] V4ProgressKeys = { "Level_0_Stars", "GoldenMatchesCurrency", "CurrentLevel", "SkinUnlocked_1", "EquippedFireSkin" };

    // Один раз за запуск: решить язык и записать его в "Lang", дальше — из кеша (без PlayerPrefs в каждом кадре)
    private static string ResolveAndStore()
    {
        string lang = PlayerPrefs.HasKey(LangPrefsKey) ? PlayerPrefs.GetString(LangPrefsKey) : null;
        if (lang != null) return Normalize(lang);
        string legacy = PlayerPrefs.HasKey(LegacyLangPrefsKey) ? PlayerPrefs.GetString(LegacyLangPrefsKey) : null;
        bool v4Progress = false;
        foreach (string k in V4ProgressKeys) if (PlayerPrefs.HasKey(k)) { v4Progress = true; break; }
        string decided = Decide(null, legacy, v4Progress, Application.systemLanguage);
        PlayerPrefs.SetString(LangPrefsKey, decided);   // старый ключ "Language" не трогаем
        PlayerPrefs.Save();
        return decided;
    }
#endif

    /// <summary>
    /// Какой язык показать (чистая функция, проверяется тестом). null — ключа нет.
    /// 1) сохранённый "Lang"; 2) выбор из прежней версии под ключом "Language" ("RU"/"EN");
    /// 3) игрок прежней версии без явного выбора видел русский (там по умолчанию было "RU");
    /// 4) новый игрок — язык системы (русский для ru/be, иначе английский).
    /// </summary>
    public static string Decide(string lang, string legacyLanguage, bool hasV4Progress, SystemLanguage system)
    {
        if (!string.IsNullOrEmpty(lang)) return Normalize(lang);
        if (!string.IsNullOrEmpty(legacyLanguage)) return Normalize(legacyLanguage);
        if (hasV4Progress) return "ru";
        return system == SystemLanguage.Russian || system == SystemLanguage.Belarusian ? "ru" : DefaultLang;
    }

    /// <summary>
    /// Ручное переключение языка из настроек. Принимает и "RU", и "ru".
    /// WebGL: YG2 сам сохранит выбор и разошлёт onSwitchLang.
    /// </summary>
    public static void SetLanguage(string langCode)
    {
        if (string.IsNullOrEmpty(langCode)) return;

#if UNITY_WEBGL
        EnsureSubscribed();
        YG2.SwitchLanguage(Normalize(langCode));
#else
        cachedLang = Normalize(langCode);
        PlayerPrefs.SetString(LangPrefsKey, cachedLang);
        PlayerPrefs.Save();
        LanguageChanged?.Invoke();
#endif
    }

    private static string Normalize(string lang)
    {
        return string.IsNullOrEmpty(lang) ? DefaultLang : lang.ToLowerInvariant();
    }
}
