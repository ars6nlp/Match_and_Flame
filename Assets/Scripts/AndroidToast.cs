using UnityEngine;

/// <summary>
/// Короткое системное уведомление Android (Toast) — без UI в сцене.
/// На остальных платформах только пишет в лог.
/// </summary>
public static class AndroidToast
{
    public static void Show(string text)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        var activity = UnityEngine.Android.AndroidApplication.currentActivity;
        UnityEngine.Android.AndroidApplication.InvokeOnUIThread(() =>
        {
            using (var toast = new AndroidJavaClass("android.widget.Toast"))
            using (var instance = toast.CallStatic<AndroidJavaObject>("makeText", activity, text, toast.GetStatic<int>("LENGTH_SHORT")))
            {
                instance.Call("show");
            }
        });
#else
        Debug.Log("[Toast] " + text);
#endif
    }

    /// <summary>Выбор строки по текущему языку игры.</summary>
    public static void Show(string textEN, string textRU)
    {
        Show(LanguageInit.CurrentLang == "ru" ? textRU : textEN);
    }
}
