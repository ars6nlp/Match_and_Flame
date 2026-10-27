using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>
/// Не даёт собрать Android с WebGL-реализацией Яндекс Игр, а WebGL — без неё.
///
/// PluginYG2 при включённом Auto Define Symbols раздаёт символ YandexGamesPlatform_yg
/// на ВСЕ платформы (PluginYourGames/Scripts/EditorScr/DefineSymbols.cs). Сам плагин на Android
/// уже не компилируется (PluginYourGames.asmdef: только Editor и WebGL), так что символ там
/// ничего не ломает, но означает, что настройки разъехались — останавливаем сборку, чтобы это
/// заметить. См. C-01 в GAME_AUDIT_REPORT.md.
/// </summary>
class PlatformDefinesBuildGuard : IPreprocessBuildWithReport
{
    private const string YandexPlatformDefine = "YandexGamesPlatform_yg";

    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        var target = NamedBuildTarget.FromBuildTargetGroup(report.summary.platformGroup);
        PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);
        bool hasYandex = Array.IndexOf(defines, YandexPlatformDefine) >= 0;

        if (report.summary.platform == BuildTarget.WebGL)
        {
            if (!hasYandex)
                throw new BuildFailedException(
                    $"WebGL-сборка без {YandexPlatformDefine}: SDK Яндекс Игр не будет работать. " +
                    "Добавьте символ в Player Settings → Web → Scripting Define Symbols.");
        }
        else if (hasYandex)
        {
            throw new BuildFailedException(
                $"{report.summary.platform}: в Scripting Define Symbols есть {YandexPlatformDefine}. " +
                "Уберите его в Player Settings этой платформы и выключите Tools → YG2 → Настройки → " +
                "Basic → Auto Define Symbols, иначе плагин вернёт символ.");
        }
    }
}
