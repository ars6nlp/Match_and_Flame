using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Комната Очага. Квадрат 1000×1000 единиц (свой вложенный Canvas — анимации не перестраивают остальной интерфейс):
/// окно со сменой времени суток, декор по слоям (стена, полка камина, пол под огоньком, передний план), камин, огонёк,
/// праздничный декор, жизнь предметов (маятник, мерцание лампы, растение, пар чайника, шторы), появление купленного декора.
/// Задник (стена из досок, панель, кирпичный дымоход, пол, луч из окна с пылинками) — на весь экран: комната не «в рамке».
/// Поверх — свет камина (аддитивный), виньетка и ценники незанятых мест (в слое без масштаба: кегль не мельчает).
/// Положение задника и ценников пересчитывает <see cref="Sync"/> (вызывает экран при смене размера). Жизнь — 30 раз в секунду,
/// без выделений памяти.
/// </summary>
public class HearthScene : MonoBehaviour
{
    public const float Size = 1000f;
    public static readonly Vector2 MantelLine = new Vector2(0.5f, 0.518f);
    /// <summary>Проём камина в долях комнаты (по рисунку hearth_fireplace): x 0.344–0.656, низ 0.081, верх арки 0.434.</summary>
    public static readonly Rect Opening = Rect.MinMaxRect(0.344f, 0.081f, 0.656f, 0.434f);
    public const float FloorLine = 0.22f, FlameBase = 0.088f;

    public RetFlame Flame { get; private set; }
    public RectTransform Root { get; private set; }
    public Action<int> OnSlotTap;

    private RectTransform backdrop, overlay;
    private Image wall, wainscot, floor, skirting, chimney, beam, sky, sun, moon, fireLight, pool, vignette, eventTint, seasonal;
    private UIParticleField dust;
    private Image[] stars;
    private Image[] decorImgs, decorGhosts, decorShadows;
    private RectTransform[] tags;
    private Image[] tagPlaques, tagGlows, tagIcons, tagSparks;
    private TextMeshProUGUI[] tagNums;
    private bool[] tagCan;
    private RectTransform[] lifeParts;
    private Image lampGlow;
    private RectTransform[] steam;
    private Image[] steamImgs;
    private UIParticleField burst;
    private int[] appear;
    private float[] appearAt;
    private float nextLife, lightBase = 0.35f, beamAlpha;
    private Color roomColor = Color.white;
    private static Material additive;
    private bool ambientPaused;

    private static RetentionSettings C => RetentionSettings.Current;
    private static HearthVisualSettings V => HearthVisualSettings.Current;

    /// <summary>Аддитивный материал свечения (Resources/HearthAdditive); нет — обычное смешивание.</summary>
    public static Material Additive
    {
        get
        {
            if (additive == null) additive = Resources.Load<Material>("HearthAdditive");
            return additive;
        }
    }

