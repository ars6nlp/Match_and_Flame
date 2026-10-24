using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MatchLogic : MonoBehaviour
{
    [Header("Fire Settings")]
    public float burnTime = 1f;
    [Tooltip("Кривая движения огня. Если огонь обгоняет черноту, опустите середину линии чуть ниже.")]
    public AnimationCurve fireMovementCurve = AnimationCurve.Linear(0, 0, 1, 1);
    
    [Header("Fire Visual Offsets (360 Degrees)")]
    [Tooltip("Смещения огня для каждого угла (0, 30, 60... 330)")]
    public Vector2[] angleOffsets = new Vector2[12];
    
    public float flashRange = 1.5f;
    public float flashRadius = 0.1f;
    public float spreadRadius = 0.7f;
    public float sideSpreadDelay = 0.4f;
    public LayerMask matchLayer;

    [Header("Match Points (Start and End)")]
    public Transform headPoint;
    public Transform tailPoint;

    [Header("Currency")]
    [Tooltip("Если галочка стоит, эта спичка при сгорании даст +1 к валюте")]
    public bool isGolden = false;

    [Header("Перемещение")]
    [Tooltip("Если галочка стоит, игрок сможет перемещать эту спичку (перетаскивать)")]
    public bool isDraggable = false;
    private Vector3 dragOffset;
    private bool isBeingDragged = false;
    private Vector3 originalPos;

    [Header("Иммунитет")]
    [Tooltip("Эта спичка загорится только от другой обычной спички, но НЕ от стартовой спички игрока!")]
    public bool ignorePlayerMatch = false;

    [Header("Подсказки")]
    [Tooltip("Текст, который исчезнет, когда эта спичка загорится (необязательно)")]
    public GameObject hideOnBurnText;

    [Header("Graphics & Animation")]
    public Sprite burntSprite;
    public Animator animator;
    public GameObject firePrefab; // Дефолтный (оранжевый)
    public GameObject[] skinFirePrefabs; // Массив префабов: 0-Оранж, 1-Зеленый, 2-Синий, 3-Фиол

    public bool isBurning = false;
    public bool isBurnedOut = false;
    [Tooltip("Потушена водой или ветром: не горит, не поджигает соседей, не считается сгоревшей")]
    public bool isExtinguished = false;
    private SpriteRenderer spriteRenderer;

    public enum ExtinguishCause { Water, Wind }

    // Механики «капля» и «вентилятор»
    private float wetUntil = -1f;
    private Color dryColor = Color.white;
    private Coroutine burnRoutine, wetRoutine;
    private GameObject currentFire;
    private FlameAnimator flame;
    private float fireBlowScale = 1f;
    private Vector2 lastFlamePos;
    private float fireTilt;

    /// <summary>Мокрая спичка не загорается (капля воды), пока не высохнет.</summary>
    public bool IsWet => wetUntil >= 0f && LevelClock.Now < wetUntil;
    public bool IsPlayerMatch => isPlayerMatch || GetComponent<DraggableMatch>() != null;

    // Кеш для цикла горения: раньше каждый кадр создавался массив OverlapCircleAll и
    // вызывались GetComponent. Список свой у каждой спички: Ignite() соседа синхронно
    // выполняет первый шаг его корутины, и общий список испортился бы во время обхода.
    private readonly List<Collider2D> sideHits = new List<Collider2D>(8);
    private bool isPlayerMatch;
    private GameJuice.BurnFx burnFx;

    // Предел подшагов распространения огня за кадр (M-02)
    private const int MaxSpreadSubsteps = 8;

    void Start()
    {
        isPlayerMatch = GetComponent<DraggableMatch>() != null;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();
        if (matchLayer.value == 0) matchLayer = LayerMask.GetMask("Match");
        if (GameManager.Instance != null) GameManager.Instance.RegisterMatch();

        // Подкрашиваем золотую спичку
        if (isGolden && spriteRenderer != null)
        {
            spriteRenderer.color = new Color(1f, 0.85f, 0.2f); // Желто-золотой оттенок
            // Блеск до поджога (VisualTheme.goldenShimmer)
            if (VisualTheme.Current.goldenShimmer) gameObject.AddComponent<GoldenShimmer>().Init(this, spriteRenderer);
        }
    }

    public void Ignite()
    {
        if (isBurning || isBurnedOut || isExtinguished) return;
        if (IsWet) return; // мокрая спичка от соседей не загорается
        isBurning = true;

        if (GameManager.Instance != null) GameManager.Instance.MatchIgnited();
        GameJuice.OnIgnite(this, isPlayerMatch);

        if (animator != null)
        {
            animator.SetTrigger("Burn");
            animator.speed = 0f; 
        }
        else if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(1f, 0.5f, 0.5f);
        }

        // Прячем текст подсказки, если он был привязан к этой спичке
        if (hideOnBurnText != null)
        {
            Destroy(hideOnBurnText);
        }

        burnRoutine = StartCoroutine(BurnCoroutine());
    }

    /// <summary>Префаб огня выбранного в магазине скина и его оттенок (оттеночные скины — перекрашенный базовый).</summary>
    public GameObject FirePrefab(out Color fireTint)
    {
        int equippedSkin = PlayerPrefs.GetInt("EquippedFireSkin", 0);
        GameObject prefabToUse = firePrefab;
        fireTint = Color.white;
        if (skinFirePrefabs != null)
        {
            VisualTheme.FlameTint flameTint = VisualTheme.Current.GetTint(equippedSkin, skinFirePrefabs.Length);
            if (flameTint != null) { equippedSkin = flameTint.baseSkin; fireTint = flameTint.color; }
        }
        if (skinFirePrefabs != null && skinFirePrefabs.Length > 0 && equippedSkin >= 0 && equippedSkin < skinFirePrefabs.Length && skinFirePrefabs[equippedSkin] != null)
            prefabToUse = skinFirePrefabs[equippedSkin];
        return prefabToUse;
    }

    private IEnumerator BurnCoroutine()
    {
        int mask = matchLayer.value != 0 ? matchLayer.value : LayerMask.GetMask("Match");

        GameObject prefabToUse = FirePrefab(out Color fireTint);

        currentFire = null;
        flame = null;
        fireTilt = 0f;
        fireBlowScale = 1f;
        FireOffset fireOffset = null;
        if (prefabToUse != null)
        {
            // Огонь берётся из пула (GameJuice) и «оживляется» FlameAnimator; перекраска оттеночного скина — там же
            flame = GameJuice.SpawnFire(prefabToUse, headPoint.position, fireTint, GetInstanceID());
            currentFire = flame.gameObject;
            fireOffset = currentFire.GetComponent<FireOffset>();
            burnFx = GameJuice.OnFireSpawned(currentFire.transform, GetInstanceID());
        }

        var sideFilter = new ContactFilter2D { useTriggers = true };
        sideFilter.SetLayerMask(mask);

        float timer = 0;
        float prevMovement = 0f;
        float prevProgress = 0f;
        float windExposure = 0f;
        lastFlamePos = headPoint.position;
        while (timer < burnTime)
        {
            // Ветер (вентиляторы): без них wind пустой и расчёт совпадает с прежним
            float dt = Time.deltaTime;
            WindSample wind = default;
            if (Fan.ActiveCount > 0) wind = Fan.Sample(lastFlamePos);
            if (wind.extinguish)
            {
                windExposure += dt;
                if (windExposure >= wind.extinguishTime)
                {
                    StopBurning(ExtinguishCause.Wind);
                    yield break;
                }
            }
            else windExposure = 0f;

            timer += dt * (1f + wind.burnBoost);
            float progress = timer / burnTime;
            
            float movementProgress = fireMovementCurve.Evaluate(progress);
            Vector2 flamePos = Vector2.Lerp(headPoint.position, tailPoint.position, movementProgress);

            if (currentFire != null)
            {
                // Получаем текущий угол поворота спички (от 0 до 360)
                float angle = transform.eulerAngles.z;
                if (angle < 0) angle += 360f;
                
                // Находим ближайший индекс для шага в 30 градусов (0, 1, 2... 11)
                int index = Mathf.RoundToInt(angle / 30f) % 12;
                if (index < 0) index += 12;
                
                // Берем смещение из массива
                Vector2 currentOffset = Vector2.zero;
                if (angleOffsets != null && angleOffsets.Length == 12)
                {
                    currentOffset = angleOffsets[index];
                }
                
                if (fireOffset != null)
                {
                    currentOffset += fireOffset.offset;
                }
                
                if (wind.Any || fireTilt != 0f) BendFire(wind, windExposure);
                flame.Track(flamePos + currentOffset, flamePos, progress, fireTilt, fireBlowScale);
            }
            GameJuice.UpdateBurnFx(burnFx, progress);

            if (animator != null)
            {
                animator.Play("Burn", 0, progress);
            }

            // Если за кадр огонь продвинулся дальше половины spreadRadius (просадка FPS, GC,
            // подгрузка рекламы), проверяем и промежуточные точки — иначе соседняя спичка
            // могла проскочить между кадрами и цепочка рвалась. Шагаем вдоль самой спички,
            // а не между мировыми позициями: перетаскивание горящей спички не должно поджигать всё по пути.
            float burnLength = Vector2.Distance(headPoint.position, tailPoint.position);
            float advance = (movementProgress - prevMovement) * burnLength;
            int steps = Mathf.Clamp(Mathf.CeilToInt(advance / Mathf.Max(spreadRadius * 0.5f, 0.01f)), 1, MaxSpreadSubsteps);
            for (int s = 1; s <= steps; s++)
            {
                float k = (float)s / steps;
                float stepMovement = Mathf.Lerp(prevMovement, movementProgress, k);
                Vector2 stepPos = Vector2.Lerp(headPoint.position, tailPoint.position, stepMovement);
                SpreadSideways(stepPos + wind.push, Mathf.Lerp(prevProgress, progress, k), sideFilter, spreadRadius + wind.radiusBonus);
            }
            prevMovement = movementProgress;
            prevProgress = progress;
            lastFlamePos = flamePos;

            yield return null;
        }

        GameJuice.ReleaseBurnFx(burnFx); // отцепить искры и дым, пока огонь ещё существует
        Vector3 lastFire = currentFire != null ? currentFire.transform.position : (Vector3)lastFlamePos;
        if (flame != null) flame.FadeOut();
        flame = null;
        currentFire = null;
        burnRoutine = null;

        Collider2D myCol = GetComponent<Collider2D>();
        if (myCol != null) myCol.enabled = false;

        isBurning = false;
        isBurnedOut = true;

        if (spriteRenderer != null) spriteRenderer.color = Color.white;
        if (burntSprite != null && spriteRenderer != null) spriteRenderer.sprite = burntSprite;
        if (animator != null) animator.speed = 1f;
        GameJuice.OnBurnout(this, spriteRenderer, lastFire); // пепел, потемнение, дымок

        FlashForward();

        if (GameManager.Instance != null)
        {
            // Сначала засчитываем золото: MatchBurnt() может сразу завершить уровень
            // и начислить монеты, и золото последней спички тогда потеряется.
            if (isGolden)
            {
                GameManager.Instance.GoldenMatchBurnt();
                GameJuice.OnGoldenCollected(this);
            }
            GameManager.Instance.MatchBurnt();
        }
    }

    // Поджигает соседей сбоку от точки пламени flamePos (логика прежняя, вынесена из BurnCoroutine)
    // flamePos уже смещена ветром (wind.push), radius — spreadRadius с прибавкой ветра; без ветра — как раньше
    private void SpreadSideways(Vector2 flamePos, float progress, ContactFilter2D sideFilter, float radius)
    {
        Physics2D.OverlapCircle(flamePos, radius, sideFilter, sideHits);
        foreach (var col in sideHits)
        {
            if (col.gameObject != gameObject)
            {
                if (col.TryGetComponent(out MatchLogic sideMatch) && !sideMatch.isBurnedOut && !sideMatch.isBurning)
                {
                    // --- ПРОВЕРКА ИММУНИТЕТА ---
                    // Если ЭТА спичка - спичка игрока (у нее есть DraggableMatch),
                    // а соседняя спичка имеет галочку ignorePlayerMatch - игнорируем!
                    if (isPlayerMatch && sideMatch.ignorePlayerMatch)
                    {
                        continue;
                    }

                    Vector2 myCenter = (headPoint.position + tailPoint.position) / 2f;
                    Vector2 sideCenter = (sideMatch.headPoint.position + sideMatch.tailPoint.position) / 2f;
                    Vector2 diff = sideCenter - myCenter;

                    Vector2 myForward = (headPoint.position - tailPoint.position).normalized;
                    Vector2 sideForward = (sideMatch.headPoint.position - sideMatch.tailPoint.position).normalized;

                    float forwardShift = Mathf.Abs(Vector2.Dot(diff, myForward));
                    Vector2 myRight = new Vector2(-myForward.y, myForward.x);
                    float sideShift = Mathf.Abs(Vector2.Dot(diff, myRight));

                    if (sideShift < 0.3f) continue;

                    float alignment = Mathf.Abs(Vector2.Dot(myForward, sideForward));
                    bool isParallel = alignment > 0.8f;

                    if (isParallel)
                    {
                        Vector2 sideVector = (Vector2)sideMatch.headPoint.position - (Vector2)sideMatch.tailPoint.position;
                        float sideLength = sideVector.magnitude;
                        Vector2 sideDir = sideVector / sideLength;
                        Vector2 toFlame = flamePos - (Vector2)sideMatch.tailPoint.position;

                        float t = Vector2.Dot(toFlame, sideDir) / sideLength;
                        if (t < -0.1f || t > 1.1f) continue;
                    }

                    float matchLength = Vector2.Distance(headPoint.position, tailPoint.position);
                    if (isParallel && forwardShift < matchLength * 0.25f)
                    {
                        if (progress <= sideSpreadDelay) continue;
                    }

                    GameJuice.OnSpread(burnFx); // всплеск искр в момент поджога соседки
                    sideMatch.Ignite();
                }
            }
        }
    }

    private void FlashForward()
    {
        Vector2 direction = (tailPoint.position - headPoint.position).normalized;
        Debug.DrawRay(tailPoint.position, direction * flashRange, Color.red, 5f);

        int mask = matchLayer.value != 0 ? matchLayer.value : LayerMask.GetMask("Match");

        // Ветер вдоль спички добавляет дальности вспышке, встречный — отнимает
        float range = flashRange;
        if (Fan.ActiveCount > 0) range = Mathf.Max(0.02f, flashRange + Vector2.Dot(Fan.Sample(tailPoint.position).flash, direction));

        RaycastHit2D hit = Physics2D.CircleCast(
            tailPoint.position,
            flashRadius,
            direction,
            range,
            mask
        );

        if (hit.collider != null && hit.collider.gameObject != gameObject)
        {
            MatchLogic nextMatch = hit.collider.GetComponent<MatchLogic>();
            if (nextMatch != null && !nextMatch.isBurnedOut && !nextMatch.isBurning)
            {
                // --- ПРОВЕРКА ИММУНИТЕТА ---
                if (isPlayerMatch && nextMatch.ignorePlayerMatch)
                {
                    return;
                }

                nextMatch.Ignite();
            }
        }
    }

    // ---------------------------------------------------------------- Механики: вода и ветер

    /// <summary>Потушить горящую спичку (капля или сильный ветер). Несгоревшую не трогает.</summary>
    public void Extinguish(ExtinguishCause cause)
    {
        if (!isBurning) return;
        if (burnRoutine != null) StopCoroutine(burnRoutine);
        StopBurning(cause);
    }

    private void StopBurning(ExtinguishCause cause)
    {
        burnRoutine = null;
        Vector3 at = currentFire != null ? currentFire.transform.position : (Vector3)lastFlamePos;
        GameJuice.ReleaseBurnFx(burnFx);
        if (flame != null) flame.FadeOut();
        flame = null;
        currentFire = null;
        isBurning = false;
        isExtinguished = true; // спичка застывает недогоревшей: анимация остановлена на текущем кадре
        if (spriteRenderer != null) spriteRenderer.color = MechanicsSettings.Current.extinguishedTint;
        GameJuice.OnExtinguished(this, cause, at);
        if (GameManager.Instance != null) GameManager.Instance.MatchExtinguished();
    }

    /// <summary>Намочить: duration секунд не загорается. Горящую — тушит.</summary>
    public void Wet(float duration)
    {
        if (isBurning) { Extinguish(ExtinguishCause.Water); return; }
        if (isBurnedOut || isExtinguished) return;
        bool wasWet = IsWet;
        wetUntil = LevelClock.Now + duration;
        if (!wasWet && spriteRenderer != null)
        {
            dryColor = spriteRenderer.color;
            if (wetRoutine != null) StopCoroutine(wetRoutine);
            wetRoutine = StartCoroutine(WetVisual());
        }
    }

    private IEnumerator WetVisual()
    {
        MechanicsSettings m = MechanicsSettings.Current;
        Color wet = dryColor * m.wetTint;
        while (IsWet && !isBurning && !isExtinguished)
        {
            float left = wetUntil - LevelClock.Now;
            float k = Mathf.Clamp01(left / Mathf.Max(0.01f, m.dryFadeTime));
            if (spriteRenderer != null) spriteRenderer.color = Color.Lerp(dryColor, wet, k);
            yield return null;
        }
        if (!isExtinguished && !isBurning && spriteRenderer != null) spriteRenderer.color = dryColor;
        wetRoutine = null;
    }

    // Пламя наклоняется по ветру; перед тем как ветер его задует — мигает и уменьшается
    private void BendFire(WindSample wind, float exposure)
    {
        MechanicsSettings m = MechanicsSettings.Current;
        float target = Mathf.Clamp(-wind.wind.x * m.fireTiltPerStrength, -m.fireMaxTilt, m.fireMaxTilt);
        fireTilt = Mathf.MoveTowards(fireTilt, target, 240f * Time.deltaTime);
        float scale = 1f;
        if (wind.extinguish && wind.extinguishTime > 0f)
        {
            float k = Mathf.Clamp01(exposure / wind.extinguishTime);
            scale = (1f - 0.6f * k) * (0.8f + 0.2f * Mathf.Sin(LevelClock.Now * m.blowOutFlickerHz * Mathf.PI * 2f));
        }
        fireBlowScale = scale;
    }

    private void OnDestroy()
    {
        // Корутины гибнут вместе с объектом; огонь — из пула GameJuice, возвращаем его сразу
        if (flame != null) flame.FadeOut(true);
    }

    private void OnMouseDown()
    {
        if (Time.timeScale == 0f) return; // Игнорируем нажатия на паузе
        
        // Перетаскивать можно только ПОСЛЕ того, как игрок зажег главную спичку (игра началась)
        if (GameManager.Instance != null && !GameManager.Instance.isGameStarted) return;
        // ...и только пока уровень не закончился: под панелью победы спички не двигаем
        if (GameManager.Instance != null && GameManager.Instance.isLevelEnded) return;

        // Теперь разрешаем таскать горящие спички (убрали проверку isBurning)
        if (!isDraggable || isBurnedOut) return;
        if (FieldInput.PointerOverUI()) return; // касание по кнопке не двигает спичку (M-06)

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        dragOffset = transform.position - mousePos;
        dragOffset.z = 0;
        originalPos = transform.position; // Сохраняем начальную позицию
        isBeingDragged = true;
    }

    private void OnMouseDrag()
    {
        if (Time.timeScale == 0f) return;
        
        if (GameManager.Instance != null && !GameManager.Instance.isGameStarted) return;
        if (GameManager.Instance != null && GameManager.Instance.isLevelEnded) return;

        // Теперь разрешаем таскать горящие спички
        if (!isDraggable || !isBeingDragged || isBurnedOut) return;

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        Vector3 target = mousePos + dragOffset;
        // Не даём утащить спичку за край экрана (M-07): держим в поле её центр
        Vector3 center = target + (GameJuice.Center(this) - transform.position);
        transform.position = target + FieldInput.PushInside(center, Camera.main);
    }

    private void OnMouseUp()
    {
        if (!isDraggable || !isBeingDragged) return;
        isBeingDragged = false;
        
        // ПРОВЕРКА НА ПЕРЕСЕЧЕНИЕ СО ВСЕМИ ДРУГИМИ СПИЧКАМИ
        MatchLogic[] allMatches = FindObjectsByType<MatchLogic>(FindObjectsSortMode.None);
        foreach (var match in allMatches)
        {
            if (match != null && match != this && match.gameObject.activeInHierarchy)
            {
                if (CheckOverlap(this, match))
                {
                    // Накладывается на другую спичку - возвращаем на исходную позицию!
                    transform.position = originalPos;
                    return;
                }
            }
        }
    }

    private bool CheckOverlap(MatchLogic logic1, MatchLogic logic2)
    {
        if (logic1 == null || logic1.headPoint == null || logic1.tailPoint == null ||
            logic2 == null || logic2.headPoint == null || logic2.tailPoint == null)
            return false;

        float minAllowedDistance = 0.25f; // Насколько близко можно класть параллельные спички
        
        Vector2 a1 = logic1.headPoint.position;
        Vector2 a2 = logic1.tailPoint.position;
        Vector2 b1 = logic2.headPoint.position;
        Vector2 b2 = logic2.tailPoint.position;

        // Быстрая проверка на пересечение самих отрезков (крест-накрест). 
        // Если они касаются только кончиками - это разрешено!
        if (SegmentsIntersect(a1, a2, b1, b2)) return true;

        // Проверяем ТОЛЬКО середину первой спички (без кончиков), чтобы кончиками можно было касаться!
        Vector2[] pointsA = { Vector2.Lerp(a1, a2, 0.25f), Vector2.Lerp(a1, a2, 0.5f), Vector2.Lerp(a1, a2, 0.75f) };
        foreach (Vector2 p in pointsA)
        {
            if (DistancePointToSegment(p, b1, b2) < minAllowedDistance) return true;
        }

        // Проверяем ТОЛЬКО середину второй спички
        Vector2[] pointsB = { Vector2.Lerp(b1, b2, 0.25f), Vector2.Lerp(b1, b2, 0.5f), Vector2.Lerp(b1, b2, 0.75f) };
        foreach (Vector2 p in pointsB)
        {
            if (DistancePointToSegment(p, a1, a2) < minAllowedDistance) return true;
        }

        return false;
    }

    private bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float denominator = ((b.x - a.x) * (d.y - c.y)) - ((b.y - a.y) * (d.x - c.x));
        if (denominator == 0) return false;

        float numerator1 = ((a.y - c.y) * (d.x - c.x)) - ((a.x - c.x) * (d.y - c.y));
        float numerator2 = ((a.y - c.y) * (b.x - a.x)) - ((a.x - c.x) * (b.y - a.y));

        float r = numerator1 / denominator;
        float s = numerator2 / denominator;

        // Используем 0.05 и 0.95, чтобы разрешить пересечение на самых кончиках (соединение спичек)
        return (r > 0.05f && r < 0.95f) && (s > 0.05f && s < 0.95f);
    }

    private float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSq = ab.sqrMagnitude;
        if (lengthSq == 0) return Vector2.Distance(point, a);

        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq);
        Vector2 projection = a + t * ab;
        return Vector2.Distance(point, projection);
    }

    // Рисуется всегда, чтобы было удобно расставлять спички!
    private void OnDrawGizmos()
    {
        if (tailPoint != null && headPoint != null)
        {
            // Зеленая линия показывает направление горения спички
            Gizmos.color = Color.green;
            Gizmos.DrawLine(headPoint.position, tailPoint.position);

            // Красный круг показывает зону поджога следующей спички
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(tailPoint.position, flashRange);
        }
    }

    // Рисуется только когда спичка выделена
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, spreadRadius);
    }
}
