using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class UISequenceAnimator : MonoBehaviour
{
    [Header("Настройки анимации")]
    public Sprite[] frames;           // Массив ваших 40 кадров
    public float framesPerSecond = 30f; // Скорость (обычно 30 или 60)
    public bool playOnEnable = true;  // Запускать при появлении панели
    public bool loop = false;         // Зацикливать ли анимацию

    [Header("Кадры для остановки (ОТ 0 ДО 39)")]
    public int frameStop1Star = 10;  // На каком кадре остановиться, если 1 звезда (примерно ezgif10)
    public int frameStop2Star = 21;  // На каком кадре остановиться, если 2 звезды (ezgif21)
    
    [Header("Звук")]
    public AudioSource winSound;      // Звук, который проиграется при старте

    private Image targetImage;
    private float timer;
    private int currentFrame;
    private int targetStopFrame;
    private bool isPlaying = false;

    public System.Action onAnimationComplete;

    private AudioSource localAudio; // AudioSource с PlayOnAwake на этом же объекте (звук победы в GameScene)

    private void Awake()
    {
        targetImage = GetComponent<Image>();
        localAudio = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        // Теперь мы запускаем анимацию только через вызов Play(int stars) из GameManager

        // Звук победы подчиняется тумблеру «Звук» (H-06)
        bool mute = !GameAudio.SoundEnabled;
        if (localAudio != null) localAudio.mute = mute;
        if (winSound != null) winSound.mute = mute;
    }

    public void Play(int starsCount)
    {
        if (frames == null || frames.Length == 0) return;

        // Определяем, на каком кадре нужно остановиться
        if (starsCount == 1) targetStopFrame = frameStop1Star;
        else if (starsCount == 2) targetStopFrame = frameStop2Star;
        else targetStopFrame = frames.Length - 1; // 3 звезды - до конца

        // Защита от ошибок (чтобы индекс не вышел за пределы массива)
        if (targetStopFrame >= frames.Length) targetStopFrame = frames.Length - 1;

        Debug.Log($"[UISequenceAnimator] Звезд: {starsCount}, остановка на кадре №: {targetStopFrame}");

        currentFrame = 0;
        timer = 0f;
        isPlaying = true;
        
        // Показываем первый кадр сразу
        targetImage.sprite = frames[currentFrame];

        // Воспроизводим звук, если он назначен
        if (winSound != null)
        {
            winSound.Play();
        }
    }

    private void Update()
    {
        if (!isPlaying || frames == null || frames.Length == 0) return;

        timer += Time.deltaTime;
        float frameDuration = 1f / framesPerSecond;

        if (timer >= frameDuration)
        {
            timer -= frameDuration;
            currentFrame++;

            if (currentFrame >= targetStopFrame)
            {
                currentFrame = targetStopFrame;
                isPlaying = false; // Останавливаем анимацию на нужном кадре
                onAnimationComplete?.Invoke(); // Вызываем событие завершения анимации
            }

            targetImage.sprite = frames[currentFrame];
        }
    }
}