    /// <summary>parent — место сцены; backdrop — слой задника на весь экран (под сценой); overlay — слой над сценой (свет, ценники).</summary>
    public static HearthScene Create(Transform parent, RectTransform backdrop, RectTransform overlay)
    {
        RectTransform rt = RetUi.At(parent, "HearthScene", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Size, Size));
        rt.gameObject.AddComponent<Canvas>();          // вложенный Canvas: перестройка только комнаты
        rt.gameObject.AddComponent<GraphicRaycaster>();
        var s = rt.gameObject.AddComponent<HearthScene>();
        s.Root = rt;
        s.backdrop = backdrop;
        s.overlay = overlay;
        s.Build();
        return s;
    }

    private Image Layer(string name, string sprite, float x, float y, float w, float h, Color color, Vector2? pivot = null)
    {
        RectTransform r = RetUi.Rect(Root, name, new Vector2(x, y), new Vector2(x, y), pivot ?? new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
        var img = r.gameObject.AddComponent<Image>();
        img.sprite = RetUi.Art(sprite); img.color = color; img.raycastTarget = false; img.preserveAspect = true;
        return img;
    }

    private static Image Tiled(Transform parent, string name, string sprite)
    {
        Image img = RetUi.Rect(parent, name, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        img.sprite = RetUi.Art(sprite); img.type = Image.Type.Tiled; img.raycastTarget = false;
        return img;
    }

    private void Build()
    {
        // Задник на весь экран: стена, дымоход над камином, панель, плинтус, пол, луч из окна с пылинками
        wall = RetUi.Fill(backdrop, "Wall").gameObject.AddComponent<Image>();
        wall.sprite = RetUi.Art("rm_wall"); wall.type = Image.Type.Tiled; wall.raycastTarget = false;
        chimney = Tiled(backdrop, "Chimney", "rm_brick");
        wainscot = Tiled(backdrop, "Wainscot", "rm_wainscot");
        floor = Tiled(backdrop, "Floor", "rm_floor");
        skirting = RetUi.Rect(backdrop, "Skirting", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        skirting.color = new Color(0.3f, 0.16f, 0.09f); skirting.raycastTarget = false;
        beam = RetUi.Rect(backdrop, "WindowBeam", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        beam.sprite = RetUi.Art("rm_beam"); beam.raycastTarget = false; beam.material = Additive;
        dust = RetUi.Fill(beam.transform, "Dust").gameObject.AddComponent<UIParticleField>();
        dust.raycastTarget = false; dust.maxParticles = 8; dust.emissionRate = 1.6f; dust.lifetime = new Vector2(4f, 7f);
        dust.size = new Vector2(4f, 8f); dust.riseSpeed = new Vector2(6f, 16f); dust.sway = 14f; dust.spawnHeight = new Vector2(0f, 0.9f);
        dust.color = new Color(1f, 0.95f, 0.82f, 0.55f);

        // Окно: небо, солнце/луна, звёзды, рама
        sky = Layer("Sky", "rm_sky", 0.17f, 0.655f, 132f, 148f, Color.white);
        sky.preserveAspect = false;
        stars = new Image[5];
        Vector2[] sp = { new Vector2(-40, 40), new Vector2(30, 55), new Vector2(45, 10), new Vector2(-20, -10), new Vector2(10, 30) };
        for (int i = 0; i < stars.Length; i++)
            stars[i] = RetUi.Img(sky.transform, "Star" + i, RetUi.Art("rm_star"), Color.white, new Vector2(0.5f, 0.5f), sp[i], new Vector2(14f, 14f));
        sun = RetUi.Img(sky.transform, "Sun", RetUi.Art("rm_sun"), Color.white, new Vector2(0.5f, 0.5f), new Vector2(20f, 30f), new Vector2(70f, 70f));
        moon = RetUi.Img(sky.transform, "Moon", RetUi.Art("rm_moon"), Color.white, new Vector2(0.5f, 0.5f), new Vector2(25f, 35f), new Vector2(60f, 60f));
        Layer("Window", "rm_window", 0.17f, 0.66f, 200f, 240f, Color.white);

        int n = C.decor.Count;
        decorImgs = new Image[n]; decorGhosts = new Image[n]; decorShadows = new Image[n]; lifeParts = new RectTransform[n];
        tags = new RectTransform[n]; tagPlaques = new Image[n]; tagGlows = new Image[n]; tagIcons = new Image[n]; tagSparks = new Image[n];
        tagNums = new TextMeshProUGUI[n]; tagCan = new bool[n];
        appear = new int[n]; appearAt = new float[n];
        for (int i = 0; i < n; i++) if (C.decor[i].layer == 0) MakeDecor(i);
        // Камин и полка
        Layer("Fireplace", "hearth_fireplace", 0.5f, 0.06f, 520f, 520f, Color.white, new Vector2(0.5f, 0f));
        for (int i = 0; i < n; i++) if (C.decor[i].layer == 1) MakeDecor(i);
        seasonal = Layer("Seasonal", null, 0.5f, 0.51f, 300f, 150f, Color.white);
        // Пол под огоньком, огонёк, передний план
        for (int i = 0; i < n; i++) if (C.decor[i].layer == 2) MakeDecor(i);
        pool = Layer("LightPool", "rm_pool", 0.5f, 0.11f, 760f, 300f, new Color(1f, 0.75f, 0.45f, 0.35f));
        pool.preserveAspect = false;
        pool.material = Additive;
        Flame = RetFlame.Create(Root, Vector2.zero, Size);
        var frt = Flame.Rect;
        frt.anchorMin = frt.anchorMax = new Vector2(0.5f, FlameBase);
        frt.pivot = new Vector2(0.5f, 0f);
        frt.anchoredPosition = Vector2.zero;
        for (int i = 0; i < n; i++) if (C.decor[i].layer == 3) MakeDecor(i);
        burst = RetUi.Fill(Root, "Burst").gameObject.AddComponent<UIParticleField>();
        burst.raycastTarget = false; burst.emissionRate = 0f; burst.maxParticles = 12;
        burst.material = Additive;

        // Над сценой: свет камина (аддитивный), виньетка, праздничный оттенок, ценники
        fireLight = RetUi.Rect(overlay, "FireLight", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        fireLight.sprite = RetUi.Art("rm_light"); fireLight.raycastTarget = false; fireLight.material = Additive;
        vignette = RetUi.Fill(overlay, "Vignette").gameObject.AddComponent<Image>();
        vignette.sprite = RetUi.Art("rm_vignette"); vignette.raycastTarget = false;
        eventTint = RetUi.Fill(overlay, "EventTint").gameObject.AddComponent<Image>();
        eventTint.raycastTarget = false;
        for (int i = 0; i < n; i++) MakeTag(i);
    }

    private void MakeDecor(int i)
    {
        RetDecor d = C.decor[i];
        float w = Size * d.width;
        Vector2 pivot = d.layer == 1 ? new Vector2(0.5f, 0.06f) : new Vector2(0.5f, 0.5f);
        if (d.layer == 3)
        {
            // Тёплая тень на полу под предметом переднего плана
            decorShadows[i] = Layer("Shadow_" + i, "fl2_shadow", d.slot.x, d.slot.y - d.width * 0.42f, w * 0.95f, w * 0.2f, new Color(1f, 1f, 1f, 0.55f));
            decorShadows[i].preserveAspect = false;
        }
        Image ghost = Layer("Ghost_" + i, d.sprite, d.slot.x, d.slot.y, w, w, new Color(0.05f, 0.02f, 0.01f, 0.32f), pivot);
        ghost.raycastTarget = true;
        int ii = i;
        var trig = ghost.gameObject.AddComponent<SlotTap>();
        trig.onTap = () => OnSlotTap?.Invoke(ii);
        Image img = Layer("Decor_" + i, d.sprite, d.slot.x, d.slot.y, w, w, Color.white, pivot);
        decorGhosts[i] = ghost; decorImgs[i] = img;
        // Жизнь
        RectTransform life = img.rectTransform;
        if (d.life == 1)
        {
            Image p = RetUi.Img(img.transform, "Pendulum", RetUi.Art("rm_pendulum"), Color.white, new Vector2(0.5f, 0.22f), Vector2.zero, new Vector2(w * 0.18f, w * 0.55f));
            p.rectTransform.pivot = new Vector2(0.5f, 1f);
            p.transform.SetAsFirstSibling();
            life = p.rectTransform;
        }
        else if (d.life == 2)
        {
            lampGlow = RetUi.Img(img.transform, "Glow", C.glow, new Color(1f, 0.8f, 0.4f, 0.5f), new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(w * 2.2f, w * 2.2f));
            lampGlow.material = Additive;
            lampGlow.transform.SetAsFirstSibling();
        }
        else if (d.life == 3) life.pivot = new Vector2(0.5f, 0.05f);
        else if (d.life == 4)
        {
            steam = new RectTransform[3]; steamImgs = new Image[3];
            for (int k = 0; k < steam.Length; k++)
            {
                steamImgs[k] = RetUi.Img(img.transform, "Steam" + k, RetUi.Art("rm_steam"), new Color(1f, 1f, 1f, 0.5f), new Vector2(0.62f, 0.8f), Vector2.zero, new Vector2(w * 0.3f, w * 0.3f));
                steam[k] = steamImgs[k].rectTransform;
            }
        }
        lifeParts[i] = life;
    }

    /// <summary>Ценник незанятого места: светлая плашка со значком монеты/уголька и числом; невидимая зона касания ≥48 dp.</summary>
    private void MakeTag(int i)
    {
        RetDecor d = C.decor[i];
        RectTransform t = RetUi.Rect(overlay, "Price_" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(176f, 128f));
        int ii = i;
        RetUi.HitArea(t.gameObject, () => OnSlotTap?.Invoke(ii));
        tagGlows[i] = RetUi.Img(t, "Glow", RetUi.Art("ui_soft"), new Color(1f, 0.72f, 0.3f, 0.7f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(230f, 130f));
        tagGlows[i].preserveAspect = false;
        tagGlows[i].material = Additive;
        tagPlaques[i] = HearthStyle.Sliced(t, "Plaque", "ui_tag", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160f, 68f));
        tagIcons[i] = HearthStyle.Icon(tagPlaques[i].transform, "Icon", d.priceCoins > 0 ? "ic_coin" : "s_ember", new Vector2(0f, 0.5f), new Vector2(34f, 0f), 50f);
        tagNums[i] = HearthStyle.Num(tagPlaques[i].transform, "Value", TextLevel.Caption, V.ink, new Vector2(0f, 0.5f), new Vector2(104f, 2f), new Vector2(100f, 56f));
        tagSparks[i] = RetUi.Img(tagPlaques[i].transform, "Spark", RetUi.Art("rm_star"), Color.white, new Vector2(1f, 1f), new Vector2(-10f, -8f), new Vector2(30f, 30f));
        tagSparks[i].material = Additive;
        tags[i] = t;
    }

    /// <summary>Доля комнаты → мировые координаты (для слоёв без масштаба).</summary>
    public Vector3 RoomToWorld(float x, float y) => Root.TransformPoint(new Vector3((x - 0.5f) * Size, (y - 0.5f) * Size, 0f));

    private Vector2 Local(RectTransform space, float x, float y) => space.InverseTransformPoint(RoomToWorld(x, y));

    /// <summary>Центр ценника в долях комнаты: на силуэте (настенные высокие предметы — чуть выше центра).</summary>
    public static Vector2 TagSpot(RetDecor d)
    {
        float cy = d.layer == 1 ? d.slot.y + d.width * 0.44f : d.slot.y;
        if (d.layer == 0) cy += Mathf.Min(0.06f, d.width * 0.25f);
        return new Vector2(Mathf.Clamp(d.slot.x, 0.1f, 0.9f), Mathf.Clamp(cy, 0.08f, 0.92f));
    }

    /// <summary>Разложить задник, свет и ценники по текущему положению и масштабу комнаты.</summary>
    public void Sync()
    {
        float s = Root.lossyScale.x / Mathf.Max(1e-5f, backdrop.lossyScale.x);   // масштаб комнаты в единицах задника
        Rect br = backdrop.rect;
        Vector2 floorP = Local(backdrop, 0.5f, FloorLine);
        float floorTop = floorP.y - br.yMin;
        Place(floor, br, 0f, br.width, 0f, floorTop);
        Place(wainscot, br, 0f, br.width, floorTop, 0.085f * Size * s);
        Place(skirting, br, 0f, br.width, floorTop - 8f * s, 16f * s);
        Vector2 mantel = Local(backdrop, 0.5f, MantelLine.y);
        float chimneyW = 0.34f * Size * s;
        Place(chimney, br, mantel.x - br.xMin - chimneyW * 0.5f, chimneyW, mantel.y - br.yMin, br.yMax - mantel.y);
        float k = 1f / Mathf.Max(0.2f, s);   // плитки — в масштабе комнаты
        wall.pixelsPerUnitMultiplier = k; floor.pixelsPerUnitMultiplier = k; chimney.pixelsPerUnitMultiplier = k;
        wainscot.pixelsPerUnitMultiplier = RetUi.Art("rm_wainscot") != null ? RetUi.Art("rm_wainscot").rect.height / Mathf.Max(1f, 0.085f * Size * s) : 1f;
        // Луч из окна — вниз вправо, к полу перед камином
        var brt = beam.rectTransform;
        brt.anchoredPosition = Local(backdrop, 0.19f, 0.62f);
        brt.sizeDelta = new Vector2(0.42f * Size * s, 0.78f * Size * s);
        brt.localRotation = Quaternion.Euler(0f, 0f, 32f);
        // Свет камина — на весь экран вокруг проёма
        var frt = fireLight.rectTransform;
        frt.anchoredPosition = Local(overlay, 0.5f, 0.24f);
        frt.sizeDelta = Vector2.one * 1.7f * Size * s;
        for (int i = 0; i < tags.Length; i++)
        {
            Vector2 p = TagSpot(C.decor[i]);
            tags[i].anchoredPosition = Local(overlay, p.x, p.y);
        }
    }

    // Прямоугольник в задникe: x, ширина, низ (от нижнего края), высота
    private static void Place(Graphic g, Rect br, float x, float w, float bottom, float h)
    {
        var rt = g.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(x, bottom);
        rt.sizeDelta = new Vector2(w, Mathf.Max(0f, h));
    }

    /// <summary>Мир → точка слоя overlay (для пузыря и подсказки руки).</summary>
    public RectTransform Overlay => overlay;

    /// <summary>Обновить всё по состоянию (владение, время суток, серия, праздник).</summary>
    public void Refresh(RetentionCore core)
    {
        RetState s = core.s;
        int tod = V.timeOfDay ? HearthVisualSettings.TimeOfDay(HearthVisualSettings.Hour) : 1;
        Color[] skyC = { V.morningSky, V.daySky, V.eveningSky, V.nightSky };
        Color[] roomC = { V.morningRoom, V.dayRoom, V.eveningRoom, V.nightRoom };
        sky.color = skyC[tod];
        sun.enabled = tod <= 1;
        sun.rectTransform.anchoredPosition = tod == 0 ? new Vector2(-25f, -15f) : new Vector2(25f, 35f);
        moon.enabled = tod >= 2;
        for (int i = 0; i < stars.Length; i++) stars[i].enabled = tod == 3 || (tod == 2 && i < 2);
        roomColor = roomC[tod];
        wall.color = wainscot.color = floor.color = chimney.color = roomColor;
        vignette.color = new Color(1f, 1f, 1f, V.sceneLight ? (tod == 3 ? 0.8f : tod == 2 ? 0.62f : 0.42f) : 0f);
        vignette.enabled = V.sceneLight;
        lightBase = V.sceneLight ? 0.16f + core.Glow * V.streakLightBonus * 0.5f + s.stage * 0.01f : 0f;
        fireLight.enabled = pool.enabled = V.sceneLight;
        beamAlpha = !V.sceneLight ? 0f : tod == 1 ? 0.2f : tod == 0 ? 0.24f : tod == 2 ? 0.12f : 0.06f;
        beam.color = tod == 3 ? new Color(0.62f, 0.72f, 1f, beamAlpha) : tod == 2 ? new Color(1f, 0.7f, 0.45f, beamAlpha) : new Color(1f, 0.94f, 0.8f, beamAlpha);
        beam.enabled = beamAlpha > 0f;
        dust.enabled = beam.enabled && tod <= 1 && FxQuality.High;
        int coins = CurrencyManager.GetGoldenMatches();

        for (int i = 0; i < decorImgs.Length; i++)
        {
            RetDecor d = C.decor[i];
            bool own = s.HasDecor(i);
            decorImgs[i].enabled = own;
            if (decorShadows[i] != null) decorShadows[i].enabled = own;
            decorGhosts[i].enabled = !own && d.enabled;
            bool tagOn = !own && d.enabled;
            tags[i].gameObject.SetActive(tagOn);
            if (tagOn)
            {
                bool can = d.priceCoins > 0 ? coins >= d.priceCoins : s.embers >= d.priceEmbers;
                tagCan[i] = can;
                tagNums[i].text = (d.priceCoins > 0 ? d.priceCoins : d.priceEmbers).ToString();
                tagNums[i].color = can ? V.ink : V.lowRed;
                tagPlaques[i].color = can ? Color.white : V.mutedTint;
                tagIcons[i].color = can ? Color.white : new Color(1f, 1f, 1f, 0.75f);
                tagGlows[i].enabled = can && V.sceneLight;
                tagSparks[i].enabled = can && V.uiAnimations;
            }
            // Свет огня на предметах: ближе к камину — теплее и ярче
            float dist = Vector2.Distance(new Vector2(d.slot.x, d.slot.y), new Vector2(0.5f, 0.3f));
            float lit = V.sceneLight ? Mathf.Clamp01(1.05f - dist * 1.1f) : 1f;
            Color dc = Color.Lerp(roomColor * 0.82f, new Color(1f, 0.95f, 0.88f), lit);
            dc.a = 1f;
            decorImgs[i].color = dc;
        }

        // Праздник: оттенок и декор на полке
        RetSeasonal ev = V.seasonalDecor ? core.ActiveSeasonal(core.Today, out _) : null;
        seasonal.enabled = ev != null;
        eventTint.enabled = ev != null;
        if (ev != null)
        {
            seasonal.sprite = RetUi.Art(SeasonalSprite(ev.id));
            seasonal.enabled = seasonal.sprite != null;
            Color tint = ColorUtility.TryParseHtmlString(ev.tint, out Color c) ? c : Color.white;
            tint.a = 0.035f;
            eventTint.color = tint;
        }
    }

    /// <summary>Частицы комнаты (пылинки) — пауза, пока поверх открыто окно: на экране не больше 30 частиц.</summary>
    public void PauseAmbient(bool pause)
    {
        if (dust != null) dust.emissionRate = pause ? 0f : 1.6f;
        if (Flame != null && (pause || pause != ambientPaused)) Flame.PauseSparks(pause);   // пока окно открыто — каждый кадр (Setup мог включить поток)
        ambientPaused = pause;
    }

    public static string SeasonalSprite(string id)
    {
        switch (id)
        {
            case "newyear": return "sea_garland";
            case "halloween": return "sea_pumpkins";
            case "hearts": return "sea_hearts";
            case "spring": return "sea_flowers";
            case "leaffall": return "sea_leaves";
            case "summer": return "sea_sun";
            case "nauryz": return "sea_nauryz";
            case "fireday": return "sea_feather";
            default: return null;
        }
    }

    /// <summary>Купленный предмет «встаёт» в слот: пружинка и искры.</summary>
    public void PlayAppear(int i)
    {
        if (i < 0 || i >= decorImgs.Length) return;
        appear[i] = 1;
        appearAt[i] = Time.unscaledTime;
        if (V.decorAppear && burst != null)
        {
            RetDecor d = C.decor[i];
            burst.Burst(12, new Vector2((d.slot.x - 0.5f) * Size, (d.slot.y - 0.5f) * Size));
        }
    }

    private void Update()
    {
        float t = Time.unscaledTime;
        // Появление (каждый кадр, только пока идёт)
        for (int i = 0; i < appear.Length; i++)
        {
            if (appear[i] == 0) continue;
            float k = (t - appearAt[i]) / 0.55f;
            float s = !V.decorAppear || k >= 1f ? 1f : k < 0.6f ? Mathf.Lerp(0.2f, 1.15f, k / 0.6f) : Mathf.Lerp(1.15f, 1f, (k - 0.6f) / 0.4f);
            decorImgs[i].rectTransform.localScale = new Vector3(s, s, 1f);
            if (k >= 1f) appear[i] = 0;
        }
        if (t < nextLife) return;
        nextLife = t + 1f / 30f;
        // Свет огня: мягкое дыхание
        if (fireLight.enabled)
        {
            float f = 0.88f + 0.12f * Mathf.Sin(t * 3.1f) * Mathf.Sin(t * 1.7f + 1f);
            Color lc = fireLight.color; lc.a = lightBase * f; fireLight.color = lc;
            Color pc = pool.color; pc.a = (lightBase + 0.1f) * f; pool.color = pc;
        }
        if (beam.enabled)
        {
            Color bc = beam.color; bc.a = beamAlpha * (0.9f + 0.1f * Mathf.Sin(t * 0.5f)); beam.color = bc;
        }
        // Ценники «по карману»: мягкое свечение и искорка в углу по очереди
        if (V.uiAnimations)
            for (int i = 0; i < tags.Length; i++)
            {
                if (!tagCan[i] || !tags[i].gameObject.activeSelf) continue;
                float ph = t * 1.4f + i * 1.7f;
                Color gc = tagGlows[i].color; gc.a = 0.45f + 0.25f * Mathf.Sin(ph); tagGlows[i].color = gc;
                float tw = Mathf.Repeat(t * 0.5f + i * 0.37f, 1f);
                Color sc = tagSparks[i].color; sc.a = tw < 0.25f ? Mathf.Sin(tw / 0.25f * Mathf.PI) : 0f; tagSparks[i].color = sc;
                tagSparks[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, tw * 180f);
            }
        if (!V.itemLife) return;
        for (int i = 0; i < lifeParts.Length; i++)
        {
            RectTransform p = lifeParts[i];
            if (p == null || !decorImgs[i].enabled) continue;
            switch (C.decor[i].life)
            {
                case 1: p.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2.6f) * 14f); break;
                case 3: p.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.3f + i) * 2.2f); break;
                case 5: p.localScale = new Vector3(1f + Mathf.Sin(t * 0.8f + i) * 0.012f, 1f, 1f); break;
            }
        }
        if (lampGlow != null) { Color c = lampGlow.color; c.a = 0.4f + 0.12f * Mathf.Sin(t * 7.3f) * Mathf.Sin(t * 2.9f); lampGlow.color = c; }
        if (steam != null)
            for (int k = 0; k < steam.Length; k++)
            {
                float ph = Mathf.Repeat(t * 0.45f + k / 3f, 1f);
                steam[k].anchoredPosition = new Vector2(Mathf.Sin(ph * 6f + k) * 6f, ph * 60f);
                Color sc = steamImgs[k].color; sc.a = 0.45f * (1f - ph) * Mathf.Clamp01(ph * 5f); steamImgs[k].color = sc;
                steam[k].localScale = Vector3.one * (0.6f + ph * 0.8f);
            }
    }

    /// <summary>Тап по силуэту свободного места.</summary>
    private class SlotTap : MonoBehaviour, IPointerClickHandler
    {
        public Action onTap;
        public void OnPointerClick(PointerEventData e) => onTap?.Invoke();
    }
}

