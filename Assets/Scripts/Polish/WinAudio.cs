using UnityEngine;

/// <summary>
/// Звук итога уровня по числу звёзд (из существующих звуков, без новых ассетов):
///  • на каждую появляющуюся звезду — короткий «чим» (вырезка из win_star3, искристое место), высота растёт: 1-я, 2-я, 3-я;
///  • фанфара win_star3 целиком — только при 3★; при 2★ — тише и обрывается мягким затуханием после второй звезды; при 1★ — нет;
///  • «Почти получилось» — мягкий нейтральный стук вместо победы.
/// Звёзды и «чимы» синхронны с анимацией: GameJuice вызывает Star(i) в момент появления звезды.
/// Если подключить отдельные клипы winStar1/winStar2/winChime, они заменят собранные из win_star3.
/// Все звуки — только при включённом «Звуке»; вибрация — по числу звёзд. Источники звука созданы заранее (пул, без аллокаций).
/// </summary>
public class WinAudio : MonoBehaviour
{
    public static WinAudio Instance { get; private set; }

    [Header("Клипы (из Assets/Sounds)")]
    [SerializeField] private AudioClip fanfare;      // win_star3
    [SerializeField] private AudioClip almost;       // knocked-on-a-wooden-surface-once
    [Header("Отдельные клипы — заглушки, пока пустые (тогда звучание собирается из win_star3)")]
    [SerializeField] private AudioClip winStar1;
    [SerializeField] private AudioClip winStar2;
    [SerializeField] private AudioClip winChime;

    [Header("«Чим» звезды — вырезка из фанфары")]
    [SerializeField] private float chimeStart = 3.15f;
    [SerializeField] private float chimeLength = 0.45f;
    [SerializeField] private float[] chimePitch = { 1f, 1.12f, 1.26f };
    [Range(0f, 1f)] [SerializeField] private float chimeVolume = 0.9f;

    [Header("Фанфара по числу звёзд")]
    [Range(0f, 1f)] [SerializeField] private float fanfareVolume3 = 1f;
    [Range(0f, 1f)] [SerializeField] private float fanfareVolume2 = 0.5f;
    [Tooltip("2★: через сколько секунд после второй звезды фанфара затихает")]
    [SerializeField] private float fanfare2Tail = 0.5f;
    [SerializeField] private float fanfareFadeSeconds = 0.6f;
    [Range(0f, 1f)] [SerializeField] private float almostVolume = 0.55f;

    [Header("Вибрация по числу звёзд, мс (1★, 2★, 3★)")]
    [SerializeField] private int[] vibrateMs = { 25, 45, 80 };

    private AudioSource fanfareSource;
    private readonly AudioSource[] chimes = new AudioSource[3];
    private int stars;
    private float fadeAt = -1f, fadeFrom;
    private int startedFrame = -1;

    /// <summary>Что прозвучало (для проверки в Play Mode): «fanfare:3», «chime:1» и т.п.</summary>
    public static System.Action<string> Played;

    private void Awake()
    {
        Instance = this;
        fanfareSource = NewSource();
        for (int i = 0; i < chimes.Length; i++) chimes[i] = NewSource();
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private AudioSource NewSource()
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = false;
        s.spatialBlend = 0f;
        return s;
    }

    /// <summary>Итог уровня: запускается один раз (повторный вызов в том же кадре или при уже идущей победе — игнор).</summary>
    public void Begin(int starCount)
    {
        if (startedFrame >= 0) return; // двойной запуск (Restart/Next/повторная проверка победы)
        startedFrame = Time.frameCount;
        stars = Mathf.Clamp(starCount, 0, 3);
        if (stars > 0 && vibrateMs != null && vibrateMs.Length >= stars) Haptics.Pulse(vibrateMs[stars - 1], VisualTheme.Current.vibrateSuccessAmplitude);
        if (!GameAudio.SoundEnabled) return;

        if (stars == 0)
        {
            if (almost != null) { fanfareSource.pitch = 0.9f; fanfareSource.PlayOneShot(almost, almostVolume); }
            Played?.Invoke("almost");
            return;
        }
        AudioClip clip = stars == 1 ? winStar1 : stars == 2 ? winStar2 : fanfare;
        if (clip == null && stars == 2) clip = fanfare;
        if (clip == null) { Played?.Invoke("fanfare:none"); return; } // 1★ без своего клипа — только «чим» звезды
        fanfareSource.clip = clip;
        fanfareSource.pitch = 1f;
        fanfareSource.volume = stars == 3 || clip != fanfare ? fanfareVolume3 : fanfareVolume2;
        fanfareSource.time = 0f;
        fanfareSource.Play();
        fadeAt = -1f;
        Played?.Invoke($"fanfare:{stars}:{fanfareSource.volume:0.00}");
    }

    /// <summary>Звезда i (0, 1, 2) появилась — «чим» с растущей высотой.</summary>
    public void Star(int index)
    {
        if (startedFrame < 0 || index >= stars) return;
        if (GameAudio.SoundEnabled)
        {
            AudioSource s = chimes[Mathf.Clamp(index, 0, chimes.Length - 1)];
            float pitch = chimePitch != null && index < chimePitch.Length ? chimePitch[index] : 1f + 0.12f * index;
            AudioClip clip = winChime != null ? winChime : fanfare;
            if (clip != null)
            {
                s.clip = clip;
                s.pitch = pitch;
                s.volume = chimeVolume;
                s.time = winChime != null ? 0f : Mathf.Min(chimeStart, clip.length - 0.05f);
                double dsp = AudioSettings.dspTime + 0.01;
                s.PlayScheduled(dsp);
                s.SetScheduledEndTime(dsp + (winChime != null ? clip.length : chimeLength) / Mathf.Max(0.1f, pitch));
            }
            Played?.Invoke($"chime:{index + 1}:{pitch:0.00}");
        }
        // 2★ из общей фанфары: после второй звезды фанфара мягко затихает
        if (stars == 2 && index == 1 && fanfareSource.isPlaying && fanfareSource.clip == fanfare) fadeAt = Time.unscaledTime + fanfare2Tail;
    }

    private void Update()
    {
        if (fadeAt < 0f || Time.unscaledTime < fadeAt) return;
        if (fadeFrom <= 0f) fadeFrom = fanfareSource.volume;
        float k = (Time.unscaledTime - fadeAt) / Mathf.Max(0.05f, fanfareFadeSeconds);
        fanfareSource.volume = Mathf.Lerp(fadeFrom, 0f, k);
        if (k >= 1f) { fanfareSource.Stop(); fadeAt = -1f; fadeFrom = 0f; Played?.Invoke("fanfare:faded"); }
    }
}
