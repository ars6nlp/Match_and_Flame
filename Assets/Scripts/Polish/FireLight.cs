using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Свет огня спички: мерцание по шуму и лимит одновременно светящих огней
/// (BurnFxSettings.maxLights, на профиле «низкие» — без света). Огни сверх лимита горят без Light2D — это дёшево для GPU.
/// Висит на «Point Light» внутри FireEffect*.prefab. Яркость гаснет вместе с пламенем (Fade от FlameAnimator).
/// </summary>
[RequireComponent(typeof(Light2D))]
public class FireLight : MonoBehaviour
{
    private static int activeCount;

    private Light2D light2D;
    private float baseIntensity;
    private Color baseColor;
    private bool counted;
    private float seed;

    /// <summary>Множитель яркости (0…1): затухание пламени.</summary>
    public float Fade { get; set; } = 1f;

    private void Awake()
    {
        light2D = GetComponent<Light2D>();
        baseIntensity = light2D.intensity;
        baseColor = light2D.color;
    }

    /// <summary>Оттеночный скин: цвет света (белый — исходный).</summary>
    public void Tint(Color tint)
    {
        if (light2D == null) Awake();
        light2D.color = baseColor * tint;
    }

    private void OnEnable()
    {
        seed = Random.value * 100f;
        Fade = 1f;
        counted = activeCount < BurnFxSettings.Current.MaxLights;
        light2D.enabled = counted;
        if (counted) activeCount++;
    }

    private void OnDisable()
    {
        if (counted) activeCount--;
        counted = false;
    }

    private void Update()
    {
        if (!counted) return;
        VisualTheme theme = VisualTheme.Current;
        float k = 1f;
        if (theme.fireLightFlicker)
        {
            float n = Mathf.PerlinNoise(seed, Time.time * theme.fireLightFlickerSpeed);
            k = 1f - theme.fireLightFlickerAmount + 2f * theme.fireLightFlickerAmount * n;
        }
        light2D.intensity = baseIntensity * k * Fade;
    }

    /// <summary>Сколько огней сейчас светят.</summary>
    public static int ActiveCount => activeCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => activeCount = 0;
}
