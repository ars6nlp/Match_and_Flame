using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Симулятор уровня в редакторе: создаёт префаб уровня в пустой сцене и кадр за кадром
/// повторяет правила MatchLogic на настоящем Physics2D (OverlapCircle, CircleCast по реальным коллайдерам).
///
/// Повторяет порядок событий игры:
///  • поджог синхронно выполняет первый шаг горения (первая итерация корутины до yield);
///  • спичка, подожжённая в этом кадре, второй раз в нём не шагает;
///  • сгорание (FlashForward) — на кадр позже последнего шага пламени (выход из while);
///  • боковое распространение с подшагами (M-02), sideSpreadDelay, иммунитет к спичке игрока;
///  • Oscillator: позиция = старт + ось · sin(время · speed) · distance (время игры передаётся параметром).
/// Проверка наложения и привязка к слоту — копия DraggableMatch.
/// Не моделирует: перетаскивание подвижных спичек во время горения, разброс кадров. Окончательно — плейтест.
/// </summary>
public class LevelSimulator : IDisposable
{
    public struct Result
    {
        public bool valid;          // спичку можно поставить (нет наложения, есть слот в режиме слотов)
        public int total, burnt, golden, goldenBurnt, stars, extinguished;
        public float duration;
        public override string ToString() => valid ? $"{burnt}/{total} burnt, gold {goldenBurnt}/{golden}, {stars}*, {duration:0.0}s" + (extinguished > 0 ? $", {extinguished} put out" : "") : "invalid placement";
    }

    private class Sim
    {
        public MatchLogic logic;
        public Collider2D collider;
        public bool isPlayer, burning, burned, extinguished;
        public float wetUntil = -1f, windExposure;
        public Vector2 lastFlame;
        public float timer, prevMove, prevProgress;
        public int steppedFrame = -1;
        public Sim litBy;
        public bool litByFlash;
        public Vector3 startPos;
        public Quaternion startRot;
    }

    private class Osc
    {
        public Oscillator osc;
        public Vector3 start;
    }

    private class DropSim
    {
        public WaterDripper src;
        public float spawn;
        public Vector2 origin, driftPos, driftVel, prev;
    }

    public const float Dt = 1f / 60f;
    private const int MaxSubsteps = 8;

    private readonly GameObject root;
    private readonly List<Sim> matches = new List<Sim>();
    private readonly Dictionary<Collider2D, Sim> byCollider = new Dictionary<Collider2D, Sim>();
    private readonly List<Osc> oscillators = new List<Osc>();
    private readonly Collider2D[] slots;
    private readonly int matchMask;
    private readonly List<Fan> fans = new List<Fan>();
    private readonly List<WaterDripper> drippers = new List<WaterDripper>();
    private readonly List<DropSim> drops = new List<DropSim>();
    private int[] nextDrop;
    private float levelTime, firstIgnite = -1f;
    private readonly List<RaycastHit2D> dropHits = new List<RaycastHit2D>(8);
    private ContactFilter2D dropFilter;
    private Sim player;
    private DraggableMatch playerDrag;
    private int frame;

    public GameObject Root => root;
    public DraggableMatch Player => playerDrag;
    public bool FreePlacement => playerDrag != null && playerDrag.freePlacementMode;

    public LevelSimulator(GameObject levelPrefab) : this(InstantiateInNewScene(levelPrefab), owned: true) { }

    /// <summary>Симуляция уже собранного в открытой сцене уровня (лаборатория, сборка новых уровней).</summary>
    public LevelSimulator(GameObject sceneRoot, bool owned)
    {
        root = sceneRoot;
        ownsRoot = owned;
        root.transform.position = Vector3.zero; // GameManager создаёт уровень в нуле
        matchMask = LayerMask.GetMask("Match");

        foreach (MatchLogic m in root.GetComponentsInChildren<MatchLogic>(false))
        {
            var s = new Sim
            {
                logic = m,
                collider = m.GetComponent<Collider2D>(),
                isPlayer = m.GetComponent<DraggableMatch>() != null,
                startPos = m.transform.position,
                startRot = m.transform.rotation,
            };
            matches.Add(s);
            if (s.collider != null) byCollider[s.collider] = s;
            if (s.isPlayer) { player = s; playerDrag = m.GetComponent<DraggableMatch>(); }
        }
        foreach (Oscillator o in root.GetComponentsInChildren<Oscillator>(false))
            oscillators.Add(new Osc { osc = o, start = o.transform.position });

        fans.AddRange(root.GetComponentsInChildren<Fan>(false));
        drippers.AddRange(root.GetComponentsInChildren<WaterDripper>(false));
        nextDrop = new int[drippers.Count];
        dropFilter = new ContactFilter2D { useTriggers = true };
        dropFilter.SetLayerMask(WaterDripper.DropMask);

        int slotLayer = LayerMask.NameToLayer("Slot");
        slots = root.GetComponentsInChildren<Collider2D>(false).Where(c => c.gameObject.layer == slotLayer).ToArray();
    }

