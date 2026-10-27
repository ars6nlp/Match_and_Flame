using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>
/// Не даёт препроцессору сборки PluginYG2 менять настройки Android.
///
/// YG.EditorScr.BuildModify.ProcessBuild (callbackOrder -1000) при ЛЮБОЙ сборке вызывает
/// ApplyProjectSettings() платформы Яндекса: ставит ManagedStrippingLevel.Minimal для выбранной
/// платформы, меняет runInBackground и автоматический Graphics API активной платформы.
/// Для Android запоминаем эти значения до него и возвращаем после. WebGL-сборки не трогаем —
/// там настройки плагина и нужны.
/// </summary>
static class AndroidSettingsShield
{
    internal static bool Saved;
    internal static ManagedStrippingLevel Stripping;
    internal static bool RunInBackground;
    internal static bool DefaultGraphicsApis;
}

class AndroidSettingsShieldBefore : IPreprocessBuildWithReport
{
    public int callbackOrder => -2000; // раньше ProcessBuild плагина (-1000)

    public void OnPreprocessBuild(BuildReport report)
    {
        AndroidSettingsShield.Saved = report.summary.platform == BuildTarget.Android;
        if (!AndroidSettingsShield.Saved) return;
        AndroidSettingsShield.Stripping = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Android);
        AndroidSettingsShield.RunInBackground = PlayerSettings.runInBackground;
        AndroidSettingsShield.DefaultGraphicsApis = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
    }
}

class AndroidSettingsShieldAfter : IPreprocessBuildWithReport
{
    public int callbackOrder => 0; // после ProcessBuild плагина

    public void OnPreprocessBuild(BuildReport report)
    {
        if (!AndroidSettingsShield.Saved || report.summary.platform != BuildTarget.Android) return;
        AndroidSettingsShield.Saved = false;
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, AndroidSettingsShield.Stripping);
        PlayerSettings.runInBackground = AndroidSettingsShield.RunInBackground;
        if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android) != AndroidSettingsShield.DefaultGraphicsApis)
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, AndroidSettingsShield.DefaultGraphicsApis);
    }
}
