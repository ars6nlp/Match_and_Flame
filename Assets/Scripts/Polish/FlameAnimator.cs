using UnityEngine;

/// <summary>
/// «Живое» пламя спички поверх кадровой анимации FireEffect: мерцание (шум по масштабу, смещению, цвету),
/// вспышка при поджоге, слабеет к концу и гаснет плавно, тянется вверх и покачивается, наклоняется по ветру.
/// Плюс тлеющий фронт на самой спичке (мягкое свечение там, где идёт огонь; в начале — вспышка головки).
/// Экземпляры живут в пуле GameJuice: Begin при выдаче, FadeOut при возврате. Без аллокаций в кадре.
/// Параметры — BurnFxSettings.
/// </summary>
public class FlameAnimator : MonoBehaviour
{
    private SpriteRenderer[] sprites;
    private FireLight[] lights;
    private Color[] baseColors;
    private Vector3 baseScale;
    private SpriteRenderer ember;
    private Transform emberT;

    private float seed, age, fadeTime = -1f, tilt, blowScale = 1f, progress;
    private Vector3 anchor, front;
    private System.Action<FlameAnimator> onFaded;

    /// <summary>Огонь виден и обновляется (после FadeOut — ещё доли секунды, пока гаснет).</summary>
    public bool Active => gameObject.activeSelf;

    private void Init()
    {
        if (sprites != null) return;
        sprites = GetComponentsInChildren<SpriteRenderer>(true);
        lights = GetComponentsInChildren<FireLight>(true);
        baseColors = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++) baseColors[i] = sprites[i].color;
        baseScale = transform.localScale;

        var go = new GameObject("EmberFront");
        go.transform.SetParent(transform.parent, false);
        emberT = go.transform;
        ember = go.AddComponent<SpriteRenderer>();
        ember.sprite = ProceduralTextures.Glow;
        int order = sprites.Length > 0 ? sprites[0].sortingOrder : 10;
        ember.sortingOrder = order - 1;
        go.SetActive(false);
    }

    /// <summary>Выдать огонь: tint — оттеночный скин (белый — без перекраски), id — для своего ритма мерцания.</summary>
    public void Begin(Vector3 position, Color tint, int id, System.Action<FlameAnimator> faded)
    {
        Init();
        for (int i = 0; i < sprites.Length; i++) sprites[i].color = baseColors[i] * tint;
        for (int i = 0; i < lights.Length; i++) lights[i].Tint(tint);
        seed = (id * 0.6180339f) % 1f * 100f;
        age = 0f;
        fadeTime = -1f;
        tilt = 0f;
        blowScale = 1f;
        progress = 0f;
        onFaded = faded;
        anchor = front = position;
        transform.SetPositionAndRotation(position, Quaternion.identity);
        transform.localScale = baseScale;
        gameObject.SetActive(true);
        if (emberT.parent != transform.parent) emberT.SetParent(transform.parent, false);
        emberT.gameObject.SetActive(BurnFxSettings.Current.emberFront);
        if (BurnFxSettings.Current.emberMaterial != null) ember.sharedMaterial = BurnFxSettings.Current.emberMaterial;
        emberT.position = position;
        Apply(0f);
    }

    /// <summary>Каждый кадр горения: где пламя, где фронт на спичке, доля сгоревшего, наклон по ветру, «задувание».</summary>
    public void Track(Vector3 flamePosition, Vector3 frontPosition, float burnProgress, float windTilt, float windScale)
    {
        anchor = flamePosition;
        front = frontPosition;
        progress = burnProgress;
        tilt = windTilt;
        blowScale = windScale;
    }

    /// <summary>Огонь гаснет (спичка догорела, потушена, уровень перезапущен): плавно, потом вернуть в пул.</summary>
    public void FadeOut(bool immediate = false)
    {
        if (!gameObject.activeSelf) return;
        BurnFxSettings s = BurnFxSettings.Current;
        if (immediate || !s.flameFadeOut) { Finish(); return; }
        if (fadeTime < 0f) fadeTime = 0f;
    }

    private void Finish()
    {
        fadeTime = -1f;
        if (emberT != null) emberT.gameObject.SetActive(false);
        gameObject.SetActive(false);
        onFaded?.Invoke(this);
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        age += dt;
        float fade = 1f;
        if (fadeTime >= 0f)
        {
            fadeTime += dt;
            fade = 1f - fadeTime / Mathf.Max(0.01f, BurnFxSettings.Current.fadeOutDuration);
            if (fade <= 0f) { Finish(); return; }
        }
        Apply(fade);
    }

    private void Apply(float fade)
    {
        BurnFxSettings s = BurnFxSettings.Current;
        float t = age * s.flickerSpeed;

        // Масштаб: вспышка в начале, слабеет к концу, мерцание (по вертикали сильнее), задувание ветром, затухание
        float k = 1f;
        if (s.flameFlare && age < s.flareDuration) k *= Mathf.Lerp(s.flareScale, 1f, Ease.Evaluate(Ease.Type.OutQuad, age / s.flareDuration));
        if (s.endShrink > 0f && progress > 0.7f) k *= 1f - s.endShrink * Mathf.InverseLerp(0.7f, 1f, progress);
        float nx = 0f, ny = 0f, nc = 0f;
        if (s.flameFlicker)
        {
            nx = Mathf.PerlinNoise(seed, t) * 2f - 1f;
            ny = Mathf.PerlinNoise(seed + 17.3f, t * 1.3f) * 2f - 1f;
            nc = Mathf.PerlinNoise(seed + 41.7f, t * 0.8f) * 2f - 1f;
        }
        float sx = k * (1f + s.flickerScale * (1f - s.flickerStretch) * nx);
        float sy = k * (1f + s.flickerScale * (1f + s.flickerStretch) * ny);
        float f = Mathf.Clamp01(fade);
        transform.localScale = new Vector3(baseScale.x * sx * blowScale * f, baseScale.y * sy * blowScale * Mathf.Lerp(0.4f, 1f, f), baseScale.z);

        // Положение и поворот: дрожание вбок, покачивание, наклон по ветру
        float sway = s.swayAngle * Mathf.Sin(age * s.swaySpeed * Mathf.PI * 2f + seed);
        transform.SetPositionAndRotation(anchor + new Vector3(s.flickerOffset * nx, 0f, 0f), Quaternion.Euler(0f, 0f, tilt + sway));

        // Цвет: лёгкое «дыхание» яркости
        float bright = 1f + s.flickerColor * nc;
        for (int i = 0; i < sprites.Length; i++)
        {
            Color c = baseColors[i];
            sprites[i].color = new Color(Mathf.Min(1f, c.r * bright), Mathf.Min(1f, c.g * bright), Mathf.Min(1f, c.b * bright), c.a * f);
        }

        for (int i = 0; i < lights.Length; i++) lights[i].Fade = f;

        // Тлеющий фронт на спичке: в начале — вспышка головки, дальше пульсирует по шуму
        if (emberT != null && emberT.gameObject.activeSelf)
        {
            float size = Mathf.Lerp(s.headGlowSize, s.emberSize, Mathf.Clamp01(age / Mathf.Max(0.05f, s.flareDuration * 2f)));
            size *= 1f + 0.18f * Mathf.Sin(age * s.emberPulseSpeed + seed);
            emberT.position = front;
            emberT.localScale = Vector3.one * size;
            Color e = s.emberColor;
            e.a *= f * (0.85f + 0.15f * nc);
            ember.color = e;
        }
    }

    private void OnDestroy()
    {
        if (emberT != null) Destroy(emberT.gameObject);
    }
}
