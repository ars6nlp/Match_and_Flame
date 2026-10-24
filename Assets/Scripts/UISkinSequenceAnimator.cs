using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class UISkinSequenceAnimator : MonoBehaviour
{
    [System.Serializable]
    public class SkinData
    {
        public string skinName;      // Название скина (для удобства)
        public Sprite[] frames;      // Массив из 50-60 кадров для этого скина
    }

    [Header("Настройки скинов")]
    public SkinData[] skins;          // Массив всех ваших скинов (Оранжевый, Синий, Фиолетовый и т.д.)
    public float framesPerSecond = 30f; // Скорость анимации
    public bool loop = true;          // Зацикливать ли огонь на фоне

    [Header("Звук фона")]
    public AudioSource flameAudioSource; // Ссылка на AudioSource огонька (для сброса звука)

    private Image targetImage;
    private float timer;
    private int currentFrame;
    private int currentSkinIndex = 0;
    private bool isPlaying = true;

    private void Awake()
    {
        targetImage = GetComponent<Image>();
        if (flameAudioSource == null)
        {
            flameAudioSource = GetComponent<AudioSource>();
        }
    }

    // Фоновый трек меню подчиняется тумблеру «Музыка» (H-06)
    private void OnEnable()
    {
        GameAudio.Changed += ApplyMusicSetting;
        ApplyMusicSetting();
    }

    private void OnDisable()
    {
        GameAudio.Changed -= ApplyMusicSetting;
    }

    private void ApplyMusicSetting()
    {
        if (flameAudioSource != null) flameAudioSource.mute = !GameAudio.MusicEnabled;
    }

    /// <summary>Базовый скин, кадры которого сейчас играют (для MenuFlameLayout: пол под кадром).</summary>
    public int CurrentSkin => currentSkinIndex;

    public void SetSkin(int skinIndex)
    {
        if (skins == null || skins.Length == 0) return;

        // Оттеночный скин: кадры базового скина + цвет из VisualTheme
        Color tint = Color.white;
        VisualTheme.FlameTint flameTint = VisualTheme.Current.GetTint(skinIndex, skins.Length);
        if (flameTint != null)
        {
            skinIndex = flameTint.baseSkin;
            tint = flameTint.color;
        }
        if (skinIndex < 0 || skinIndex >= skins.Length)
            skinIndex = 0;
        targetImage.color = tint;

        currentSkinIndex = skinIndex;
        currentFrame = 0;
        timer = 0f;
        isPlaying = true;

        if (skins[currentSkinIndex].frames != null && skins[currentSkinIndex].frames.Length > 0)
        {
            targetImage.sprite = skins[currentSkinIndex].frames[0];
        }

        // Сбрасываем и перезапускаем звук огонька с самого начала!
        if (flameAudioSource != null)
        {
            flameAudioSource.Stop();
            flameAudioSource.time = 0f;
            flameAudioSource.Play();
        }
    }

    private void Update()
    {
        if (!isPlaying || skins == null || skins.Length == 0) return;

        SkinData currentSkin = skins[currentSkinIndex];
        if (currentSkin.frames == null || currentSkin.frames.Length == 0) return;

        timer += Time.deltaTime;
        float frameDuration = 1f / framesPerSecond;

        if (timer >= frameDuration)
        {
            timer -= frameDuration;
            currentFrame++;

            if (currentFrame >= currentSkin.frames.Length)
            {
                if (loop)
                {
                    currentFrame = 0;
                }
                else
                {
                    currentFrame = currentSkin.frames.Length - 1;
                    isPlaying = false;
                }
            }

            targetImage.sprite = currentSkin.frames[currentFrame];
        }
    }
}
