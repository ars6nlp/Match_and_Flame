using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("Звуки интерфейса")]
    [Tooltip("Закиньте сюда ваш звук клика по кнопке (длиной ~1 сек)")]
    public AudioClip clickSound;

    private AudioSource audioSource;

    void Awake()
    {
        // Паттерн Синглтон: убеждаемся, что SoundManager только один в игре
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Не уничтожать при загрузке новых сцен
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    // Этот метод мы будем вызывать для проигрывания звука
    public void PlayClick()
    {
        if (!GameAudio.SoundEnabled) return;
        if (clickSound != null && audioSource != null)
        {
            // PlayOneShot позволяет звукам накладываться друг на друга, если нажать быстро
            audioSource.PlayOneShot(clickSound);
        }
    }

    // НОВЫЙ МЕТОД: Позволяет проиграть ЛЮБОЙ переданный звук
    public void PlaySound(AudioClip clip)
    {
        if (!GameAudio.SoundEnabled) return;
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