/// <summary>Касание огонька: тап — подпрыг, искры и реплика (с антиспамом, наград за тапы нет); удержание — «греет руки».</summary>
public class FlameTouch : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public RetFlame flame;
    public Action onTap, onSpam, onHold;
    private float downAt = -1f, lastTap = -10f;
    private bool holding;
    private int tapsInRow;
    private float rowStart;

    public void OnPointerDown(PointerEventData e) { downAt = Time.unscaledTime; holding = false; }
    public void OnPointerExit(PointerEventData e) { if (holding) flame.Warm(false); downAt = -1f; holding = false; }

    public void OnPointerUp(PointerEventData e)
    {
        if (downAt < 0f) return;
        downAt = -1f;
        if (holding) { holding = false; flame.Warm(false); return; }
        HearthVisualSettings v = HearthVisualSettings.Current;
        if (!v.tapReactions || Time.unscaledTime - lastTap < v.tapCooldown) return;
        if (Time.unscaledTime - rowStart > 3f) { rowStart = Time.unscaledTime; tapsInRow = 0; }
        tapsInRow++;
        lastTap = Time.unscaledTime;
        if (tapsInRow == 6) { flame.Surprise(); onSpam?.Invoke(); return; }
        flame.Tap();
        HearthAudio.Tap();
        onTap?.Invoke();
    }

    private void Update()
    {
        if (downAt < 0f || holding || !HearthVisualSettings.Current.holdWarm) return;
        if (Time.unscaledTime - downAt >= HearthVisualSettings.Current.holdSeconds)
        {
            holding = true;
            flame.Warm(true);
            onHold?.Invoke();
        }
    }
}