    private readonly bool ownsRoot;

    private static GameObject InstantiateInNewScene(GameObject levelPrefab)
    {
        Scene s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        return (GameObject)PrefabUtility.InstantiatePrefab(levelPrefab, s);
    }

    public void Dispose()
    {
        if (ownsRoot && root != null) { UnityEngine.Object.DestroyImmediate(root); return; }
        // Чужой уровень возвращаем в исходное состояние: следующий симулятор запомнит правильные стартовые позы
        foreach (Sim m in matches)
        {
            m.logic.transform.SetPositionAndRotation(m.startPos, m.startRot);
            if (m.collider != null) m.collider.enabled = true;
        }
        foreach (Osc o in oscillators) o.osc.transform.position = o.start;
        Physics2D.SyncTransforms();
    }

    /// <summary>Переставить спичку (для лаборатории): новая стартовая поза.</summary>
    public void SetStartPose(MatchLogic match, Vector2 position, float rotZ)
    {
        Sim s = matches.First(m => m.logic == match);
        s.startPos = position;
        s.startRot = Quaternion.Euler(0, 0, rotZ);
    }

    public bool Burned(MatchLogic match) => matches.First(m => m.logic == match).burned;

    /// <summary>Кто поджёг спичку в последнем прогоне: имя и способ ("~" — боковое пламя, ">" — вспышка).</summary>
    public string LitBy(MatchLogic match)
    {
        Sim s = matches.First(m => m.logic == match);
        return s.litBy == null ? "" : (s.litByFlash ? ">" : "~") + s.litBy.logic.name;
    }

    public bool Extinguished(MatchLogic match) => matches.First(m => m.logic == match).extinguished;
    public bool HasMechanics => fans.Count > 0 || drippers.Count > 0;

    /// <summary>
    /// Сколько секунд перебирать момент постановки спички: период расписания капель, циклов вентиляторов
    /// и маятников (не больше 8 с). 0 — от момента ничего не зависит.
    /// </summary>
    public float TimingPeriod()
    {
        float p = 0f;
        foreach (WaterDripper d in drippers)
            p = Mathf.Max(p, d.DripMode == WaterDripper.Mode.Continuous ? d.Interval : d.StartDelay + d.DropCount * d.Interval);
        foreach (Fan f in fans) if (f.FanMode == Fan.Mode.Cycle) p = Mathf.Max(p, f.CyclePeriod);
        foreach (Osc o in oscillators) if (o.osc.speed > 0f) p = Mathf.Max(p, 2f * Mathf.PI / o.osc.speed);
        return Mathf.Min(p, 8f);
    }

    public int MatchCount => matches.Count;
    public int GoldenCount => matches.Count(m => m.logic.isGolden);
    public IEnumerable<Collider2D> Slots => slots;

    /// <summary>Центр коллайдера спички игрока, если её корень стоит в rootPos с поворотом rotZ.</summary>
    public Vector2 PlayerCenterFor(Vector2 rootPos, float rotZ) =>
        rootPos + (Vector2)(Quaternion.Euler(0, 0, rotZ) * Vector3.Scale(player.collider.offset, player.logic.transform.lossyScale));

    /// <summary>Поза корня, при которой центр коллайдера спички игрока окажется в center.</summary>
    public Vector2 PlayerRootFor(Vector2 center, float rotZ) =>
        center - (Vector2)(Quaternion.Euler(0, 0, rotZ) * Vector3.Scale(player.collider.offset, player.logic.transform.lossyScale));

    public float PlayerStartRotation => player.startRot.eulerAngles.z;
    public Vector2 PlayerStartRoot => player.startPos;

