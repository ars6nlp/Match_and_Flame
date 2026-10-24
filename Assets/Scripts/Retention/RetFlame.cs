using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Огонёк-персонаж из слоёв (hearth_flame2): внешний мягкий край, середина, светлое ядро, язычки вокруг, блик; цвета линии
/// (красно-оранжевый → амбер → светло-жёлтый у дуба), кончики прозрачные. Лицо: глаза с белком, зрачком, бликом и веком,
/// брови, румянец, 4 рта (улыбка, «о», радость, сон) — смена выражения плавная. Тень и отражение на полу, аддитивное свечение,
/// мелкие искры с покачиванием, дыхание squash/stretch от нижней точки, покачивание. Каждая стадия — свой размер, силуэт,
/// число язычков, искры и аксессуар (поленья, луч маяка, звёзды и нимб, лучи солнца); облик садится по силуэту стадии.
/// Все параметры — HearthVisualSettings («Огонёк»); при «Эффекты: низкие» — упрощённый вариант. Без выделений памяти в Update.
/// </summary>
public class RetFlame : MonoBehaviour
{
    private const int MaxTongues = 5;
    private RectTransform root, body, face, eyeL, eyeR, pupilL, pupilR, lidL, lidR, browL, browR, zzz, accRays, accBeam, starA, starB;
    private Image outer, mid, core, hl, glowImg, shadowImg, reflectImg, flashImg, haloImg, outfitImg, cheekL, cheekR, zzzImg, mouthA, mouthB, logs, raysImg, beamImg, starAImg, starBImg;
    private Image lidLImg, lidRImg;
    private readonly RectTransform[] tongues = new RectTransform[MaxTongues];
    private readonly Image[] tongueImgs = new Image[MaxTongues];
    private readonly Vector3[] tongueBase = new Vector3[MaxTongues];   // x, y, поворот
    private UIParticleField sparks;
    private float unit = 1000f, W = 120f, H = 150f, glowBase = 0.35f, glowNow, flashAt = -10f;
    private float blinkAt, blinkUntil, hop, moodUntil = -1f, nextLookAt, lid, lidTarget, browAngle, browY, mouthFade = 1f;
    private Vector2 look, lookTarget;
    private FlameMood mood = FlameMood.Idle, baseMood = FlameMood.Idle;
    private float moodStart;
    private int tongueCount, accessory = -1, stageIndex;
    private bool hi = true, setupDone;
    private Sprite mSmile, mO, mJoy, mSleep;
    private Color cOuter, cMid, cCore, cGlow, fOuter, fMid, fCore, fGlow, tOuter, tMid, tCore, tGlow;
    private float colorAt = -1f;

    public FlameMood Mood => mood;
    public RectTransform Rect => root;

    private static HearthVisualSettings V => HearthVisualSettings.Current;

    /// <summary>Макушка огонька (с обликом) в мировых координатах — над ней встаёт пузырь реплики.</summary>
    public Vector3 HeadWorld
    {
        get
        {
            float top = H * 0.94f;
            if (outfitImg != null && outfitImg.enabled)
            {
                var o = outfitImg.rectTransform;
                top = Mathf.Max(top, o.anchoredPosition.y + o.sizeDelta.y * 0.5f);
            }
            return body.TransformPoint(new Vector3(0f, top, 0f));
        }
    }

    /// <summary>Рот — сюда летит уголёк.</summary>
    public Vector3 MouthWorld => face.TransformPoint(new Vector3(0f, -W * 0.15f, 0f));

    /// <summary>unitSize — ширина комнаты (доли стадий считаются от неё); в окнах — HearthVisualSettings.popupFlameUnit.</summary>
    public static RetFlame Create(Transform parent, Vector2 pos, float unitSize)
    {
        RectTransform rt = RetUi.At(parent, "Flame", new Vector2(0.5f, 0.5f), pos, new Vector2(unitSize * 0.2f, unitSize * 0.26f));
        var f = rt.gameObject.AddComponent<RetFlame>();
        f.root = rt;
        f.unit = unitSize;
        f.Build();
        return f;
    }

