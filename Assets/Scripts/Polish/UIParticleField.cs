using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Частицы прямо в UI (Screen Space Overlay рисуется поверх мира, обычный ParticleSystem там не виден).
/// Все частицы — один меш, один draw call. Массив фиксированного размера, аллокаций в Update нет.
/// Держите объект во вложенном Canvas, чтобы перестройка меша не задевала остальной UI.
///
/// Два режима: поток (emissionRate > 0, частицы поднимаются снизу с покачиванием —
/// угольки в меню) и вспышка Burst() (разлёт с гравитацией — конфетти, покупка).
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UIParticleField : MaskableGraphic
{
    [Header("Текстура частицы (пусто — мягкая точка из кода)")]
    [SerializeField] private Texture particleTexture;

    [Header("Поток (0 — только вспышки)")]
    public float emissionRate = 6f;
    [Tooltip("Где рождаются частицы потока, доля высоты от низа")]
    public Vector2 spawnHeight = new Vector2(0f, 0.25f);
    public Vector2 lifetime = new Vector2(2.5f, 5f);
    public Vector2 size = new Vector2(6f, 16f);
    [Tooltip("Скорость подъёма, единиц канваса в секунду")]
    public Vector2 riseSpeed = new Vector2(40f, 110f);
    [Tooltip("Амплитуда покачивания в стороны")]
    public float sway = 25f;

    [Header("Вспышка Burst()")]
    public Vector2 burstSpeed = new Vector2(250f, 650f);
    public Vector2 burstLifetime = new Vector2(0.6f, 1.2f);
    public Vector2 burstSize = new Vector2(10f, 26f);
    public float gravity = -900f;

    [Header("Цвет по времени жизни (умножается на Color компонента)")]
    public Gradient colorOverLife = DefaultGradient();

    [Range(8, 256)] public int maxParticles = 48;

    private struct Particle
    {
        public Vector2 pos, vel;
        public float age, life, size, seed, gravityScale;
        public bool alive;
    }

    private Particle[] particles;
    private int aliveCount;
    private float emitAccumulator;

    public override Texture mainTexture =>
        particleTexture != null ? particleTexture : ProceduralTextures.SoftDot.texture;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
        Allocate();
    }

    private void Allocate()
    {
        if (particles == null || particles.Length != maxParticles) particles = new Particle[maxParticles];
    }

    /// <summary>Разлёт count частиц из точки (локальные координаты этого RectTransform).</summary>
    public void Burst(int count, Vector2 localPosition)
    {
        Allocate();
        for (int i = 0; i < particles.Length && count > 0; i++)
        {
            if (particles[i].alive) continue;
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float speed = Random.Range(burstSpeed.x, burstSpeed.y);
            particles[i] = new Particle
            {
                pos = localPosition,
                vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f + 0.6f) * speed,
                life = Random.Range(burstLifetime.x, burstLifetime.y),
                size = Random.Range(burstSize.x, burstSize.y),
                seed = Random.value * 100f,
                gravityScale = 1f,
                alive = true,
            };
            aliveCount++;
            count--;
        }
        SetVerticesDirty();
    }

    private void Update()
    {
        Allocate();
        float dt = Time.unscaledDeltaTime;
        Rect r = rectTransform.rect;

        if (emissionRate > 0f)
        {
            emitAccumulator += emissionRate * dt;
            while (emitAccumulator >= 1f)
            {
                emitAccumulator -= 1f;
                SpawnAmbient(r);
            }
        }

        if (aliveCount == 0) return;

        for (int i = 0; i < particles.Length; i++)
        {
            if (!particles[i].alive) continue;
            ref Particle p = ref particles[i];
            p.age += dt;
            if (p.age >= p.life) { p.alive = false; aliveCount--; continue; }
            p.vel.y += gravity * p.gravityScale * dt;
            float wobble = p.gravityScale > 0f ? 0f : (Mathf.PerlinNoise(p.seed, p.age * 0.8f) - 0.5f) * 2f * sway;
            p.pos += (p.vel + new Vector2(wobble, 0f)) * dt;
        }
        SetVerticesDirty();
    }

    private void SpawnAmbient(Rect r)
    {
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i].alive) continue;
            particles[i] = new Particle
            {
                pos = new Vector2(Random.Range(r.xMin, r.xMax), r.yMin + r.height * Random.Range(spawnHeight.x, spawnHeight.y)),
                vel = new Vector2(0f, Random.Range(riseSpeed.x, riseSpeed.y)),
                life = Random.Range(lifetime.x, lifetime.y),
                size = Random.Range(size.x, size.y),
                seed = Random.value * 100f,
                gravityScale = 0f,
                alive = true,
            };
            aliveCount++;
            return;
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (particles == null) return;
        int v = 0;
        for (int i = 0; i < particles.Length; i++)
        {
            Particle p = particles[i];
            if (!p.alive) continue;
            Color c = colorOverLife.Evaluate(p.age / p.life) * color;
            float h = p.size * 0.5f;
            vh.AddVert(new Vector3(p.pos.x - h, p.pos.y - h), c, new Vector2(0f, 0f));
            vh.AddVert(new Vector3(p.pos.x - h, p.pos.y + h), c, new Vector2(0f, 1f));
            vh.AddVert(new Vector3(p.pos.x + h, p.pos.y + h), c, new Vector2(1f, 1f));
            vh.AddVert(new Vector3(p.pos.x + h, p.pos.y - h), c, new Vector2(1f, 0f));
            vh.AddTriangle(v, v + 1, v + 2);
            vh.AddTriangle(v + 2, v + 3, v);
            v += 4;
        }
    }

    private static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.85f, 0.45f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.5f), new GradientColorKey(new Color(0.75f, 0.2f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
        return g;
    }
}