    /// <summary>
    /// Игрок ставит спичку: корень в rootPos, поворот rotZ (кратно 90° к исходному), время игры gameTime
    /// (фаза Oscillator). Возвращает итог цепной реакции.
    /// </summary>
    public Result Run(Vector2 rootPos, float rotZ, float gameTime = 0f, float maxSeconds = 60f)
    {
        Reset(gameTime);
        PreRoll(gameTime);
        Transform pt = player.logic.transform;
        pt.SetPositionAndRotation(rootPos, Quaternion.Euler(0, 0, rotZ));
        Physics2D.SyncTransforms();

        var result = new Result { total = matches.Count, golden = GoldenCount };

        // Наложение на другие спички — как DraggableMatch.CheckOverlap (до привязки к слоту)
        foreach (Sim m in matches)
            if (m != player && Overlap(player.logic, m.logic)) return result;

        if (!FreePlacement)
        {
            Vector2 center = pt.TransformPoint(player.collider.offset);
            Collider2D slot = Physics2D.OverlapCircle(center, 0.5f, playerDrag.slotLayer);
            if (slot == null) return result;
            pt.rotation = slot.transform.rotation;
            Vector3 newCenter = pt.TransformPoint(player.collider.offset);
            pt.position = slot.transform.position + (pt.position - newCenter);
            Physics2D.SyncTransforms();
        }

        result.valid = true;
        return Burn(player, result, gameTime, maxSeconds);
    }

    /// <summary>Поджечь одну спичку уровня (спичка игрока убрана с поля) — карта связей «что от чего горит».</summary>
    public Result RunFrom(MatchLogic start, float gameTime = 0f, float maxSeconds = 60f)
    {
        Reset(gameTime);
        PreRoll(gameTime);
        player.logic.transform.position = new Vector3(100f, 100f, 0f);
        Physics2D.SyncTransforms();
        var result = new Result { total = matches.Count, golden = GoldenCount, valid = true };
        return Burn(matches.First(m => m.logic == start), result, gameTime, maxSeconds);
    }

    private Result Burn(Sim first, Result result, float gameTime, float maxSeconds)
    {
        float t = 0f;
        frame = 0;
        levelTime = gameTime;
        firstIgnite = gameTime; // LevelClock.MarkFirstIgnite
        Ignite(first);
        while (t < maxSeconds)
        {
            frame++;
            t += Dt;
            levelTime = gameTime + t;
            MoveOscillators(levelTime);
            if (drippers.Count > 0) StepDrops(); // как в кадре игры: Update (маятник, капли), затем корутины огня
            bool any = false;
            for (int i = 0; i < matches.Count; i++)
            {
                Sim m = matches[i];
                if (!m.burning) continue;
                any = true;
                if (m.steppedFrame == frame) continue;
                Step(m);
            }
            if (!any) break;
        }
        result.duration = t;
        result.burnt = matches.Count(m => m.burned);
        result.goldenBurnt = matches.Count(m => m.burned && m.logic.isGolden);
        result.extinguished = matches.Count(m => m.extinguished);
        result.stars = VisualTheme.Current.StarsFor(result.burnt, result.total);
        return result;
    }

    private void Reset(float gameTime)
    {
        foreach (Sim m in matches)
        {
            m.burning = m.burned = m.extinguished = false;
            m.wetUntil = -1f;
            m.windExposure = 0f;
            m.timer = m.prevMove = m.prevProgress = 0f;
            m.steppedFrame = -1;
            m.litBy = null;
            m.logic.transform.SetPositionAndRotation(m.startPos, m.startRot);
            if (m.collider != null) m.collider.enabled = true;
        }
        MoveOscillators(gameTime);
        drops.Clear();
        for (int i = 0; i < nextDrop.Length; i++) nextDrop[i] = 0;
        firstIgnite = -1f;
        levelTime = gameTime;
    }

    /// <summary>Капли до постановки спички: с начала уровня до gameTime (могут заранее намочить спички).</summary>
    private void PreRoll(float gameTime)
    {
        if (drippers.Count == 0) return;
        Transform pt = player.logic.transform;
        Vector3 pPos = pt.position;
        Quaternion pRot = pt.rotation;
        pt.SetPositionAndRotation(player.startPos, player.startRot); // до броска спичка игрока лежит у старта
        levelTime = 0f;
        MoveOscillators(0f);
        while (levelTime + Dt <= gameTime + 1e-4f)
        {
            levelTime += Dt;
            MoveOscillators(levelTime);
            StepDrops();
        }
        levelTime = gameTime;
        MoveOscillators(gameTime);
        pt.SetPositionAndRotation(pPos, pRot);
        Physics2D.SyncTransforms();
    }

    // ---------------------------------------------------------------- Капли (копия WaterDripper.Update/Step/Resolve)

