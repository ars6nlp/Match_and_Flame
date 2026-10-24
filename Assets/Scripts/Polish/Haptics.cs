using UnityEngine;

/// <summary>
/// Короткая вибрация на Android (VibrationEffect, API 26+; на старых — vibrate(ms)).
/// Разрешение VIBRATE объявлено в Assets/Plugins/Android/MatchFlamePermissions.androidlib.
/// Переключатель «Вибрация» в настройках: PlayerPrefs "VibrationEnabled", по умолчанию включено.
/// Длительности и амплитуды — в VisualTheme («Вибрация»). На остальных платформах ничего не делает.
/// </summary>
public static class Haptics
{
    public const string PrefsKey = "VibrationEnabled";

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(PrefsKey, 1) == 1;
        set { PlayerPrefs.SetInt(PrefsKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static void Ignite() => Pulse(VisualTheme.Current.vibrateIgniteMs, VisualTheme.Current.vibrateIgniteAmplitude);
    public static void Success() => Pulse(VisualTheme.Current.vibrateSuccessMs, VisualTheme.Current.vibrateSuccessAmplitude);
    public static void Error() => Pulse(VisualTheme.Current.vibrateErrorMs, VisualTheme.Current.vibrateErrorAmplitude);

    public static void Pulse(int milliseconds, int amplitude)
    {
        if (milliseconds <= 0 || !Enabled || !VisualTheme.Current.vibration) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (vibrator == null)
            {
                var activity = UnityEngine.Android.AndroidApplication.currentActivity;
                vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    sdkInt = version.GetStatic<int>("SDK_INT");
            }
            if (vibrator == null) return;
            if (sdkInt >= 26)
            {
                using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", (long)milliseconds, Mathf.Clamp(amplitude, 1, 255)))
                    vibrator.Call("vibrate", effect);
            }
            else
            {
                vibrator.Call("vibrate", (long)milliseconds);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Haptics: " + e.Message);
        }
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject vibrator;
    private static int sdkInt;
#endif
}
