using System;
using UnityEngine;

/// <summary>
/// Единая точка настроек звука. Ключи PlayerPrefs прежние,
/// чтобы у игроков не сбросились сохранённые настройки.
/// Источники звука читают флаги при включении и подписываются на <see cref="Changed"/>.
/// </summary>
public static class GameAudio
{
    private const string MusicKey = "MusicEnabled";
    private const string SoundKey = "SoundEnabled";

    /// <summary>Настройки звука изменились — источникам пора обновить mute.</summary>
    public static event Action Changed;

    public static bool MusicEnabled
    {
        get => PlayerPrefs.GetInt(MusicKey, 1) == 1;
        set => Set(MusicKey, value);
    }

    public static bool SoundEnabled
    {
        get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
        set => Set(SoundKey, value);
    }

    private static void Set(string key, bool value)
    {
        int stored = value ? 1 : 0;
        if (PlayerPrefs.GetInt(key, 1) == stored) return;

        PlayerPrefs.SetInt(key, stored);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    // Сброс статики на случай, когда в редакторе отключён Domain Reload
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Changed = null;
}