    private void StepDrops()
    {
        for (int d = 0; d < drippers.Count; d++)
        {
            WaterDripper src = drippers[d];
            while (src.DropTime(nextDrop[d], firstIgnite, out float t) && levelTime >= t)
            {
                if (drops.Count(x => x.src == src) < src.MaxDrops)
                    drops.Add(new DropSim { src = src, spawn = t, origin = src.NozzleWorld, prev = src.NozzleWorld });
                nextDrop[d]++;
            }
        }
        for (int i = 0; i < drops.Count; i++)
        {
            DropSim drop = drops[i];
            float age = levelTime - drop.spawn;
            if (fans.Count > 0)
            {
                WindSample w = Fan.SampleFans(fans, drop.prev, levelTime, firstIgnite);
                drop.driftVel += w.drift * Dt;
                drop.driftPos += drop.driftVel * Dt;
            }
            Vector2 pos = drop.origin + drop.driftPos + Vector2.down * drop.src.FallDistance(age);
            Vector2 delta = pos - drop.prev;
            float dist = delta.magnitude;
            bool stopped = false;
            if (dist > 0.0001f)
            {
                int n = Physics2D.CircleCast(drop.prev, drop.src.DropRadius, delta / dist, dropFilter, dropHits, dist);
                for (int h = 0; h < n && !stopped; h++) stopped = ResolveDrop(dropHits[h].collider, drop.src);
            }
            drop.prev = pos;
            if (stopped || pos.y < drop.src.FloorY || age > 8f) { drops.RemoveAt(i); i--; }
        }
    }

    private bool ResolveDrop(Collider2D col, WaterDripper src)
    {
        if (byCollider.TryGetValue(col, out Sim m))
        {
            if (m.burned) return false;
            if (m.isPlayer && !m.burning) return false;
            if (m.burning) { m.burning = false; m.extinguished = true; }
            else if (!m.extinguished) m.wetUntil = levelTime + src.WetDuration;
            return true;
        }
        if (col.GetComponent<WaterShelter>() != null) return true;
        return col.gameObject.layer == LayerMask.NameToLayer("Slot");
    }

    private void MoveOscillators(float time)
    {
        if (oscillators.Count == 0) return;
        foreach (Osc o in oscillators)
            o.osc.transform.position = o.start + o.osc.movementAxis * (Mathf.Sin(time * o.osc.speed) * o.osc.distance);
        Physics2D.SyncTransforms();
    }

    private void Ignite(Sim m)
    {
        if (m.burning || m.burned || m.extinguished) return;
        if (m.wetUntil >= 0f && levelTime < m.wetUntil) return; // мокрая не загорается
        m.burning = true;
        m.timer = m.prevMove = m.prevProgress = 0f;
        m.windExposure = 0f;
        m.lastFlame = m.logic.headPoint.position;
        // StartCoroutine выполняет первую итерацию сразу
        m.steppedFrame = frame;
        Advance(m);
    }

    private void Step(Sim m)
    {
        m.steppedFrame = frame;
        if (m.timer >= m.logic.burnTime) { Finish(m); return; }
        Advance(m);
    }

    private void Advance(Sim m)
    {
        MatchLogic l = m.logic;
        // Ветер — как в MatchLogic.BurnCoroutine: проба у прошлой точки пламени, задувание, ускорение
        WindSample w = fans.Count > 0 ? Fan.SampleFans(fans, m.lastFlame, levelTime, firstIgnite) : default;
        if (w.extinguish)
        {
            m.windExposure += Dt;
            if (m.windExposure >= w.extinguishTime) { m.burning = false; m.extinguished = true; return; }
        }
        else m.windExposure = 0f;
        m.timer += Dt * (1f + w.burnBoost);
        float progress = m.timer / l.burnTime;
        float movement = l.fireMovementCurve.Evaluate(progress);
        Vector2 head = l.headPoint.position, tail = l.tailPoint.position;
        float burnLength = Vector2.Distance(head, tail);
        float advance = (movement - m.prevMove) * burnLength;
        int steps = Mathf.Clamp(Mathf.CeilToInt(advance / Mathf.Max(l.spreadRadius * 0.5f, 0.01f)), 1, MaxSubsteps);
        for (int s = 1; s <= steps; s++)
        {
            float k = (float)s / steps;
            Vector2 p = Vector2.Lerp(head, tail, Mathf.Lerp(m.prevMove, movement, k));
            SpreadSideways(m, p + w.push, Mathf.Lerp(m.prevProgress, progress, k), l.spreadRadius + w.radiusBonus);
        }
        m.prevMove = movement;
        m.prevProgress = progress;
        m.lastFlame = Vector2.Lerp(head, tail, movement);
    }

    private void Finish(Sim m)
    {
        m.burning = false;
        m.burned = true;
        if (m.collider != null) m.collider.enabled = false;
        Physics2D.SyncTransforms();
        FlashForward(m);
    }

