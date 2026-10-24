using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Механика «Капля воды». Кран роняет капли по расписанию от начала уровня (LevelClock):
/// капля k отрывается в момент startDelay + k·interval (в режиме «при первом поджоге» — от момента поджога).
/// Падение — по формуле (разгон gravity до maxSpeed), поэтому траектория одна и та же при любом FPS.
/// Перед отрывом у крана набухает висящая капля — по ней видно ритм.
///
/// Попадание:
///  • в горящую спичку — пламя тушится (спичка «потушена»: не горит, не поджигает, не считается сгоревшей);
///  • в несгоревшую спичку — она мокнет на wetDuration секунд и не загорается от соседей;
///  • в укрытие (WaterShelter) или слот — капля разбивается и дальше не летит;
///  • ниже floorY — брызги и исчезновение.
/// Сгоревшие спички капля пролетает насквозь; спичку игрока до поджога не трогает.
/// Капли берутся из пула, созданного в Awake; в Update нет аллокаций.
/// </summary>
[DisallowMultipleComponent]
public class WaterDripper : MonoBehaviour
{
    public enum Mode { Continuous, Limited, OnFirstIgnite }

    [Header("Расписание")]
    [SerializeField] private Mode mode = Mode.Continuous;
    [Tooltip("Секунд между каплями")]
    [SerializeField] private float interval = 2f;
    [Tooltip("Отрыв первой капли, секунд от начала уровня (или от поджога в режиме OnFirstIgnite) — фаза")]
    [SerializeField] private float startDelay = 1f;
    [Tooltip("Limited / OnFirstIgnite: сколько капель всего")]
    [SerializeField] private int dropCount = 1;

    [Header("Падение")]
    [SerializeField] private float startSpeed = 0f;
    [SerializeField] private float gravity = 9f;
    [SerializeField] private float maxSpeed = 6f;
    [Tooltip("Радиус капли для столкновений, мировые единицы")]
    [SerializeField] private float dropRadius = 0.08f;
    [Tooltip("Ниже этой мировой высоты капля разбивается о «пол»")]
    [SerializeField] private float floorY = -5.2f;
    [Tooltip("Точка отрыва относительно крана (локально)")]
    [SerializeField] private Vector2 nozzle = new Vector2(0f, -0.3f);

    [Header("Вода и спички")]
    [Tooltip("Сколько секунд мокрая спичка не загорается")]
    [SerializeField] private float wetDuration = 3f;
    [Tooltip("Капель одновременно в полёте (размер пула)")]
    [SerializeField] private int maxDrops = 4;

    [Header("Вид")]
    [SerializeField] private Sprite dropSprite;
    [SerializeField] private Color dropColor = Color.white;
    [Tooltip("Размер капли на экране, мировые единицы")]
    [SerializeField] private float dropSize = 0.22f;
    [Tooltip("Вытягивание капли по скорости (доля на единицу скорости)")]
    [SerializeField] private float stretchPerSpeed = 0.06f;
    [Tooltip("Висящая капля у крана: набухает перед отрывом")]
    [SerializeField] private SpriteRenderer hangingDrop;
    [Tooltip("За сколько секунд до отрыва капля начинает набухать")]
    [SerializeField] private float swellTime = 0.6f;
    [SerializeField] private int sortingOrder = 20;

    // ---------------------------------------------------------------- Расписание и падение (общие с симулятором)

    public float Interval => interval;
    public float StartDelay => startDelay;
    public int DropCount => dropCount;
    public Mode DripMode => mode;
    public int MaxDrops => maxDrops;
    public float WetDuration => wetDuration;
    public float DropRadius => dropRadius;
    public float FloorY => floorY;
    public Vector2 NozzleWorld => transform.TransformPoint(nozzle);

