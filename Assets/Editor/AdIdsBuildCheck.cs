using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Перед релизной сборкой Android: предупредить, если в AdIds.cs остались тестовые ID Google (реклама не принесёт дохода)
/// или пуст TestDevices (релиз на своих телефонах будет запрашивать боевую рекламу — риск недействительного трафика).
/// Сборку не останавливает, чтобы проект собирался с чистого клона (BuildSetup/AdIds.Example.cs).
/// </summary>
class AdIdsBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    private const string GoogleTestPublisher = "ca-app-pub-3940256099942544";

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android) return;
        if ((report.summary.options & BuildOptions.Development) != 0) return;   // в Development Build и так тестовые ID
        if (AdIds.Banner.StartsWith(GoogleTestPublisher) || AdIds.Interstitial.StartsWith(GoogleTestPublisher) || AdIds.Rewarded.StartsWith(GoogleTestPublisher))
            Debug.LogWarning("[AdIds] Релизная сборка с ТЕСТОВЫМИ ID Google: реклама не принесёт дохода. Впишите боевые ID в Assets/Scripts/AdIds.cs.");
        if (AdIds.TestDevices == null || AdIds.TestDevices.Length == 0)
            Debug.LogWarning("[AdIds] AdIds.TestDevices пуст: релиз на своих телефонах будет запрашивать боевую рекламу. Добавьте хеши устройств из logcat (setTestDeviceIds).");
    }
}