    // Копия MatchLogic.SpreadSideways
    private void SpreadSideways(Sim m, Vector2 flamePos, float progress, float radius)
    {
        MatchLogic l = m.logic;
        var filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(matchMask);
        // Свой список на каждый вызов: поджог соседки внутри цикла сразу запускает её шаг (как StartCoroutine
        // в игре), и общий список перезаписался бы. В игре у каждой спички свой sideHits.
        var hits = new List<Collider2D>(16);
        Physics2D.OverlapCircle(flamePos, radius, filter, hits);
        for (int i = 0; i < hits.Count; i++)
        {
            Collider2D col = hits[i];
            if (col == m.collider || !byCollider.TryGetValue(col, out Sim side)) continue;
            if (side.burned || side.burning) continue;
            if (m.isPlayer && side.logic.ignorePlayerMatch) continue;

            Vector2 myHead = l.headPoint.position, myTail = l.tailPoint.position;
            Vector2 sHead = side.logic.headPoint.position, sTail = side.logic.tailPoint.position;
            Vector2 diff = (sHead + sTail) / 2f - (myHead + myTail) / 2f;
            Vector2 myForward = (myHead - myTail).normalized;
            Vector2 sideForward = (sHead - sTail).normalized;
            float forwardShift = Mathf.Abs(Vector2.Dot(diff, myForward));
            float sideShift = Mathf.Abs(Vector2.Dot(diff, new Vector2(-myForward.y, myForward.x)));
            if (sideShift < 0.3f) continue;

            bool isParallel = Mathf.Abs(Vector2.Dot(myForward, sideForward)) > 0.8f;
            if (isParallel)
            {
                Vector2 sideVector = sHead - sTail;
                float sideLength = sideVector.magnitude;
                float t = Vector2.Dot(flamePos - sTail, sideVector / sideLength) / sideLength;
                if (t < -0.1f || t > 1.1f) continue;
            }
            float matchLength = Vector2.Distance(myHead, myTail);
            if (isParallel && forwardShift < matchLength * 0.25f && progress <= l.sideSpreadDelay) continue;

            if (!side.burning) { side.litBy = m; side.litByFlash = false; }
            Ignite(side);
        }
    }

    // Копия MatchLogic.FlashForward
    private void FlashForward(Sim m)
    {
        MatchLogic l = m.logic;
        Vector2 tail = l.tailPoint.position;
        Vector2 dir = ((Vector2)l.tailPoint.position - (Vector2)l.headPoint.position).normalized;
        float range = l.flashRange;
        if (fans.Count > 0) range = Mathf.Max(0.02f, l.flashRange + Vector2.Dot(Fan.SampleFans(fans, tail, levelTime, firstIgnite).flash, dir));
        RaycastHit2D hit = Physics2D.CircleCast(tail, l.flashRadius, dir, range, matchMask);
        if (hit.collider == null || hit.collider == m.collider || !byCollider.TryGetValue(hit.collider, out Sim next)) return;
        if (next.burned || next.burning) return;
        if (m.isPlayer && next.logic.ignorePlayerMatch) return;
        next.litBy = m; next.litByFlash = true;
        Ignite(next);
    }

    // Копия DraggableMatch.CheckOverlap
    public static bool Overlap(MatchLogic a, MatchLogic b)
    {
        if (a == null || b == null || a.headPoint == null || a.tailPoint == null || b.headPoint == null || b.tailPoint == null) return false;
        const float minAllowedDistance = 0.25f;
        Vector2 a1 = a.headPoint.position, a2 = a.tailPoint.position, b1 = b.headPoint.position, b2 = b.tailPoint.position;
        if (SegmentsIntersect(a1, a2, b1, b2)) return true;
        for (int i = 1; i <= 3; i++)
        {
            if (DistancePointToSegment(Vector2.Lerp(a1, a2, i * 0.25f), b1, b2) < minAllowedDistance) return true;
            if (DistancePointToSegment(Vector2.Lerp(b1, b2, i * 0.25f), a1, a2) < minAllowedDistance) return true;
        }
        return false;
    }

    private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float den = ((b.x - a.x) * (d.y - c.y)) - ((b.y - a.y) * (d.x - c.x));
        if (den == 0) return false;
        float r = (((a.y - c.y) * (d.x - c.x)) - ((a.x - c.x) * (d.y - c.y))) / den;
        float s = (((a.y - c.y) * (b.x - a.x)) - ((a.x - c.x) * (b.y - a.y))) / den;
        return r > 0.05f && r < 0.95f && s > 0.05f && s < 0.95f;
    }

    private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float l2 = ab.sqrMagnitude;
        if (l2 == 0) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
        return Vector2.Distance(p, a + t * ab);
    }
}