/// <summary>Уголёк из лотка: перетащить к огоньку — он «съест» горсть угольков. Один переиспользуемый жетон.</summary>
public class EmberDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public RectTransform token;
    public RetFlame flame;
    public Func<bool> canFeed;
    public Action onFed, onEmpty;
    private Canvas canvas;
    private Vector2 home;
    private float flyBack = -1f;
    private Vector2 flyFrom;
    private bool flyIntoFlame;

    private void Start() { canvas = GetComponentInParent<Canvas>().rootCanvas; token.gameObject.SetActive(false); }

    public void OnBeginDrag(PointerEventData e)
    {
        if (canFeed != null && !canFeed()) { onEmpty?.Invoke(); return; }
        token.gameObject.SetActive(true);
        token.position = e.position;
        flyBack = -1f;
        flame.SetMood(FlameMood.Curious, 3f);
    }

    public void OnDrag(PointerEventData e)
    {
        if (!token.gameObject.activeSelf) return;
        RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)canvas.transform, e.position, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out Vector3 w);
        token.position = w;
        // Огонёк следит взглядом за угольком
        Vector3 d = token.position - flame.Rect.position;
        flame.LookAt(new Vector2(Mathf.Clamp(d.x / 300f, -1f, 1f), Mathf.Clamp(d.y / 300f, -1f, 1f)), 0.5f);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!token.gameObject.activeSelf) return;
        bool over = RectTransformUtility.RectangleContainsScreenPoint(flame.Rect, e.position, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera)
                    || Vector2.Distance(e.position, RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, flame.Rect.position)) < Screen.width * 0.16f;
        flyFrom = token.position;
        flyIntoFlame = over;
        flyBack = Time.unscaledTime;
        if (over) onFed?.Invoke();
    }

    private void Update()
    {
        if (flyBack < 0f) return;
        float k = (Time.unscaledTime - flyBack) / 0.25f;
        Vector3 target = flyIntoFlame ? flame.MouthWorld : ((RectTransform)transform).position;
        token.position = Vector3.Lerp(flyFrom, target, Mathf.Clamp01(k));
        token.localScale = Vector3.one * (flyIntoFlame ? Mathf.Lerp(1f, 0.3f, k) : 1f);
        if (k >= 1f) { flyBack = -1f; token.gameObject.SetActive(false); token.localScale = Vector3.one; }
    }
}