    /// <summary>Момент отрыва капли k по часам уровня; false — такой капли не будет (пока или вообще).</summary>
    public bool DropTime(int k, float firstIgniteTime, out float time)
    {
        time = 0f;
        if (k < 0) return false;
        if (mode != Mode.Continuous && k >= dropCount) return false;
        float origin = 0f;
        if (mode == Mode.OnFirstIgnite)
        {
            if (firstIgniteTime < 0f) return false;
            origin = firstIgniteTime;
        }
        time = origin + startDelay + k * Mathf.Max(0.05f, interval);
        return true;
    }

    /// <summary>Сколько пролетела капля за age секунд после отрыва.</summary>
    public float FallDistance(float age)
    {
        if (age <= 0f) return 0f;
        float g = Mathf.Max(0.001f, gravity);
        float v0 = Mathf.Min(startSpeed, maxSpeed);
        float tCap = Mathf.Max(0f, (maxSpeed - v0) / g);
        if (age <= tCap) return v0 * age + 0.5f * g * age * age;
        float dCap = v0 * tCap + 0.5f * g * tCap * tCap;
        return dCap + maxSpeed * (age - tCap);
    }

    public float FallSpeed(float age) => Mathf.Min(Mathf.Min(startSpeed, maxSpeed) + Mathf.Max(0f, age) * gravity, maxSpeed);

    /// <summary>Маска слоёв, по которым летит капля: спички, слоты, укрытия (Default).</summary>
    public static int DropMask => LayerMask.GetMask("Match", "Slot", "Default");

    // ---------------------------------------------------------------- Капли в игре

    private struct Drop
    {
        public bool active;
        public float spawnTime;
        public Vector2 origin, driftPos, driftVel, prev;
        public Transform view;
        public SpriteRenderer sprite;
    }

    private Drop[] drops;
    private int nextIndex;
    private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(8);
    private ContactFilter2D filter;
    private bool stopped;

    private void Awake()
    {
        drops = new Drop[Mathf.Max(1, maxDrops)];
        for (int i = 0; i < drops.Length; i++)
        {
            var go = new GameObject("Drop" + i);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dropSprite;
            sr.color = dropColor;
            sr.sortingOrder = sortingOrder;
            go.SetActive(false);
            drops[i] = new Drop { view = go.transform, sprite = sr };
        }
        filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(DropMask);
        if (hangingDrop != null) hangingDrop.transform.localScale = Vector3.zero;
    }

