using System.Collections.Generic;
using UnityEngine;

/// <summary>Суммарное действие ветра в точке (от всех включённых вентиляторов).</summary>
public struct WindSample
{
    /// <summary>Вектор ветра (направление × сила).</summary>
    public Vector2 wind;
    /// <summary>Смещение точки, от которой пламя поджигает соседей (по ветру).</summary>
    public Vector2 push;
    /// <summary>Прибавка к радиусу бокового поджога.</summary>
    public float radiusBonus;
    /// <summary>Вектор прибавки к дальности вспышки: берётся проекция на направление спички.</summary>
    public Vector2 flash;
    /// <summary>Ускорение горения: 0.2 — спичка горит на 20% быстрее.</summary>
    public float burnBoost;
    /// <summary>Ветер здесь сильнее порога: пламя задувает за extinguishTime секунд.</summary>
    public bool extinguish;
    public float extinguishTime;
    /// <summary>Ускорение, с которым ветер сносит капли (0, если у вентиляторов dropDrift = 0).</summary>
    public Vector2 drift;

    public bool Any => wind.sqrMagnitude > 0f;
}

/// <summary>
/// Механика «Вентилятор». Дует вдоль своей оси X (поверните объект, чтобы сменить направление).
/// Ветер в зоне (прямоугольник или конус) направляет огонь: точка поджога соседей смещается по ветру,
/// радиус бокового поджога и дальность вспышки по ветру растут, против ветра — падают; горение ускоряется.
/// Если сила в точке пламени не меньше extinguishStrength, пламя мигает и гаснет через extinguishTime.
/// Сила падает от farStrength вдоль зоны: ближняя спичка гаснет, дальняя только отклоняется.
/// Режимы: всегда / циклы вкл-выкл по часам уровня / включается при первом поджоге.
/// Вся логика детерминирована: зависит только от LevelClock и положения вентилятора.
/// Тот же расчёт (SampleFans) использует редакторный симулятор уровней.
/// </summary>
[DisallowMultipleComponent]
public class Fan : MonoBehaviour
{
    public enum Mode { Always, Cycle, OnFirstIgnite }
    public enum Shape { Rectangle, Cone }

    [Header("Режим")]
    [SerializeField] private Mode mode = Mode.Always;
    [Tooltip("Цикл: секунд включён")]
    [SerializeField] private float onDuration = 2f;
    [Tooltip("Цикл: секунд выключен")]
    [SerializeField] private float offDuration = 2f;
    [Tooltip("Цикл: сдвиг фазы, секунды (0 — уровень начинается с включённого)")]
    [SerializeField] private float cyclePhase = 0f;
    [Tooltip("При первом поджоге: задержка включения, секунды")]
    [SerializeField] private float igniteDelay = 0f;

    [Header("Сила и зона")]
    [Tooltip("Сила у сопла (условные единицы 0–10)")]
    [SerializeField] private float strength = 4f;
    [Tooltip("Доля силы в дальнем конце зоны (1 — не убывает)")]
    [Range(0f, 1f)] [SerializeField] private float farStrength = 0.35f;
    [SerializeField] private Shape shape = Shape.Rectangle;
    [Tooltip("Длина зоны вдоль ветра, мировые единицы")]
    [SerializeField] private float zoneLength = 3f;
    [Tooltip("Ширина зоны у сопла")]
    [SerializeField] private float zoneWidth = 1.2f;
    [Tooltip("Конус: полный угол раскрытия, градусы")]
    [SerializeField] private float coneAngle = 30f;
    [Tooltip("Сопло: расстояние от центра вентилятора вдоль оси, откуда начинается зона")]
    [SerializeField] private float nozzleOffset = 0.4f;

    [Header("Действие на огонь (на единицу силы)")]
    [Tooltip("Смещение точки поджога соседей по ветру, мировые единицы")]
    [SerializeField] private float spreadPush = 0.06f;
    [Tooltip("Прибавка к радиусу бокового поджога")]
    [SerializeField] private float spreadRadiusBonus = 0.02f;
    [Tooltip("Прибавка к дальности вспышки по ветру (против ветра — убавка)")]
    [SerializeField] private float flashBoost = 0.12f;
    [Tooltip("Ускорение горения: доля на единицу силы")]
    [SerializeField] private float burnSpeedBoost = 0.08f;
    [Tooltip("Сила, начиная с которой пламя задувает")]
    [SerializeField] private float extinguishStrength = 6f;
    [Tooltip("Сколько секунд пламя держится в сильном ветре, прежде чем погаснуть")]
    [SerializeField] private float extinguishTime = 0.35f;

