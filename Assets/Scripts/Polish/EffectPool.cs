using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Пул эффектов частиц: Spawn берёт свободный экземпляр (или создаёт новый),
/// по окончании частиц экземпляр сам возвращается в пул (OnParticleSystemStopped).
/// Во время уровня нет Instantiate/Destroy, кроме первого появления каждого эффекта.
/// </summary>
public class EffectPool
{
    private readonly Dictionary<ParticleSystem, Stack<PooledEffect>> free = new Dictionary<ParticleSystem, Stack<PooledEffect>>();
    private readonly Transform root;

    public EffectPool(Transform root)
    {
        this.root = root;
    }

    public void Prewarm(ParticleSystem prefab, int count)
    {
        if (prefab == null) return;
        Stack<PooledEffect> stack = StackFor(prefab);
        for (int i = 0; i < count; i++)
        {
            PooledEffect fx = Create(prefab);
            fx.gameObject.SetActive(false);
            stack.Push(fx);
        }
    }

    /// <summary>Запустить эффект в точке; follow — родитель, за которым эффект будет двигаться.</summary>
    public PooledEffect Spawn(ParticleSystem prefab, Vector3 position, Transform follow = null, uint seed = 0)
    {
        if (prefab == null) return null;
        Stack<PooledEffect> stack = StackFor(prefab);
        PooledEffect fx = stack.Count > 0 ? stack.Pop() : Create(prefab);
        fx.transform.SetParent(follow != null ? follow : root, false);
        fx.transform.position = position;
        fx.gameObject.SetActive(true);
        fx.InUse = true;
        fx.Released = false;
        fx.SetRate(1f);
        fx.SetAlpha(1f);
        if (seed != 0)
        {
            // Свой «почерк» у каждой спички: случайность частиц от её id (система из пула остановлена — пересеять можно)
            fx.System.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            fx.System.useAutoRandomSeed = false;
            fx.System.randomSeed = seed;
        }
        fx.System.Play(true);
        return fx;
    }

    /// <summary>Перестать испускать; уже летящие частицы догорят, затем экземпляр вернётся в пул.</summary>
    public void Release(PooledEffect fx)
    {
        if (fx == null || !fx.InUse || fx.Released) return;
        fx.Released = true;
        fx.transform.SetParent(root, true);
        fx.System.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    internal void Return(PooledEffect fx)
    {
        if (fx == null) return;
        fx.InUse = false;
        fx.gameObject.SetActive(false);
        fx.transform.SetParent(root, false);
        StackFor(fx.PrefabKey).Push(fx);
    }

    private Stack<PooledEffect> StackFor(ParticleSystem prefab)
    {
        if (!free.TryGetValue(prefab, out Stack<PooledEffect> stack))
        {
            stack = new Stack<PooledEffect>();
            free[prefab] = stack;
        }
        return stack;
    }

    private PooledEffect Create(ParticleSystem prefab)
    {
        ParticleSystem ps = Object.Instantiate(prefab, root);
        PooledEffect fx = ps.GetComponent<PooledEffect>();
        if (fx == null) fx = ps.gameObject.AddComponent<PooledEffect>();
        fx.Bind(this, prefab, ps);
        return fx;
    }
}
