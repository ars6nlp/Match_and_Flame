using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class RandomAudioPlayer : MonoBehaviour
{
    [Tooltip("Перетащите сюда все ваши звуки горения")]
    public AudioClip[] randomClips;

    // OnEnable, а не Start: огонь берётся из пула, Start при повторном использовании не вызывается
    void OnEnable()
    {
        AudioSource audioSource = GetComponent<AudioSource>();

        // Читаем настройку
        bool soundEnabled = GameAudio.SoundEnabled;

        // ДВОЙНАЯ БЛОКИРОВКА ЗВУКА:
        audioSource.mute = !soundEnabled; // Ставим на Mute
        audioSource.volume = soundEnabled ? 1f : 0f; // Скручиваем громкость в 0

        if (soundEnabled && randomClips != null && randomClips.Length > 0)
        {
            int randomIndex = Random.Range(0, randomClips.Length);
            audioSource.clip = randomClips[randomIndex];
            // «Мелодия» цепи: каждая следующая спичка звучит чуть выше (ChainAudio, VisualTheme.chainPitch)
            audioSource.pitch = ChainAudio.Pitch;
            audioSource.Play();
        }
    }
}
