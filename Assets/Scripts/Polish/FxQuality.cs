using System;
using UnityEngine;

/// <summary>
/// Профиль качества эффектов: высокие / низкие. Ключ PlayerPrefs "FxQuality" (1 — высокие, 0 — низкие).
/// Без сохранённого выбора: на устройствах с памятью меньше 3000 МБ — низкие, иначе высокие.
/// </summary>
public static class FxQuality
{
    public const string Key = "FxQuality";
    public const int LowMemoryMb = 3000;

    private static int cached = -1;
    public static event Action Changed;

    public static bool High
    {
        get
        {
            if (cached < 0) cached = PlayerPrefs.GetInt(Key, SystemInfo.systemMemorySize < LowMemoryMb ? 0 : 1);
            return cached == 1;
        }
        set
        {
            cached = value ? 1 : 0;
            PlayerPrefs.SetInt(Key, cached);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { cached = -1; Changed = null; }
}
