using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Эффекты магазина: превью скина «дышит», пока магазин открыт; при покупке — вспышка,
/// разлёт искр и звук; при нехватке монет — тряска цены (L-08). Висит на ShopPanel,
/// вызывается из ShopManager. Все параметры — здесь, в инспекторе.
/// </summary>
public class ShopFX : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private RectTransform preview;
    [SerializeField] private RectTransform priceText;
    [SerializeField] private UIParticleField burst;
    [SerializeField] private Image flash;
    [SerializeField] private AudioClip purchaseSound;

    [Header("Превью")]
    public bool previewBreathing = true;
    public float breathingAmplitude = 0.025f;
    public float breathingPeriod = 3f;

    [Header("Покупка")]
    public bool purchaseEffect = true;
    public int burstCount = 40;
    public float flashDuration = 0.4f;
    [Range(0f, 1f)] public float flashPeak = 0.8f;

    [Header("Нехватка монет")]
    public bool shakeOnNotEnough = true;
    public float shakeDuration = 0.35f;
    public float shakeAmplitude = 18f;
    public float shakeFrequency = 45f;

    private float shakeTime = -1f, flashTime = -1f;
    private Vector2 priceBase;

    private void OnEnable()
    {
        if (preview != null && previewBreathing)
            UIScaleDriver.For(preview).SetPulse(true, breathingAmplitude, breathingPeriod);
    }

    private void OnDisable()
    {
        if (preview != null)
        {
            UIScaleDriver driver = preview.GetComponent<UIScaleDriver>();
            if (driver != null) driver.SetPulse(false, 0f, 1f);
        }
        StopShake();
        if (flash != null) SetFlashAlpha(0f);
        flashTime = -1f;
    }

    public void PlayPurchase()
    {
        if (!purchaseEffect) return;
        if (burst != null) burst.Burst(burstCount, Vector2.zero);
        if (flash != null) flashTime = 0f;
        if (purchaseSound != null && SoundManager.Instance != null) SoundManager.Instance.PlaySound(purchaseSound);
        Haptics.Success();
    }

    public void ShakePrice()
    {
        if (!shakeOnNotEnough || priceText == null) return;
        if (shakeTime < 0f) priceBase = priceText.anchoredPosition;
        shakeTime = 0f;
        Haptics.Error();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (shakeTime >= 0f)
        {
            shakeTime += dt;
            if (shakeTime >= shakeDuration) StopShake();
            else
            {
                float fade = 1f - shakeTime / shakeDuration;
                priceText.anchoredPosition = priceBase + new Vector2(Mathf.Sin(shakeTime * shakeFrequency) * shakeAmplitude * fade, 0f);
            }
        }

        if (flashTime >= 0f && flash != null)
        {
            flashTime += dt;
            float k = flashTime / flashDuration;
            SetFlashAlpha(k >= 1f ? 0f : flashPeak * (1f - k));
            if (k >= 1f) flashTime = -1f;
        }
    }

    private void StopShake()
    {
        if (shakeTime >= 0f && priceText != null) priceText.anchoredPosition = priceBase;
        shakeTime = -1f;
    }

    private void SetFlashAlpha(float a)
    {
        Color c = flash.color;
        c.a = a;
        flash.color = c;
    }
}