    private Image Part(Transform parent, string name, string sprite, Color color, Vector2 pivot)
    {
        RectTransform r = RetUi.Rect(parent, name, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), pivot, Vector2.zero, Vector2.one * 10f);
        var img = r.gameObject.AddComponent<Image>();
        img.sprite = RetUi.Art(sprite); img.color = color; img.raycastTarget = false;
        return img;
    }

    private void Build()
    {
        Material add = HearthScene.Additive;
        reflectImg = Part(root, "Reflect", "fl2_reflect", Color.white, new Vector2(0.5f, 0.5f)); reflectImg.material = add;
        shadowImg = Part(root, "Shadow", "fl2_shadow", new Color(1f, 1f, 1f, 0.75f), new Vector2(0.5f, 0.5f));
        glowImg = Part(root, "Glow", "fl2_glow", Color.white, new Vector2(0.5f, 0.5f)); glowImg.material = add;
        raysImg = Part(root, "Rays", "acc_rays", new Color(1f, 0.9f, 0.6f, 0.32f), new Vector2(0.5f, 0.5f)); raysImg.material = add; accRays = raysImg.rectTransform;
        beamImg = Part(root, "Beam", "acc_beam", new Color(1f, 0.95f, 0.75f, 0.2f), new Vector2(0f, 0.5f)); beamImg.material = add; accBeam = beamImg.rectTransform;
        haloImg = Part(root, "Halo", "fl_halo", new Color(1f, 0.95f, 0.75f, 0.85f), new Vector2(0.5f, 0.5f)); haloImg.material = add;
        sparks = RetUi.Fill(root, "Sparks").gameObject.AddComponent<UIParticleField>();
        sparks.raycastTarget = false; sparks.maxParticles = 10; sparks.emissionRate = 0f; sparks.material = add;
        sparks.spawnHeight = new Vector2(0.35f, 0.7f); sparks.lifetime = new Vector2(1.2f, 2.2f); sparks.size = new Vector2(6f, 12f);
        sparks.riseSpeed = new Vector2(40f, 90f); sparks.sway = 22f; sparks.burstSize = new Vector2(8f, 16f);

        body = RetUi.Rect(root, "Body", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(100f, 125f));
        for (int i = 0; i < MaxTongues; i++)
        {
            tongueImgs[i] = Part(body, "Tongue" + i, "fl2_tongue", Color.white, new Vector2(0.5f, 0.05f));
            tongues[i] = tongueImgs[i].rectTransform;
        }
        outer = Part(body, "Outer", "fl2_outer", Color.white, new Vector2(0.5f, 0f));
        mid = Part(body, "Mid", "fl2_mid", Color.white, new Vector2(0.5f, 0f));
        core = Part(body, "Core", "fl2_core", Color.white, new Vector2(0.5f, 0f));
        hl = Part(body, "Highlight", "fl2_hl", new Color(1f, 1f, 1f, 0.5f), new Vector2(0.5f, 0.5f));
        logs = Part(root, "Logs", "acc_logs", Color.white, new Vector2(0.5f, 0.5f));

        face = RetUi.Rect(body, "Face", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 60f));
        cheekL = Part(face, "CheekL", "fc2_blush", new Color(1f, 1f, 1f, 0.8f), new Vector2(0.5f, 0.5f));
        cheekR = Part(face, "CheekR", "fc2_blush", new Color(1f, 1f, 1f, 0.8f), new Vector2(0.5f, 0.5f));
        eyeL = Eye("EyeL", out pupilL, out lidL, out lidLImg);
        eyeR = Eye("EyeR", out pupilR, out lidR, out lidRImg);
        browL = Part(face, "BrowL", "fc2_brow", Color.white, new Vector2(0.5f, 0.5f)).rectTransform;
        browR = Part(face, "BrowR", "fc2_brow", Color.white, new Vector2(0.5f, 0.5f)).rectTransform;
        mSmile = RetUi.Art("fc2_m_smile"); mO = RetUi.Art("fc2_m_o"); mJoy = RetUi.Art("fc2_m_joy"); mSleep = RetUi.Art("fc2_m_sleep");
        mouthB = Part(face, "MouthPrev", "fc2_m_smile", Color.white, new Vector2(0.5f, 0.5f));
        mouthA = Part(face, "Mouth", "fc2_m_smile", Color.white, new Vector2(0.5f, 0.5f));
        mouthB.preserveAspect = mouthA.preserveAspect = true;
        outfitImg = Part(body, "Outfit", null, Color.white, new Vector2(0.5f, 0.5f));
        outfitImg.preserveAspect = true;
        starAImg = Part(root, "StarA", "rm_star", Color.white, new Vector2(0.5f, 0.5f)); starAImg.material = add; starA = starAImg.rectTransform;
        starBImg = Part(root, "StarB", "rm_star", Color.white, new Vector2(0.5f, 0.5f)); starBImg.material = add; starB = starBImg.rectTransform;
        flashImg = Part(root, "Flash", "fl2_glow", new Color(1f, 1f, 1f, 0f), new Vector2(0.5f, 0.5f)); flashImg.material = add;
        zzzImg = Part(root, "Zzz", "fc_zzz", Color.white, new Vector2(0.5f, 0.5f));
        zzz = zzzImg.rectTransform;
        zzzImg.enabled = false;
        blinkAt = Time.unscaledTime + Random.Range(2f, 4f);
    }

    private RectTransform Eye(string name, out RectTransform pupil, out RectTransform lidRt, out Image lidImg)
    {
        Image white = Part(face, name, "fc2_white", Color.white, new Vector2(0.5f, 0.5f));
        var rt = white.rectTransform;
        pupil = RetUi.Rect(rt, "Pupil", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 10f);
        var p = pupil.gameObject.AddComponent<Image>(); p.sprite = RetUi.Art("fc2_pupil"); p.raycastTarget = false;
        lidRt = RetUi.Rect(rt, "Lid", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 2f), Vector2.one * 10f);
        lidImg = lidRt.gameObject.AddComponent<Image>(); lidImg.sprite = RetUi.Art("fc2_lid"); lidImg.raycastTarget = false;
        return rt;
    }

    private static void Size(Graphic g, float x, float y, float w, float h) => Size(g.rectTransform, x, y, w, h);

    private static void Size(RectTransform r, float x, float y, float w, float h)
    {
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(w, h);
    }

    /// <summary>stage 0..7, line 0 сосна / 1 дуб / 2 вишня, outfit −1 — без облика, glowBoost 0..0.7 — от серии.</summary>
    public void Setup(int stage, int line, int outfit, float glowBoost)
    {
        HearthVisualSettings v = V;
        RetentionSettings c = RetUi.Cfg;
        int st = Mathf.Clamp(stage, 0, 7);
        stageIndex = st;
        line = Mathf.Clamp(line, 0, 2);
        hi = !v.simplifyOnLowFx || FxQuality.High;
        W = unit * Get(v.flameWidth, st, 0.13f);
        H = W * Get(v.flameAspect, st, 1.22f);
        tongueCount = Mathf.Clamp(Mathf.RoundToInt(Get(v.flameTongues, st)), 0, MaxTongues);
        if (!hi) tongueCount = Mathf.Min(tongueCount, 2);
        accessory = v.flameAccessories ? Mathf.RoundToInt(Get(v.flameAccessory, st)) : 0;

        // Цвета линии (искра — светлее); смена линии/облика — плавно
        Color o = Pick(v.lineOuter, line, new Color(1f, 0.33f, 0.12f)), m = Pick(v.lineMid, line, new Color(1f, 0.6f, 0.18f));
        Color k = Pick(v.lineCore, line, new Color(1f, 0.9f, 0.6f)), g = Pick(v.lineGlow, line, new Color(1f, 0.55f, 0.2f));
        if (st == 0) { o = Color.Lerp(o, m, 0.35f); m = Color.Lerp(m, k, 0.25f); }
        tOuter = o; tMid = m; tCore = k; tGlow = g;
        if (!setupDone || v.colorBlendSeconds <= 0f) { cOuter = o; cMid = m; cCore = k; cGlow = g; colorAt = -1f; }
        else if (cOuter != o || cMid != m || cCore != k) { fOuter = cOuter; fMid = cMid; fCore = cCore; fGlow = cGlow; colorAt = Time.unscaledTime; }
        setupDone = true;

        // Размеры слоёв от ширины стадии
        body.sizeDelta = new Vector2(W, H);
        Size(outer, 0f, 0f, W, H);
        Size(mid, 0f, W * 0.01f, W * 0.8f, H * 0.82f);
        Size(core, 0f, W * 0.02f, W * 0.56f, Mathf.Min(H * 0.62f, W * 0.75f));
        Size(hl, -W * 0.22f, H * 0.52f, W * 0.18f, W * 0.36f);
        float faceY = H * 0.36f;
        face.anchoredPosition = new Vector2(0f, faceY);
        face.sizeDelta = Vector2.zero;   // нулевой размер: части лица (якорь снизу по центру) считаются от центра лица
        float ew = W * 0.2f, eh = W * 0.175f;
        eyeL.sizeDelta = eyeR.sizeDelta = new Vector2(ew, eh);
        pupilL.sizeDelta = pupilR.sizeDelta = Vector2.one * W * 0.125f;
        lidL.sizeDelta = lidR.sizeDelta = new Vector2(ew * 1.06f, 0f);
        Size(browL, -W * 0.14f, W * 0.15f, W * 0.15f, W * 0.065f);
        Size(browR, W * 0.14f, W * 0.15f, W * 0.15f, W * 0.065f);
        Size(cheekL, -W * 0.26f, -W * 0.1f, W * 0.16f, W * 0.09f);
        Size(cheekR, W * 0.26f, -W * 0.1f, W * 0.16f, W * 0.09f);
        Size(mouthA, 0f, -W * 0.15f, W * 0.22f, W * 0.16f);
        Size(mouthB, 0f, -W * 0.15f, W * 0.22f, W * 0.16f);

        // Язычки: по бокам вверху и у макушки, чуть наклонены наружу
        for (int i = 0; i < MaxTongues; i++)
        {
            bool on = i < tongueCount;
            tongueImgs[i].enabled = on;
            if (!on) continue;
            int side = i % 2 == 0 ? -1 : 1;
            int tier = i / 2;
            float x = side * W * (0.36f - tier * 0.08f), y = H * (0.5f + tier * 0.14f);
            if (i == 4) { x = W * 0.08f; y = H * 0.78f; }
            float rot = i == 4 ? -6f : -side * (18f + tier * 6f);
            tongueBase[i] = new Vector3(x, y, rot);
            tongues[i].anchoredPosition = new Vector2(x, y);
            tongues[i].sizeDelta = new Vector2(W * (0.3f - tier * 0.03f), W * (0.52f - tier * 0.05f));
            tongues[i].localRotation = Quaternion.Euler(0f, 0f, rot);
        }

        // Свет, тень, отражение
        glowBase = 0.3f + glowBoost * 0.5f + st * 0.03f;
        glowImg.enabled = v.flameGlow;
        Size(glowImg, 0f, H * 0.45f, W * 2.9f, W * 2.9f);
        shadowImg.enabled = v.flameShadow;
        Size(shadowImg, 0f, W * 0.02f, W * 1.45f, W * 0.32f);
        reflectImg.enabled = v.flameReflection && hi;
        Size(reflectImg, 0f, -W * 0.02f, W * 1.9f, W * 0.5f);
        Size(flashImg, 0f, H * 0.45f, W * 3.4f, W * 3.4f);

        // Аксессуары стадии
        logs.enabled = accessory == 1 || accessory == 2;
        Size(logs, 0f, -W * 0.01f, W * 1.2f, W * 0.34f);   // под основанием: верх поленьев ниже рта
        beamImg.enabled = accessory == 2;
        Size(beamImg, W * 0.1f, H * 0.72f, W * 2.6f, W * 0.6f);
        haloImg.enabled = accessory >= 3;
        Size(haloImg, 0f, H * 1.04f, W * 0.86f, W * 0.26f);   // нимб — сплюснутое кольцо над макушкой
        raysImg.enabled = accessory == 4;
        Size(raysImg, 0f, H * 0.52f, W * 2.2f, W * 2.2f);
        starAImg.enabled = starBImg.enabled = accessory == 3;
        starA.sizeDelta = starB.sizeDelta = Vector2.one * W * 0.22f;
        float rate = Get(v.flameSparks, st);
        sparks.enabled = v.flameSparksOn && hi && rate > 0f;
        sparks.emissionRate = rate;
        sparks.color = Color.Lerp(k, Color.white, 0.4f);
        var srt = sparks.rectTransform;
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0f); srt.pivot = new Vector2(0.5f, 0f);
        srt.anchoredPosition = Vector2.zero; srt.sizeDelta = new Vector2(W * 1.4f, H * 1.9f);
        zzz.sizeDelta = Vector2.one * W * 0.4f;

        // Облик садится по силуэту стадии: шапки — на макушку, очки — на глаза, шарф и цепочка — под рот
        if (outfit >= 0 && outfit < c.outfits.Count)
        {
            RetOutfit ro = c.outfits[outfit];
            outfitImg.sprite = RetUi.Art(ro.sprite);
            outfitImg.enabled = outfitImg.sprite != null;
            float off = ro.offset.y;
            float y = off >= 0.4f ? H * (0.8f + (off - 0.42f) * 0.6f) : off >= 0.28f ? H * 0.58f : off >= 0.1f ? faceY + W * 0.02f : faceY - W * 0.3f;
            float size = W * ro.size * (off >= 0.1f && off < 0.4f ? 1.25f : 1.15f);
            Size(outfitImg, W * ro.offset.x, y, size, size);
        }
        else outfitImg.enabled = false;
        if (outfitImg.enabled && outfit >= 0 && c.outfits[outfit].offset.y >= 0.4f) haloImg.enabled = false;   // шапка или нимб-облик на макушке — без второго нимба
        ApplyColors();
    }

    private static float Get(float[] a, int i, float fallback = 0f) => a != null && a.Length > 0 ? a[Mathf.Clamp(i, 0, a.Length - 1)] : fallback;
    private static float Get(int[] a, int i) => a != null && a.Length > 0 ? a[Mathf.Clamp(i, 0, a.Length - 1)] : 0f;
    private static Color Pick(Color[] a, int i, Color fallback) => a != null && a.Length > 0 ? a[Mathf.Clamp(i, 0, a.Length - 1)] : fallback;

    private void ApplyColors()
    {
        outer.color = cOuter; mid.color = cMid; core.color = cCore;
        for (int i = 0; i < MaxTongues; i++) tongueImgs[i].color = i % 2 == 0 ? cOuter : Color.Lerp(cOuter, cMid, 0.5f);
        Color lidC = Color.Lerp(cMid, cCore, 0.55f);
        lidLImg.color = lidRImg.color = lidC;
        Color g = cGlow; g.a = glowNow; glowImg.color = g;
        Color r = cGlow; r.a = 0.35f; reflectImg.color = r;
    }

    /// <summary>Базовое настроение (ночью — сонный).</summary>
    public void SetBase(FlameMood m) { baseMood = m; if (moodUntil < 0f) SetMoodNow(m); }

    /// <summary>Настроение на время (потом — базовое).</summary>
    public void SetMood(FlameMood m, float seconds)
    {
        if (!V.emotions && m != FlameMood.Idle) return;
        SetMoodNow(m);
        moodUntil = seconds > 0f ? Time.unscaledTime + seconds : -1f;
    }

    private void SetMoodNow(FlameMood m) { mood = m; moodStart = Time.unscaledTime; }

    public void Hop() => hop = 1f;
    public void Tap() { Hop(); SetMood(FlameMood.Happy, 1.2f); Burst(4); }
    public void Eat() { Hop(); SetMood(FlameMood.Eat, 1.1f); Burst(5); flashAt = Time.unscaledTime - 0.25f; }
    public void Celebrate() { SetMood(FlameMood.Dance, 2.6f); Burst(8); Flash(); }
    public void Surprise() { SetMood(FlameMood.Surprised, 1.3f); Burst(4); }
    public void Warm(bool on) { if (on) SetMood(FlameMood.Warm, -1f); else if (mood == FlameMood.Warm) { moodUntil = 0.01f; } }
    /// <summary>Вспышка (рост стадии, большое событие): яркий ореол гаснет за полсекунды.</summary>
    public void Flash() => flashAt = Time.unscaledTime;

    /// <summary>Взгляд: направление в долях (−1..1).</summary>
    public void LookAt(Vector2 dir, float seconds = 1.5f) { lookTarget = Vector2.ClampMagnitude(dir, 1f); nextLookAt = Time.unscaledTime + seconds; }

    /// <summary>Пауза потока искр (поверх открыто окно — на экране не больше 30 частиц).</summary>
    public void PauseSparks(bool pause) { if (sparks != null) sparks.emissionRate = pause ? 0f : Get(V.flameSparks, stageIndex); }

    public void Burst(int n)
    {
        if (sparks == null || !hi || !V.flameSparksOn) return;
        if (!sparks.enabled) sparks.enabled = true;
        sparks.Burst(n, new Vector2(0f, H * 0.6f));
    }

    private void Update()
    {
        float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
        HearthVisualSettings hv = V;
        if (moodUntil > 0f && t >= moodUntil) { moodUntil = -1f; SetMoodNow(baseMood); }
        float mt = t - moodStart;
        bool anim = hv.flameTongueMotion && hi;

        // Цвет: плавный переход
        if (colorAt >= 0f)
        {
            float k = Mathf.Clamp01((t - colorAt) / Mathf.Max(0.05f, hv.colorBlendSeconds));
            cOuter = Color.Lerp(fOuter, tOuter, k); cMid = Color.Lerp(fMid, tMid, k); cCore = Color.Lerp(fCore, tCore, k); cGlow = Color.Lerp(fGlow, tGlow, k);
            if (k >= 1f) colorAt = -1f;
            ApplyColors();
        }

        // Тело: дыхание squash/stretch от нижней точки, покачивание, подпрыг, движение по настроению
        float breathe = Mathf.Sin(t * (mood == FlameMood.Sleepy ? 1.3f : 2.6f)) * hv.breathe * (mood == FlameMood.Sleepy ? 1.6f : 1f);
        hop = Mathf.Max(0f, hop - dt * 2.5f);
        float jump = Mathf.Sin((1f - hop) * Mathf.PI) * hop;
        float rot = Mathf.Sin(t * 1.7f) * hv.sway, sx = 1f, sy = 1f, y = jump * W * 0.3f;
        switch (mood)
        {
            case FlameMood.Dance:
                rot = Mathf.Sin(t * 9f) * 10f;
                y += Mathf.Abs(Mathf.Sin(t * 9f)) * W * 0.08f;
                sx = 1f + Mathf.Sin(t * 18f) * 0.04f; sy = 2f - sx;
                break;
            case FlameMood.Surprised:
                float ks = Mathf.Clamp01(mt * 6f);
                sx = 1f - 0.08f * Mathf.Sin(ks * Mathf.PI); sy = 1f + 0.14f * Mathf.Sin(ks * Mathf.PI);
                break;
            case FlameMood.Eat:
                sx = 1f + Mathf.Abs(Mathf.Sin(mt * 14f)) * 0.06f; sy = 1f - Mathf.Abs(Mathf.Sin(mt * 14f)) * 0.05f;
                break;
            case FlameMood.Warm:
                sx = 1.04f; sy = 1.04f;
                break;
            case FlameMood.Sleepy:
                rot = Mathf.Sin(t * 0.8f) * 4f;
                break;
        }
        body.localScale = new Vector3(sx * (1f - breathe * 0.6f + jump * 0.05f), sy * (1f + breathe - jump * 0.04f), 1f);
        body.localRotation = Quaternion.Euler(0f, 0f, rot);
        body.anchoredPosition = new Vector2(0f, y);
        if (shadowImg.enabled) { float k = 1f - jump * 0.35f; shadowImg.rectTransform.localScale = new Vector3(k, k, 1f); }

        // Живой огонь: середина и ядро дрожат в своём ритме, язычки качаются и вытягиваются
        if (anim)
        {
            mid.rectTransform.localScale = new Vector3(1f + 0.025f * Mathf.Sin(t * 5.1f), 1f + 0.04f * Mathf.Sin(t * 6.3f + 1f), 1f);
            core.rectTransform.localScale = new Vector3(1f + 0.03f * Mathf.Sin(t * 7.7f + 2f), 1f + 0.05f * Mathf.Sin(t * 8.9f), 1f);
            for (int i = 0; i < tongueCount; i++)
            {
                Vector3 b = tongueBase[i];
                float ph = t * (4.2f + i * 0.7f) + i * 1.9f;
                tongues[i].localRotation = Quaternion.Euler(0f, 0f, b.z + Mathf.Sin(ph) * 7f);
                float stretch = 1f + 0.12f * Mathf.Sin(ph * 1.3f) * Mathf.Sin(ph * 0.7f + 1f);
                tongues[i].localScale = new Vector3(1f, stretch, 1f);
            }
        }

        // Свечение и вспышка
        float glowTarget = glowBase * (mood == FlameMood.Warm ? 1.7f : mood == FlameMood.Dance || mood == FlameMood.Eat ? 1.35f : mood == FlameMood.Sleepy ? 0.7f : 1f);
        glowNow = Mathf.Lerp(glowNow, glowTarget, dt * 4f);
        if (glowImg.enabled) { Color gc = cGlow; gc.a = glowNow * (0.88f + 0.12f * Mathf.Sin(t * 5.3f) * Mathf.Sin(t * 2.1f)); glowImg.color = gc; }
        float fl = Mathf.Clamp01(1f - (t - flashAt) / 0.5f);
        if (fl > 0f || flashImg.color.a > 0f) { Color fc = Color.Lerp(cCore, Color.white, 0.5f); fc.a = fl * 0.85f; flashImg.color = fc; }

        // Аксессуары
        if (raysImg.enabled && hi) accRays.localRotation = Quaternion.Euler(0f, 0f, t * 12f);
        if (beamImg.enabled && hi) accBeam.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.6f) * 35f);
        if (starAImg.enabled)
        {
            float a = t * 1.2f;
            starA.anchoredPosition = new Vector2(Mathf.Cos(a) * W * 0.7f, H * 0.62f + Mathf.Sin(a) * W * 0.25f);
            starB.anchoredPosition = new Vector2(Mathf.Cos(a + Mathf.PI) * W * 0.7f, H * 0.62f + Mathf.Sin(a + Mathf.PI) * W * 0.25f);
        }

        // Взгляд: сам иногда оглядывается
        if (hv.lookAround && t >= nextLookAt && mood != FlameMood.Sleepy)
        {
            lookTarget = Random.value < 0.5f ? Vector2.zero : new Vector2(Random.Range(-1f, 1f), Random.Range(-0.4f, 0.5f));
            nextLookAt = t + Random.Range(1.5f, 4f);
        }
        look = Vector2.Lerp(look, lookTarget, dt * 6f);
        float ex = W * 0.14f, ey = W * 0.02f;
        Vector2 off = look * W * 0.02f;
        eyeL.anchoredPosition = new Vector2(-ex, ey) + off;
        eyeR.anchoredPosition = new Vector2(ex, ey) + off;
        Vector2 pOff = look * W * 0.035f;
        pupilL.anchoredPosition = pupilR.anchoredPosition = pOff;

        // Веки: моргание и настроение (плавно)
        if (t >= blinkAt) { blinkUntil = t + 0.13f; blinkAt = t + Random.Range(2.5f, 5f); }
        bool blink = t < blinkUntil;
        lidTarget = mood == FlameMood.Sleepy || blink ? 1f : mood == FlameMood.Warm ? 0.55f : mood == FlameMood.Happy || mood == FlameMood.Dance ? 0.32f
                  : mood == FlameMood.Eat ? 0.22f : mood == FlameMood.Surprised ? 0f : 0.12f;
        lid = Mathf.MoveTowards(lid, lidTarget, dt * (blink ? 16f : 6f));
        float eh = eyeL.sizeDelta.y;
        lidL.sizeDelta = lidR.sizeDelta = new Vector2(eyeL.sizeDelta.x * 1.06f, eh * lid * 1.04f);
        float es = mood == FlameMood.Surprised ? 1.12f : 1f;
        eyeL.localScale = eyeR.localScale = Vector3.Lerp(eyeL.localScale, new Vector3(es, es, 1f), dt * 10f);

        // Брови: подняты от удивления и любопытства, опущены во сне
        float bTarget = mood == FlameMood.Surprised ? 14f : mood == FlameMood.Curious ? 8f : mood == FlameMood.Sleepy ? -6f : mood == FlameMood.Happy || mood == FlameMood.Dance ? 6f : 0f;
        float byTarget = mood == FlameMood.Surprised ? W * 0.05f : mood == FlameMood.Sleepy ? -W * 0.02f : mood == FlameMood.Curious ? W * 0.025f : 0f;
        browAngle = Mathf.Lerp(browAngle, bTarget, dt * 8f);
        browY = Mathf.Lerp(browY, byTarget, dt * 8f);
        browL.anchoredPosition = new Vector2(-W * 0.14f, W * 0.15f + browY) + off;
        browR.anchoredPosition = new Vector2(W * 0.14f, W * 0.15f + browY + (mood == FlameMood.Curious ? W * 0.02f : 0f)) + off;
        browL.localRotation = Quaternion.Euler(0f, 0f, browAngle);
        browR.localRotation = Quaternion.Euler(0f, 180f, browAngle);   // зеркально левой

        // Рот: смена через плавное перетекание двух картинок
        Sprite mouth = mood == FlameMood.Happy || mood == FlameMood.Dance ? mJoy
                     : mood == FlameMood.Surprised || mood == FlameMood.Curious ? mO
                     : mood == FlameMood.Sleepy ? mSleep
                     : mood == FlameMood.Eat ? ((int)(mt * 7f) % 2 == 0 ? mJoy : mO) : mSmile;
        if (mouthA.sprite != mouth) { mouthB.sprite = mouthA.sprite; mouthA.sprite = mouth; mouthFade = mood == FlameMood.Eat ? 1f : 0f; }
        if (mouthFade < 1f)
        {
            mouthFade = Mathf.MoveTowards(mouthFade, 1f, dt * 8f);
            mouthA.color = new Color(1f, 1f, 1f, mouthFade);
            mouthB.color = new Color(1f, 1f, 1f, 1f - mouthFade);
        }
        else if (mouthB.color.a > 0f) { mouthA.color = Color.white; mouthB.color = new Color(1f, 1f, 1f, 0f); }
        mouthA.rectTransform.anchoredPosition = mouthB.rectTransform.anchoredPosition = new Vector2(0f, -W * 0.15f) + off * 0.6f;
        float blushA = mood == FlameMood.Sleepy ? 0.25f : mood == FlameMood.Happy || mood == FlameMood.Dance || mood == FlameMood.Warm ? 1f : 0.7f;
        Color cc = cheekL.color; cc.a = Mathf.Lerp(cc.a, blushA, dt * 5f); cheekL.color = cheekR.color = cc;

        // Сон: «з-з-з»
        bool sleep = mood == FlameMood.Sleepy;
        if (zzzImg.enabled != sleep) zzzImg.enabled = sleep;
        if (sleep)
        {
            zzz.anchoredPosition = new Vector2(W * 0.45f + Mathf.Sin(t * 1.2f) * W * 0.05f, H * 0.95f + Mathf.Repeat(t * 0.3f, 1f) * W * 0.25f);
            Color zc = zzzImg.color; zc.a = 1f - Mathf.Repeat(t * 0.3f, 1f); zzzImg.color = zc;
        }
    }
}
