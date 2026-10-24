using UnityEngine;

/// <summary>Экземпляр эффекта из EffectPool; возвращается в пул, когда частицы закончились.</summary>
[RequireComponent(typeof(ParticleSystem))]
public class PooledEffect : MonoBehaviour
{
    public ParticleSystem System { get; private set; }
    internal ParticleSystem PrefabKey { get; private set; }
    internal bool InUse { get; set; }
    /// <summary>Перестал испускать (Release), ждёт, пока догорят частицы.</summary>
    public bool Released { get; internal set; }
    private EffectPool pool;
    private float baseRate = -1f;
    private Color baseColor;
    private bool colorCached;

    /// <summary>Прозрачность относительно префаба (1 — как в префабе).</summary>
    public void SetAlpha(float multiplier)
    {
        ParticleSystem.MainModule m = System.main;
        if (!colorCached) { baseColor = m.startColor.color; colorCached = true; }
        Color c = baseColor;
        c.a = Mathf.Clamp01(c.a * multiplier);
        m.startColor = c;
    }

    /// <summary>Частота испускания относительно префаба (1 — как в префабе). Без аллокаций.</summary>
    public void SetRate(float multiplier)
    {
        ParticleSystem.EmissionModule e = System.emission;
        if (baseRate < 0f) baseRate = e.rateOverTimeMultiplier;
        e.rateOverTimeMultiplier = baseRate * multiplier;
    }

    internal void Bind(EffectPool owner, ParticleSystem prefab, ParticleSystem system)
    {
        pool = owner;
        PrefabKey = prefab;
        System = system;
        ParticleSystem.MainModule main = system.main;
        main.stopAction = ParticleSystemStopAction.Callback;
        main.playOnAwake = false;
    }

    private void OnParticleSystemStopped()
    {
        if (pool != null) pool.Return(this);
    }
}