    [Header("Действие на капли")]
    [Tooltip("Боковое ускорение капли на единицу силы (0 — ветер капли не сносит)")]
    [SerializeField] private float dropDrift = 0f;

    [Header("Вид")]
    [SerializeField] private Transform blades;
    [Tooltip("Скорость лопастей, градусов в секунду на единицу силы")]
    [SerializeField] private float bladeSpeedPerStrength = 220f;
    [Tooltip("Разгон/остановка лопастей, доля в секунду")]
    [SerializeField] private float bladeSpinUp = 4f;
    [SerializeField] private ParticleSystem airflow;
    [Tooltip("Скорость частиц потока, единиц в секунду")]
    [SerializeField] private float airflowSpeed = 4f;
    [SerializeField] private SpriteRenderer statusLight;
    [SerializeField] private Color statusOn = new Color(1f, 0.71f, 0.28f, 1f);
    [SerializeField] private Color statusOff = new Color(0.35f, 0.22f, 0.16f, 1f);

    // ---------------------------------------------------------------- Реестр включённых вентиляторов

    private static readonly List<Fan> active = new List<Fan>(4);
    public static int ActiveCount => active.Count;

    private float bladeSpeed;
    private bool wasOn;

    private void OnEnable() { if (!active.Contains(this)) active.Add(this); }
    private void OnDisable() { active.Remove(this); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => active.Clear();

    private void Awake() => ConfigureAirflow();

    private void Update()
    {
        bool on = IsOn(LevelClock.Now, LevelClock.FirstIgniteTime);
        float target = on ? strength * bladeSpeedPerStrength : 0f;
        bladeSpeed = Mathf.MoveTowards(bladeSpeed, target, Mathf.Max(1f, strength * bladeSpeedPerStrength) * bladeSpinUp * Time.deltaTime);
        if (blades != null) blades.Rotate(0f, 0f, -bladeSpeed * Time.deltaTime, Space.Self);
        if (on != wasOn)
        {
            wasOn = on;
            if (airflow != null) { if (on) airflow.Play(true); else airflow.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
            if (statusLight != null) statusLight.color = on ? statusOn : statusOff;
        }
    }

    /// <summary>Подогнать поток частиц под зону: старт у сопла, долетают до конца зоны.</summary>
    private void ConfigureAirflow()
    {
        if (airflow == null) return;
        ParticleSystem.MainModule main = airflow.main;
        main.startSpeed = 0f; // движение задаёт модуль скорости вдоль локальной оси X — направления ветра
        ParticleSystem.VelocityOverLifetimeModule vel = airflow.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(airflowSpeed);
        vel.y = new ParticleSystem.MinMaxCurve(0f);
        vel.z = new ParticleSystem.MinMaxCurve(0f);
        main.startLifetime = Mathf.Max(0.1f, zoneLength / Mathf.Max(0.1f, airflowSpeed));
        ParticleSystem.ShapeModule sh = airflow.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(0.05f, zoneWidth * 0.8f, 0.05f);
        sh.position = new Vector3(nozzleOffset / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x)), 0f, 0f);
        airflow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        wasOn = false;
        if (statusLight != null) statusLight.color = statusOff;
    }

    // ---------------------------------------------------------------- Логика (общая для игры и симулятора)

    public float Strength => strength;
    public Mode FanMode => mode;
    public float CyclePeriod => onDuration + offDuration;
    public float ZoneLength => zoneLength;
    public float ExtinguishStrength => extinguishStrength;
    public float DropDrift => dropDrift;
    /// <summary>Куда дует: ось X объекта; при отражении (scale.x &lt; 0) — в обратную сторону, подставка остаётся внизу.</summary>
    public Vector2 Direction => transform.lossyScale.x < 0f ? -(Vector2)transform.right : (Vector2)transform.right;
    public Vector2 Nozzle => (Vector2)transform.position + Direction * nozzleOffset;

    public bool IsOn(float levelTime, float firstIgniteTime)
    {
        switch (mode)
        {
            case Mode.Cycle:
                float period = Mathf.Max(0.01f, onDuration + offDuration);
                return Mathf.Repeat(levelTime + cyclePhase, period) < onDuration;
            case Mode.OnFirstIgnite:
                return firstIgniteTime >= 0f && levelTime >= firstIgniteTime + igniteDelay;
            default:
                return true;
        }
    }

