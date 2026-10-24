using UnityEngine;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    [Header("Настройки звука и музыки")]
    public Toggle musicToggle;
    public Toggle soundToggle;

    void Start()
    {
        // Загружаем настройки. 0 = выключено (Muted), 1 = включено (Unmuted).
        // По умолчанию 1 (включено).
        bool musicEnabled = GameAudio.MusicEnabled;
        bool soundEnabled = GameAudio.SoundEnabled;

        if (musicToggle != null) musicToggle.isOn = musicEnabled;
        if (soundToggle != null) soundToggle.isOn = soundEnabled;
    }

    // Вызывать из Toggle -> OnValueChanged
    public void OnMusicToggleChanged(bool isOn)
    {
        Debug.Log("Сохраняю музыку: " + isOn);
        // Источники музыки подписаны на GameAudio.Changed и обновятся сами
        GameAudio.MusicEnabled = isOn;
    }

    // Вызывать из Toggle -> OnValueChanged
    public void OnSoundToggleChanged(bool isOn)
    {
        Debug.Log("Сохраняю звук: " + isOn);
        GameAudio.SoundEnabled = isOn;

        // Обновляем звук у видео на заднем фоне (если оно есть)
        SmoothVideoLoader[] loaders = FindObjectsByType<SmoothVideoLoader>(FindObjectsSortMode.None);
        foreach(var loader in loaders)
        {
            loader.UpdateAudioState();
        }
        
        // Эффекты сами проверяют эту настройку перед тем, как прозвучать
    }

    // Вызывать из кнопок языков (например, кнопка RU передает "RU", кнопка EN передает "EN")
    public void SetLanguage(string langCode)
    {
        // Язык живёт в Yandex Games SDK, а не в PlayerPrefs — YG2 сам его сохранит
        LanguageInit.SetLanguage(langCode);

        // Заставляем все тексты на экране обновиться на новый язык
        LocalizeText[] allTexts = FindObjectsByType<LocalizeText>(FindObjectsSortMode.None);
        foreach(var text in allTexts)
        {
            text.UpdateText();
        }

        // И картинки с надписями — логотип, кнопки
        LocalizeImage[] allImages = FindObjectsByType<LocalizeImage>(FindObjectsSortMode.None);
        foreach(var img in allImages)
        {
            img.UpdateSprite();
        }
    }

    // Вызывать из кнопки "Сбросить прогресс" (по желанию)
    public void ResetProgress()
    {
        PlayerPrefs.DeleteAll();
        // Можно перезагрузить сцену после этого
    }
}