    private void Update()
    {
        if (stopped) return;
        float now = LevelClock.Now;
        float first = LevelClock.FirstIgniteTime;

        // Отрыв новых капель (с учётом времени, прошедшего с момента отрыва внутри кадра)
        while (DropTime(nextIndex, first, out float t) && now >= t)
        {
            Spawn(t);
            nextIndex++;
        }

        // Висящая капля: набухает к следующему отрыву
        if (hangingDrop != null)
        {
            float s = 0f;
            if (DropTime(nextIndex, first, out float next)) s = Mathf.Clamp01(1f - (next - now) / Mathf.Max(0.01f, swellTime));
            hangingDrop.transform.localScale = Vector3.one * (s * dropSize / SpriteWorldSize(hangingDrop.sprite));
            hangingDrop.transform.position = NozzleWorld + Vector2.down * (s * dropSize * 0.35f);
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        for (int i = 0; i < drops.Length; i++)
            if (drops[i].active) Step(ref drops[i], now, dt);
    }

    private void Spawn(float time)
    {
        int slot = -1;
        for (int i = 0; i < drops.Length; i++) if (!drops[i].active) { slot = i; break; }
        if (slot < 0) return; // пул занят — каплю пропускаем (настройте maxDrops)
        ref Drop d = ref drops[slot];
        d.active = true;
        d.spawnTime = time;
        d.origin = NozzleWorld;
        d.prev = d.origin;
        d.driftPos = d.driftVel = Vector2.zero;
        d.view.gameObject.SetActive(true);
        d.view.position = d.origin;
        if (MechanicsSettings.Current.dripDrop != null) GameJuice.PlayMechanicSfx(MechanicsSettings.Current.dripDrop, 0.4f);
    }

    private void Step(ref Drop d, float now, float dt)
    {
        float age = now - d.spawnTime;
        if (Fan.ActiveCount > 0)
        {
            WindSample w = Fan.Sample(d.prev);
            d.driftVel += w.drift * dt;
            d.driftPos += d.driftVel * dt;
        }
        Vector2 pos = d.origin + d.driftPos + Vector2.down * FallDistance(age);

        // Что лежит на пути капли за этот кадр (по порядку)
        Vector2 delta = pos - d.prev;
        float dist = delta.magnitude;
        if (dist > 0.0001f)
        {
            int n = Physics2D.CircleCast(d.prev, dropRadius, delta / dist, filter, hits, dist);
            for (int h = 0; h < n; h++)
            {
                RaycastHit2D hit = hits[h];
                if (Resolve(hit.collider, now))
                {
                    Splash(hit.point);
                    Release(ref d);
                    return;
                }
            }
        }

        d.prev = pos;
        if (pos.y < floorY || age > 8f)
        {
            Splash(new Vector2(pos.x, Mathf.Max(pos.y, floorY)));
            Release(ref d);
            return;
        }
        d.view.position = pos;
        float speed = FallSpeed(age);
        d.view.localScale = new Vector3(1f, 1f + speed * stretchPerSpeed, 1f) * (dropSize / SpriteWorldSize(d.sprite.sprite));
    }

    /// <summary>Попадание в коллайдер; true — капля остановлена.</summary>
    private bool Resolve(Collider2D col, float now)
    {
        if (col.TryGetComponent(out MatchLogic match))
        {
            if (match.isBurnedOut) return false;
            if (match.IsPlayerMatch && !match.isBurning) return false; // спичку игрока до поджога не трогаем
            if (match.isBurning) match.Extinguish(MatchLogic.ExtinguishCause.Water);
            else match.Wet(wetDuration);
            return true;
        }
        if (col.TryGetComponent(out WaterShelter _)) return true;
        return col.gameObject.layer == LayerMask.NameToLayer("Slot");
    }

    private void Release(ref Drop d)
    {
        d.active = false;
        d.view.gameObject.SetActive(false);
    }

    private static void Splash(Vector2 at) => GameJuice.SpawnFx(MechanicsSettings.Current.splashFx, at);

    private static float SpriteWorldSize(Sprite s) => s != null ? Mathf.Max(0.0001f, Mathf.Max(s.bounds.size.x, s.bounds.size.y)) : 1f;

    /// <summary>Остановить капание (например, по сценарию уровня).</summary>
    public void StopDripping() => stopped = true;

    private void OnValidate()
    {
        interval = Mathf.Max(0.05f, interval);
        dropCount = Mathf.Max(1, dropCount);
        maxDrops = Mathf.Clamp(maxDrops, 1, 16);
        dropRadius = Mathf.Max(0.01f, dropRadius);
        gravity = Mathf.Max(0.01f, gravity);
        maxSpeed = Mathf.Max(0.1f, maxSpeed);
    }

    // ---------------------------------------------------------------- Отладка: траектория и ритм

    private void OnDrawGizmos()
    {
        if (!MechanicsSettings.Current.debugGizmos) return;
        Vector2 n = NozzleWorld;
        float bottom = floorY;
        // Первое препятствие по вертикали
        RaycastHit2D first = Physics2D.CircleCast(n, dropRadius, Vector2.down, Mathf.Max(0f, n.y - floorY), DropMask);
        if (first.collider != null) bottom = first.point.y;
        Gizmos.color = new Color(0.45f, 0.75f, 1f, 0.9f);
        Gizmos.DrawLine(n, new Vector2(n.x, bottom));
        // Метки каждые 0.25 с падения
        for (float t = 0.25f; t < 4f; t += 0.25f)
        {
            float y = n.y - FallDistance(t);
            if (y < bottom) break;
            Gizmos.DrawLine(new Vector2(n.x - 0.06f, y), new Vector2(n.x + 0.06f, y));
        }
        Gizmos.DrawWireSphere(new Vector2(n.x, bottom), dropRadius);
    }
}