    /// <summary>Сила ветра этого вентилятора в точке (без учёта вкл/выкл).</summary>
    public float StrengthAt(Vector2 position)
    {
        Vector2 dir = Direction;
        Vector2 local = position - Nozzle;
        float along = Vector2.Dot(local, dir);
        if (along < 0f || along > zoneLength) return 0f;
        float across = Mathf.Abs(local.x * -dir.y + local.y * dir.x);
        float halfWidth = zoneWidth * 0.5f + (shape == Shape.Cone ? along * Mathf.Tan(coneAngle * 0.5f * Mathf.Deg2Rad) : 0f);
        if (across > halfWidth) return 0f;
        return strength * Mathf.Lerp(1f, farStrength, along / Mathf.Max(0.0001f, zoneLength));
    }

    /// <summary>Ветер в точке от включённых вентиляторов сцены.</summary>
    public static WindSample Sample(Vector2 position) =>
        SampleFans(active, position, LevelClock.Now, LevelClock.FirstIgniteTime);

    /// <summary>Ветер в точке от заданного списка вентиляторов (симулятор передаёт свои).</summary>
    public static WindSample SampleFans(IReadOnlyList<Fan> fans, Vector2 position, float levelTime, float firstIgniteTime)
    {
        var w = new WindSample();
        for (int i = 0; i < fans.Count; i++)
        {
            Fan f = fans[i];
            if (f == null || !f.IsOn(levelTime, firstIgniteTime)) continue;
            float s = f.StrengthAt(position);
            if (s <= 0f) continue;
            Vector2 dir = f.Direction;
            w.wind += dir * s;
            w.push += dir * (s * f.spreadPush);
            w.radiusBonus += s * f.spreadRadiusBonus;
            w.flash += dir * (s * f.flashBoost);
            w.burnBoost += s * f.burnSpeedBoost;
            w.drift += dir * (s * f.dropDrift);
            if (s >= f.extinguishStrength)
            {
                w.extinguishTime = w.extinguish ? Mathf.Min(w.extinguishTime, f.extinguishTime) : f.extinguishTime;
                w.extinguish = true;
            }
        }
        return w;
    }

    /// <summary>Сколько единиц вдоль зоны от сопла сила не ниже порога тушения (для Gizmos и документации).</summary>
    public float ExtinguishReach()
    {
        if (strength < extinguishStrength) return 0f;
        if (farStrength >= 1f) return zoneLength;
        return Mathf.Clamp(zoneLength * (1f - extinguishStrength / strength) / (1f - farStrength), 0f, zoneLength);
    }

    // ---------------------------------------------------------------- Отладка

    private void OnValidate()
    {
        onDuration = Mathf.Max(0.05f, onDuration);
        offDuration = Mathf.Max(0f, offDuration);
        zoneLength = Mathf.Max(0.1f, zoneLength);
        zoneWidth = Mathf.Max(0.05f, zoneWidth);
        extinguishTime = Mathf.Max(0f, extinguishTime);
    }

    private void OnDrawGizmos()
    {
        if (!MechanicsSettings.Current.debugGizmos) return;
        Vector2 dir = Direction, perp = new Vector2(-dir.y, dir.x), n = Nozzle;
        float tan = shape == Shape.Cone ? Mathf.Tan(coneAngle * 0.5f * Mathf.Deg2Rad) : 0f;
        const int Steps = 8;
        for (int i = 0; i < Steps; i++)
        {
            float a0 = zoneLength * i / Steps, a1 = zoneLength * (i + 1) / Steps;
            float s = strength * Mathf.Lerp(1f, farStrength, (a0 + a1) * 0.5f / zoneLength);
            Gizmos.color = s >= extinguishStrength ? new Color(1f, 0.2f, 0.1f, 0.9f) : Color.Lerp(new Color(0.4f, 0.8f, 1f, 0.3f), new Color(0.4f, 0.8f, 1f, 0.9f), s / Mathf.Max(0.01f, strength));
            float h0 = zoneWidth * 0.5f + a0 * tan, h1 = zoneWidth * 0.5f + a1 * tan;
            Gizmos.DrawLine(n + dir * a0 + perp * h0, n + dir * a1 + perp * h1);
            Gizmos.DrawLine(n + dir * a0 - perp * h0, n + dir * a1 - perp * h1);
            Gizmos.DrawLine(n + dir * a0, n + dir * a1);
        }
        float far = zoneWidth * 0.5f + zoneLength * tan;
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
        Gizmos.DrawLine(n + perp * zoneWidth * 0.5f, n - perp * zoneWidth * 0.5f);
        Gizmos.DrawLine(n + dir * zoneLength + perp * far, n + dir * zoneLength - perp * far);
        float reach = ExtinguishReach();
        if (reach > 0f)
        {
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.9f);
            float h = zoneWidth * 0.5f + reach * tan;
            Gizmos.DrawLine(n + dir * reach + perp * h, n + dir * reach - perp * h);
        }
    }
}
