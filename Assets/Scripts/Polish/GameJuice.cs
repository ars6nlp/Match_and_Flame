using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Все эффекты геймплея в одном месте (GameScene, объект «GameJuice»).
/// Игровой код вызывает статические методы; если объекта нет (другая сцена, эффекты убраны) — ничего не происходит.
/// Флаги и параметры каждого эффекта — в VisualTheme, раздел «Игра: …».
/// Частицы — из пула (EffectPool), короткие анимации — в фиксированных массивах без аллокаций.
/// </summary>
public class GameJuice : MonoBehaviour
{
    public static GameJuice Instance { get; private set; }

    [Header("Префабы частиц (Assets/Prefabs/FX)")]
    [SerializeField] private ParticleSystem igniteBurst;
    [SerializeField] private ParticleSystem burnSparks;
    [SerializeField] private ParticleSystem smoke;
    [SerializeField] private ParticleSystem ash;
    [SerializeField] private ParticleSystem goldenSparkle;
    [SerializeField] private ParticleSystem goldenBurst;

    [Header("Ссылки сцены")]
    [SerializeField] private CameraShake cameraShake;
    [SerializeField] private UIParticleField victoryConfetti;
    [SerializeField] private UISequenceAnimator winAnimation;
    [SerializeField] private RectTransform coinTarget;
    [SerializeField] private RectTransform fxOverlay;
    [SerializeField] private Sprite coinSprite;
    [SerializeField] private SpriteRenderer slotHighlight;
    [SerializeField] private AudioClip starSound;

    public struct BurnFx { public PooledEffect sparks, smoke; }

    private EffectPool pool;
    private AudioSource sfx;

    private const int MaxPunches = 16, MaxDarken = 64, MaxCoins = 4;
    private readonly Transform[] punchTarget = new Transform[MaxPunches];
    private readonly Vector3[] punchBase = new Vector3[MaxPunches];
    private readonly float[] punchTime = new float[MaxPunches];
    private readonly SpriteRenderer[] darkenTarget = new SpriteRenderer[MaxDarken];
    private readonly Color[] darkenFrom = new Color[MaxDarken];
    private readonly float[] darkenTime = new float[MaxDarken];
    private readonly Image[] coins = new Image[MaxCoins];
    private readonly Vector2[] coinFrom = new Vector2[MaxCoins];
    private readonly float[] coinTime = new float[MaxCoins];

    private readonly float[] starTimes = new float[3];
    private int starCount, starNext;
    private float starClock = -1f;

    // Пул огней спичек (FireEffect*): ключ — префаб скина
    private readonly System.Collections.Generic.Dictionary<GameObject, System.Collections.Generic.Stack<FlameAnimator>> firePool =
        new System.Collections.Generic.Dictionary<GameObject, System.Collections.Generic.Stack<FlameAnimator>>();
    private readonly System.Collections.Generic.Dictionary<FlameAnimator, GameObject> fireKey = new System.Collections.Generic.Dictionary<FlameAnimator, GameObject>();
    private Transform fireRoot;
    private System.Action<FlameAnimator> onFireFaded;

    // Активные петли горения (искры, дым) — для лимита: при превышении гасим самые старые
    private const int MaxLoops = 64, MaxLinger = 16;
    private readonly PooledEffect[] loops = new PooledEffect[MaxLoops];
    private readonly float[] loopTime = new float[MaxLoops];
    private readonly PooledEffect[] linger = new PooledEffect[MaxLinger];
    private readonly float[] lingerUntil = new float[MaxLinger];
    private readonly Vector3[] darkenScale = new Vector3[MaxDarken];

