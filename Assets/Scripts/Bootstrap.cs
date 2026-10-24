using UnityEngine;

/// <summary>
/// Глобальные настройки приложения до загрузки первой сцены.
/// </summary>
public static class Bootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
#if !UNITY_WEBGL
        // На Android vSyncCount из Quality Settings игнорируется и по умолчанию держится 30 FPS —
        // перетаскивание спичек и огонь заметно дёргаются. В WebGL частотой управляет браузер.
        Application.targetFrameRate = 60;
#endif

        // В релизе обычные Debug.Log не пишем: каждый — это строка и стектрейс, на Android дорого.
        // Предупреждения, ошибки и исключения остаются, чтобы разбирать проблемы игроков по logcat.
        if (!Debug.isDebugBuild)
            Debug.unityLogger.filterLogType = LogType.Warning;
    }
}
