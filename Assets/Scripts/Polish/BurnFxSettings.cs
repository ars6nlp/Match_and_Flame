using UnityEngine;

/// <summary>
/// Параметры «живого» горения: пламя, фронт огня, искры, дым, пепел, свет, лимиты. Каждый эффект отключается флагом.
/// Ассет: Assets/Resources/BurnFxSettings.asset. Профиль «низкие» (FxQuality) умножает число частиц и выключает свет.
/// </summary>
[CreateAssetMenu(fileName = "BurnFxSettings", menuName = "Match & Flame/Burn FX Settings")]
public class BurnFxSettings : ScriptableObject
{
    public const string ResourcePath = "BurnFxSettings";

    [Header("Пламя: живое мерцание")]
    public bool flameFlicker = true;
    [Tooltip("Размах колебания масштаба (доля)")]
    [Range(0f, 0.5f)] public float flickerScale = 0.12f;
    [Tooltip("Насколько пламя вытягивается вверх сильнее, чем в ширину")]
    [Range(0f, 1f)] public float flickerStretch = 0.6f;
    [Tooltip("Частота мерцания, Гц")]
    public float flickerSpeed = 7f;
    [Tooltip("Дрожание вбок, мировые единицы")]
    [Range(0f, 0.1f)] public float flickerOffset = 0.02f;
    [Tooltip("Насколько темнеет/светлеет цвет в мерцании")]
    [Range(0f, 0.4f)] public float flickerColor = 0.12f;
    [Tooltip("Покачивание, градусы, и его частота")]
    [Range(0f, 15f)] public float swayAngle = 4f;
    public float swaySpeed = 1.6f;

    [Header("Пламя: вспышка при поджоге и затухание")]
    public bool flameFlare = true;
    public float flareScale = 1.5f;
    [Range(0.05f, 0.4f)] public float flareDuration = 0.15f;
    public bool flameFadeOut = true;
    [Range(0.05f, 0.6f)] public float fadeOutDuration = 0.25f;
    [Tooltip("Пламя слабеет в последние доли горения (0 — не слабеет)")]
    [Range(0f, 0.5f)] public float endShrink = 0.25f;

    [Header("Фронт огня на спичке (тлеющий край)")]
    public bool emberFront = true;
    public Color emberColor = new Color(1f, 0.32f, 0.04f, 0.55f);
    public float emberSize = 0.45f;
    [Tooltip("Материал свечения; пусто — обычный спрайтовый (на светлом дереве аддитивный уходит в белое)")]
    public Material emberMaterial;
    [Tooltip("Головка вспыхивает при поджоге (размер свечения в начале)")]
    public float headGlowSize = 0.85f;
    public float emberPulseSpeed = 9f;

    [Header("Сгоревшая спичка: лёгкое оседание")]
    public bool burntSettle = true;
    [Range(0f, 0.15f)] public float settleAmount = 0.05f;

    [Header("Искры")]
    public bool sparks = true;
    [Tooltip("Сколько искр выбросить, когда эта спичка поджигает соседку")]
    public int sparksOnSpread = 6;

    [Header("Дым")]
    public bool smoke = true;
    [Tooltip("Во сколько раз гуще дым к концу горения")]
    public float smokeGrowth = 2.2f;
    [Tooltip("Плотность дыма относительно префаба Smoke (альфа)")]
    [Range(0.2f, 4f)] public float smokeOpacity = 2.5f;
    public bool smokeLinger = true;
    [Tooltip("Сколько секунд дымок держится после сгорания")]
    [Range(0f, 3f)] public float lingerSeconds = 1.5f;

    [Header("Пепел")]
    public bool ash = true;

    [Header("Свет (Light2D, без теней)")]
    public bool lights = true;
    [Tooltip("Сколько огней одновременно светят; остальные горят без света")]
    [Range(0, 16)] public int maxLights = 6;

    [Header("Лимиты (на сцену)")]
    [Tooltip("Сколько систем частиц горения (искры, дым) одновременно; при превышении гаснут самые старые")]
    public int maxBurnSystems = 24;
    [Tooltip("Слабые эффекты (дымок после сгорания, лишние искры) не запускаются, если активных систем больше этого")]
    public int weakEffectsLimit = 18;

    [Header("Профиль «низкие»")]
    [Range(0f, 1f)] public float lowParticleMultiplier = 0.5f;
    public int lowMaxBurnSystems = 10;

    // ---------------------------------------------------------------- Доступ

    private static BurnFxSettings current;

    /// <summary>Подмена на время (сцена BurnFxPreview крутит копию, не трогая ассет).</summary>
    public static BurnFxSettings Override;

    public static BurnFxSettings Current
    {
        get
        {
            if (Override != null) return Override;
            if (current == null)
            {
                current = Resources.Load<BurnFxSettings>(ResourcePath);
                if (current == null) current = CreateInstance<BurnFxSettings>();
            }
            return current;
        }
    }

    public int MaxLights => lights && FxQuality.High ? maxLights : 0;
    public int MaxBurnSystems => FxQuality.High ? maxBurnSystems : lowMaxBurnSystems;
    public float ParticleMultiplier => FxQuality.High ? 1f : lowParticleMultiplier;
    public bool Sparks => sparks && FxQuality.High;
    public bool SmokeLinger => smokeLinger && FxQuality.High;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { current = null; Override = null; }
}