    private void Awake()
    {
        Instance = this;
        pool = new EffectPool(transform);
        fireRoot = new GameObject("FirePool").transform;
        onFireFaded = ReturnFire;
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        for (int i = 0; i < MaxCoins; i++) coinTime[i] = -1f;
        for (int i = 0; i < MaxPunches; i++) punchTime[i] = -1f;
        for (int i = 0; i < MaxDarken; i++) darkenTime[i] = -1f;
        if (slotHighlight != null) slotHighlight.enabled = false;

        // Немного экземпляров заранее, чтобы первая цепочка не создавала их на лету
        pool.Prewarm(igniteBurst, 3);
        pool.Prewarm(burnSparks, 4);
        pool.Prewarm(smoke, 4);
        pool.Prewarm(ash, 3);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------------------------------------------------------------- API для игрового кода

    public static void OnIgnite(MatchLogic match, bool byPlayer)
    {
        if (Instance == null || match.headPoint == null) return;
        if (VisualTheme.Current.igniteBurst) Instance.pool.Spawn(Instance.igniteBurst, match.headPoint.position);
        if (byPlayer) Haptics.Ignite();
    }

    // ---------------------------------------------------------------- Огонь спички (пул)

    /// <summary>Огонь из пула (без Instantiate во время уровня). Без GameJuice — обычный экземпляр, он удалится сам.</summary>
    public static FlameAnimator SpawnFire(GameObject prefab, Vector3 position, Color tint, int id)
    {
        if (Instance == null)
        {
            GameObject go = Instantiate(prefab, position, Quaternion.identity);
            FlameAnimator solo = go.GetComponent<FlameAnimator>();
            if (solo == null) solo = go.AddComponent<FlameAnimator>();
            solo.Begin(position, tint, id, f => Destroy(f.gameObject));
            return solo;
        }
        return Instance.TakeFire(prefab, position, tint, id);
    }

    private FlameAnimator TakeFire(GameObject prefab, Vector3 position, Color tint, int id)
    {
        if (!firePool.TryGetValue(prefab, out var stack)) firePool[prefab] = stack = new System.Collections.Generic.Stack<FlameAnimator>();
        FlameAnimator f;
        if (stack.Count > 0) f = stack.Pop();
        else
        {
            GameObject go = Instantiate(prefab, position, Quaternion.identity, fireRoot);
            f = go.GetComponent<FlameAnimator>();
            if (f == null) f = go.AddComponent<FlameAnimator>();
            fireKey[f] = prefab;
        }
        f.Begin(position, tint, id, onFireFaded);
        return f;
    }

    private void ReturnFire(FlameAnimator f)
    {
        if (f != null && fireKey.TryGetValue(f, out GameObject key)) firePool[key].Push(f);
    }

    /// <summary>Заранее создать огни (первая цепочка не создаёт их на лету).</summary>
    public static void PrewarmFires(GameObject prefab, int count)
    {
        if (Instance == null || prefab == null) return;
        var made = new FlameAnimator[count];
        for (int i = 0; i < count; i++) made[i] = Instance.TakeFire(prefab, new Vector3(0f, -100f, 0f), Color.white, i);
        for (int i = 0; i < count; i++) made[i].FadeOut(true);
    }

    public static BurnFx OnFireSpawned(Transform fire, int id = 0)
    {
        var fx = new BurnFx();
        if (Instance == null || fire == null) return fx;
        VisualTheme theme = VisualTheme.Current;
        BurnFxSettings s = BurnFxSettings.Current;
        uint seed = unchecked((uint)id * 2654435761u) | 1u;
        if (theme.burningSparks && s.Sparks) fx.sparks = Instance.SpawnLoop(Instance.burnSparks, fire, seed);
        if (theme.smoke && s.smoke) { fx.smoke = Instance.SpawnLoop(Instance.smoke, fire, seed + 7u); fx.smoke.SetAlpha(s.smokeOpacity); }
        if (theme.firePunch && !s.flameFlare) Instance.Punch(fire, theme.firePunchScale); // вспышку делает FlameAnimator
        return fx;
    }

    private PooledEffect SpawnLoop(ParticleSystem prefab, Transform follow, uint seed)
    {
        if (prefab == null) return null;
        BurnFxSettings s = BurnFxSettings.Current;
        // Лимит: при превышении гасим самую старую петлю
        int count = 0, oldest = -1;
        for (int i = 0; i < MaxLoops; i++)
        {
            if (loops[i] == null) continue;
            if (!loops[i].InUse || loops[i].Released) { loops[i] = null; continue; }
            count++;
            if (oldest < 0 || loopTime[i] < loopTime[oldest]) oldest = i;
        }
        if (count >= s.MaxBurnSystems && oldest >= 0) { pool.Release(loops[oldest]); loops[oldest] = null; }
        PooledEffect fx = pool.Spawn(prefab, follow.position, follow, seed);
        fx.SetRate(s.ParticleMultiplier);
        for (int i = 0; i < MaxLoops; i++)
            if (loops[i] == null) { loops[i] = fx; loopTime[i] = Time.time; break; }
        return fx;
    }

    private int ActiveLoops()
    {
        int n = 0;
        for (int i = 0; i < MaxLoops; i++) if (loops[i] != null && loops[i].InUse && !loops[i].Released) n++;
        return n;
    }

    /// <summary>Сколько систем частиц горения сейчас испускают (для сцены BurnFxPreview и замеров).</summary>
    public static int ActiveBurnSystems => Instance != null ? Instance.ActiveLoops() : 0;

    /// <summary>Каждый кадр горения: дым гуще к концу.</summary>
    public static void UpdateBurnFx(BurnFx fx, float progress)
    {
        if (Instance == null || fx.smoke == null || fx.smoke.Released) return;
        BurnFxSettings s = BurnFxSettings.Current;
        fx.smoke.SetRate(s.ParticleMultiplier * Mathf.Lerp(1f, s.smokeGrowth, Mathf.Clamp01(progress)));
    }

    /// <summary>Спичка поджигает соседку: всплеск искр.</summary>
    public static void OnSpread(BurnFx fx)
    {
        if (Instance == null || fx.sparks == null || fx.sparks.Released) return;
        BurnFxSettings s = BurnFxSettings.Current;
        int n = Mathf.RoundToInt(s.sparksOnSpread * s.ParticleMultiplier);
        if (n > 0) fx.sparks.System.Emit(n);
    }

    /// <summary>Отцепить петли от огня ДО его уничтожения: частицы догорят и вернутся в пул.</summary>
    public static void ReleaseBurnFx(BurnFx fx)
    {
        if (Instance == null) return;
        Instance.pool.Release(fx.sparks);
        Instance.pool.Release(fx.smoke);
    }

    public static void OnBurnout(MatchLogic match, SpriteRenderer sprite, Vector3 firePosition = default)
    {
        if (Instance == null) return;
        VisualTheme theme = VisualTheme.Current;
        BurnFxSettings s = BurnFxSettings.Current;
        if (theme.ashOnBurnout && s.ash) Instance.pool.Spawn(Instance.ash, Center(match));
        if (sprite != null) Instance.Darken(sprite);
        // Дымок после сгорания — «слабый» эффект: только если хватает лимита
        if (theme.smoke && s.SmokeLinger && s.lingerSeconds > 0f && Instance.smoke != null && Instance.ActiveLoops() < s.weakEffectsLimit)
        {
            Vector3 at = firePosition == default ? match.tailPoint.position : firePosition;
            for (int i = 0; i < MaxLinger; i++)
            {
                if (Instance.linger[i] != null) continue;
                PooledEffect fx = Instance.pool.Spawn(Instance.smoke, at);
                fx.SetRate(0.5f * s.ParticleMultiplier);
                fx.SetAlpha(s.smokeOpacity);
                Instance.linger[i] = fx;
                Instance.lingerUntil[i] = Time.time + s.lingerSeconds;
                break;
            }
        }
    }

    public static void OnGoldenCollected(MatchLogic match)
    {
        if (Instance == null) return;
        Vector3 center = Center(match);
        Instance.pool.Spawn(Instance.goldenBurst, center);
        if (VisualTheme.Current.coinFly) Instance.FlyCoin(center);
    }

    public static PooledEffect AttachGoldenSparkle(Transform match, Vector3 worldPosition)
    {
        if (Instance == null) return null;
        return Instance.pool.Spawn(Instance.goldenSparkle, worldPosition, match);
    }

    public static void Release(PooledEffect fx)
    {
        if (Instance != null) Instance.pool.Release(fx);
    }

    // ---------------------------------------------------------------- Механики: вода и ветер

    /// <summary>Разовый эффект из пула (брызги, пар). Префаб может быть пустым — тогда ничего.</summary>
    public static void SpawnFx(ParticleSystem prefab, Vector3 position)
    {
        if (Instance == null || prefab == null) return;
        Instance.pool.Spawn(prefab, position);
    }

    public static void PlayMechanicSfx(AudioClip clip, float volumeScale = 1f)
    {
        if (Instance == null || clip == null || !GameAudio.SoundEnabled) return;
        Instance.sfx.pitch = 1f;
        Instance.sfx.PlayOneShot(clip, MechanicsSettings.Current.sfxVolume * volumeScale);
    }

    /// <summary>Пламя потушено: пар (вода) или дымок (ветер), короткая вибрация «ошибка», без звука победы.</summary>
    public static void OnExtinguished(MatchLogic match, MatchLogic.ExtinguishCause cause, Vector3 flamePosition)
    {
        if (Instance == null) return;
        MechanicsSettings m = MechanicsSettings.Current;
        if (cause == MatchLogic.ExtinguishCause.Water)
        {
            Instance.pool.Spawn(m.steamFx, flamePosition);
            PlayMechanicSfx(m.extinguishHiss);
        }
        else
        {
            Instance.pool.Spawn(m.blowOutSmokeFx != null ? m.blowOutSmokeFx : Instance.smoke, flamePosition);
            PlayMechanicSfx(m.blowOutPuff);
        }
        if (m.vibrateOnExtinguish) Haptics.Error();
    }

    /// <summary>Конец уровня: тряска, конфетти, пружинки звёзд, вибрация.</summary>
    public static void OnLevelEnd(int stars, bool won)
    {
        if (Instance == null) return;
        VisualTheme theme = VisualTheme.Current;
        if (Instance.cameraShake != null) Instance.cameraShake.Shake(theme.shakeAmplitude, theme.shakeDuration);
        // Звук и вибрация итога — по числу звёзд (WinAudio); без него — как раньше
        if (WinAudio.Instance != null) WinAudio.Instance.Begin(won ? stars : 0);
        if (!won) { Haptics.Error(); return; }
        if (WinAudio.Instance == null) Haptics.Success();
        if (theme.victoryConfetti && Instance.victoryConfetti != null)
        {
            Rect r = Instance.victoryConfetti.rectTransform.rect;
            Instance.victoryConfetti.Burst(theme.confettiCount, new Vector2(0f, r.height * 0.15f));
        }
        if (theme.starPop || WinAudio.Instance != null) Instance.ScheduleStars(stars); // звёзды: пружинка и «чим» в момент появления
    }

    public static void ShowSlotHighlight(Vector3 position, float strength)
    {
        if (Instance == null || Instance.slotHighlight == null || !VisualTheme.Current.slotHighlight) return;
        SpriteRenderer sr = Instance.slotHighlight;
        sr.enabled = true;
        sr.transform.position = new Vector3(position.x, position.y, sr.transform.position.z);
        Color c = VisualTheme.Current.flame;
        c.a = Mathf.Clamp01(strength) * 0.6f;
        sr.color = c;
    }

    public static void HideSlotHighlight()
    {
        if (Instance != null && Instance.slotHighlight != null) Instance.slotHighlight.enabled = false;
    }

    public static Vector3 Center(MatchLogic match) =>
        match.headPoint != null && match.tailPoint != null
            ? (match.headPoint.position + match.tailPoint.position) * 0.5f
            : match.transform.position;

    // ---------------------------------------------------------------- Короткие анимации

    private void Punch(Transform target, float scale)
    {
        for (int i = 0; i < MaxPunches; i++)
        {
            if (punchTime[i] >= 0f) continue;
            punchTarget[i] = target;
            punchBase[i] = target.localScale;
            punchTime[i] = 0f;
            target.localScale = punchBase[i] * scale;
            return;
        }
    }

    private void Darken(SpriteRenderer sprite)
    {
        for (int i = 0; i < MaxDarken; i++)
        {
            if (darkenTime[i] >= 0f) continue;
            darkenTarget[i] = sprite;
            darkenFrom[i] = sprite.color;
            darkenScale[i] = sprite.transform.localScale;
            darkenTime[i] = 0f;
            return;
        }
    }

    private void FlyCoin(Vector3 worldPosition)
    {
        if (fxOverlay == null || coinTarget == null || Camera.main == null) return;
        for (int i = 0; i < MaxCoins; i++)
        {
            if (coinTime[i] >= 0f) continue;
            if (coins[i] == null)
            {
                var go = new GameObject("Coin", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(fxOverlay, false);
                coins[i] = go.GetComponent<Image>();
                coins[i].sprite = coinSprite;
                coins[i].raycastTarget = false;
                coins[i].rectTransform.sizeDelta = new Vector2(70f, 70f);
            }
            Vector2 screen = Camera.main.WorldToScreenPoint(worldPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(fxOverlay, screen, null, out coinFrom[i]);
            coins[i].color = VisualTheme.Current.gold;
            coins[i].rectTransform.anchoredPosition = coinFrom[i];
            coins[i].gameObject.SetActive(true);
            coinTime[i] = 0f;
            return;
        }
    }

    private void ScheduleStars(int stars)
    {
        if (winAnimation == null) return;
        float fps = Mathf.Max(1f, winAnimation.framesPerSecond);
        int last = winAnimation.frames != null ? winAnimation.frames.Length - 1 : winAnimation.frameStop2Star;
        starTimes[0] = winAnimation.frameStop1Star / fps;
        starTimes[1] = winAnimation.frameStop2Star / fps;
        starTimes[2] = last / fps;
        starCount = Mathf.Clamp(stars, 0, 3);
        starNext = 0;
        starClock = 0f;
    }

    private void Update()
    {
        VisualTheme theme = VisualTheme.Current;
        float dt = Time.deltaTime, udt = Time.unscaledDeltaTime;

        for (int i = 0; i < MaxPunches; i++)
        {
            if (punchTime[i] < 0f) continue;
            if (punchTarget[i] == null) { punchTime[i] = -1f; continue; }
            punchTime[i] += dt;
            float k = Ease.Evaluate(Ease.Type.OutQuad, punchTime[i] / theme.firePunchDuration);
            punchTarget[i].localScale = punchBase[i] * Mathf.Lerp(theme.firePunchScale, 1f, k);
            if (k >= 1f) punchTime[i] = -1f;
        }

        for (int i = 0; i < MaxDarken; i++)
        {
            if (darkenTime[i] < 0f) continue;
            if (darkenTarget[i] == null) { darkenTime[i] = -1f; continue; }
            darkenTime[i] += dt;
            float k = Mathf.Clamp01(darkenTime[i] / theme.burntDarkenDuration);
            darkenTarget[i].color = Color.Lerp(darkenFrom[i], darkenFrom[i] * theme.burntTint, k);
            BurnFxSettings bs = BurnFxSettings.Current;
            if (bs.burntSettle) // лёгкое оседание сгоревшей спички (только видимое: коллайдер уже выключен)
                darkenTarget[i].transform.localScale = new Vector3(darkenScale[i].x * (1f - 0.4f * bs.settleAmount * k), darkenScale[i].y * (1f - bs.settleAmount * k), darkenScale[i].z);
            if (k >= 1f) darkenTime[i] = -1f;
        }

        for (int i = 0; i < MaxLinger; i++)
        {
            if (linger[i] == null || Time.time < lingerUntil[i]) continue;
            pool.Release(linger[i]);
            linger[i] = null;
        }

        for (int i = 0; i < MaxCoins; i++)
        {
            if (coinTime[i] < 0f) continue;
            coinTime[i] += udt;
            float k = Mathf.Clamp01(coinTime[i] / theme.coinFlyDuration);
            Vector2 to = TargetLocal();
            Vector2 p = Vector2.LerpUnclamped(coinFrom[i], to, Ease.Evaluate(Ease.Type.InOutSine, k));
            p.y += Mathf.Sin(k * Mathf.PI) * 180f; // дуга
            coins[i].rectTransform.anchoredPosition = p;
            coins[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.6f, k);
            if (k >= 1f)
            {
                coinTime[i] = -1f;
                coins[i].gameObject.SetActive(false);
                UIScaleDriver.For(coinTarget).PlayAppear(1.35f, 0.3f, null, 0f, Ease.Type.OutBack, 2f);
            }
        }

        if (starClock >= 0f)
        {
            starClock += udt;
            while (starNext < starCount && starClock >= starTimes[starNext])
            {
                if (theme.starPop) UIScaleDriver.For(winAnimation).PlayAppear(1.12f, 0.25f, null, 0f, Ease.Type.OutBack, 2.2f);
                if (WinAudio.Instance != null) WinAudio.Instance.Star(starNext);
                else PlaySfx(starSound, 1f + 0.18f * starNext);
                starNext++;
            }
            if (starNext >= starCount) starClock = -1f;
        }
    }

    private Vector2 TargetLocal()
    {
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, coinTarget.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(fxOverlay, screen, null, out Vector2 local);
        return local;
    }

    private void PlaySfx(AudioClip clip, float pitch)
    {
        if (clip == null || !GameAudio.SoundEnabled) return;
        sfx.pitch = pitch;
        sfx.PlayOneShot(clip);
    }
}
