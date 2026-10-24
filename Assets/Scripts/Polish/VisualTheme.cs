using TMPro;
using UnityEngine;

/// <summary>
/// Единая визуальная тема и «сок» игры. Один ассет: Assets/Resources/VisualTheme.asset.
/// Все длительности, амплитуды и флаги эффектов крутятся здесь, в инспекторе.
/// </summary>
[CreateAssetMenu(fileName = "VisualTheme", menuName = "Match & Flame/Visual Theme")]
public class VisualTheme : ScriptableObject
{
    private const string ResourcePath = "VisualTheme";
    private static VisualTheme current;

    /// <summary>Тема из Resources; если ассета нет — значения по умолчанию из этого файла.</summary>
    public static VisualTheme Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<VisualTheme>(ResourcePath);
                if (current == null) current = CreateInstance<VisualTheme>();
            }
            return current;
        }
    }

    [Header("Палитра (снята с арта игры, см. DESIGN_PROPOSAL.md)")]
    public Color charColor = Hex("2A0F09");
    public Color wood = Hex("5F3022");
    public Color woodLight = Hex("8F4F2D");
    public Color cream = Hex("F2CF80");
    public Color ember = Hex("E06A1F");
    public Color flame = Hex("FFB547");
    public Color gold = Hex("FFD34D");
    public Color matchHead = Hex("A3241A");

    [Header("Шрифт новых текстов (с кириллицей)")]
    public TMP_FontAsset font;

    [Header("Кнопки: сжатие и отскок")]
    public bool buttonJuice = true;
    [Range(0.7f, 1f)] public float pressScale = 0.92f;
    public float pressDuration = 0.06f;
    [Tooltip("Сила «пружины» при отпускании (параметр easeOutBack, 0 — без перелёта)")]
    public float releaseOvershoot = 1.6f;
    public float releaseDuration = 0.18f;

    [Header("Панели: появление")]
    public bool panelAnimation = true;
    public float panelInDuration = 0.22f;
    [Range(0.5f, 1f)] public float panelStartScale = 0.9f;

    [Header("Пульсация (кнопка Play, текущий уровень)")]
    public float pulseAmplitude = 0.05f;
    public float pulsePeriod = 1.6f;

    [Header("Раскладка UI")]
    [Tooltip("Запас снизу под рекламный баннер в меню и выборе уровня, dp (адаптивный баннер — до 100 dp)")]
    public float bannerReserveDp = 100f;
    [Tooltip("Дополнительный зазор над баннером, dp")]
    public float bannerGapDp = 8f;

    [Header("Выбор уровня")]
    [Tooltip("Блокировка уровней: следующий открывается после 1★ на предыдущем. По умолчанию выключено.")]
    public bool levelLock = false;
    [Tooltip("Задержка между появлением соседних карточек")]
    public float cardCascadeDelay = 0.04f;
    public float cardAppearDuration = 0.3f;
    [Range(0.3f, 1f)] public float lockedCardBrightness = 0.45f;

    [Header("Правила: звёзды по доле сгоревших спичек (H-03)")]
    [Range(0f, 1f)] public float twoStarsShare = 0.8f;
    [Tooltip("Меньше этой доли — панель «Почти получилось» без звёзд и монет")]
    [Range(0f, 1f)] public float oneStarShare = 0.6f;

    [Header("Правила: поле и ввод (M-06, M-07)")]
    [Tooltip("Отступ от края экрана, за который спичку не вытащить (мировые единицы)")]
    public float fieldMargin = 0.25f;

    [Header("Онбординг (M-10)")]
    public bool tutorials = true;
    public float tutorialLoopDuration = 1.8f;

    [Header("Подсказка за рекламу")]
    public bool hints = true;
    public float hintDuration = 3f;
    [Range(0f, 1f)] public float hintAlpha = 0.5f;

    /// <summary>Звёзды по доле сгоревших; 0 — «почти получилось».</summary>
    public int StarsFor(int burnt, int total)
    {
        if (total <= 0) return 3;
        if (burnt >= total) return 3;
        float share = (float)burnt / total;
        return share >= twoStarsShare ? 2 : share >= oneStarShare ? 1 : 0;
    }

    [Header("Игра: поджог")]
    public bool igniteBurst = true;
    [Tooltip("«Рывок» масштаба пламени при поджоге (саму спичку не масштабируем: от неё считается распространение огня)")]
    public bool firePunch = true;
    public float firePunchScale = 1.45f;
    public float firePunchDuration = 0.16f;

    [Header("Игра: горение и сгорание")]
    public bool burningSparks = true;
    public bool smoke = true;
    public bool ashOnBurnout = true;
    [Tooltip("Во что темнеет сгоревшая спичка (умножается на её цвет)")]
    public Color burntTint = Hex("8C8580");
    public float burntDarkenDuration = 0.5f;

    [Header("Игра: свет огня (Light2D, без теней)")]
    public bool fireLightFlicker = true;
    [Range(0f, 0.8f)] public float fireLightFlickerAmount = 0.25f;
    public float fireLightFlickerSpeed = 6f;
    [Tooltip("Сколько огней одновременно светят; остальные горят без Light2D")]
    [Range(0, 16)] public int maxFireLights = 4;

    [Header("Игра: «мелодия» цепной реакции")]
    public bool chainPitch = true;
    [Tooltip("Насколько выше звучит каждая следующая спичка в цепи")]
    public float chainPitchStep = 0.04f;
    public float chainPitchMax = 1.6f;

    [Header("Игра: тряска камеры")]
    public bool cameraShake = true;
    public float shakeAmplitude = 0.12f;
    public float shakeDuration = 0.35f;

    [Header("Игра: победа")]
    public bool victoryConfetti = true;
    public int confettiCount = 80;
    [Tooltip("Звёзды «пружинят» и звучат в моменты, когда анимация доходит до 1/2/3 звезды")]
    public bool starPop = true;
    public bool coinTick = true;
    public float coinTickDuration = 0.6f;

    [Header("Игра: золотые спички")]
    public bool goldenShimmer = true;
    public float goldenPulseSpeed = 3f;
    [Range(0f, 1f)] public float goldenPulseAmount = 0.35f;
    public bool coinFly = true;
    public float coinFlyDuration = 0.65f;

    [Header("Игра: перетаскивание")]
    public bool dragTilt = true;
    public float dragTiltMaxAngle = 12f;
    public float dragTiltResponse = 12f;
    public bool slotMagnet = true;
    [Tooltip("С какого расстояния (мировые единицы) спичку тянет к слоту")]
    public float slotMagnetRadius = 1.1f;
    [Range(0f, 1f)] public float slotMagnetStrength = 0.35f;
    public bool slotHighlight = true;
    public float returnDuration = 0.18f;

    [Header("Игра: фон уровня")]
    public bool levelAmbience = true;
    [Range(0f, 1f)] public float vignetteAlpha = 0.55f;
    [Range(0f, 1f)] public float warmGlowAlpha = 0.14f;
    public bool ambientEmbers = true;

    [Header("Игра: подложка поля")]
    [Tooltip("Мягкая тёмная подложка под фигурой уровня (PlayfieldBacking)")]
    public bool playfieldBacking = true;
    [Range(0f, 1f)] public float playfieldBackingAlpha = 0.3f;
    public float playfieldBackingPadding = 0.6f;
    [Tooltip("Спрайт 9-slice (ui_shadow); пусто — мягкое пятно")]
    public Sprite playfieldBackingSprite;

    [Header("Игра: кадр уровня")]
    [Tooltip("Сколько единиц Canvas под верхним краем безопасной зоны занимает HUD (отступ 20 + панель 140 + зазор)")]
    public float levelHudReserve = 170f;
    [Tooltip("Запас снизу над жестовой панелью, dp")]
    public float levelBottomReserveDp = 24f;
    [Tooltip("Зазор между содержимым уровня и HUD/краем, мировые единицы")]
    public float levelVerticalMargin = 0.12f;
    [Tooltip("Подложка под подсказками уровня: цвет и запас вокруг текста (мировые единицы)")]
    public Color hintBackingColor = new Color(0.165f, 0.06f, 0.035f, 0.72f);
    public Vector2 hintBackingPadding = new Vector2(0.28f, 0.16f);

    [Header("Переходы между сценами: двери (SceneTransition)")]
    [Tooltip("Двери закрываются перед загрузкой сцены и открываются после неё. Выкл — сцены меняются сразу, как раньше")]
    public bool sceneDoors = true;
    [Tooltip("Спрайт створки (доски); правая — зеркальная копия")]
    public Sprite doorSprite;
    [Range(0.15f, 1.2f)] public float doorCloseSeconds = 0.4f;
    [Range(0.15f, 1.2f)] public float doorOpenSeconds = 0.45f;
    [Tooltip("Насколько закрытые створки заходят друг за друга, доля ширины экрана (3–5%)")]
    [Range(0.02f, 0.08f)] public float doorOverlap = 0.04f;
    [Tooltip("Накладка на стыке: ширина (доля экрана) и цвет; светлая кромка — по её краю")]
    [Range(0f, 0.06f)] public float doorSeamWidth = 0.022f;
    public Color doorSeamColor = new Color(0.22f, 0.11f, 0.05f, 1f);
    public Color doorSeamEdgeColor = new Color(0.62f, 0.38f, 0.2f, 1f);
    [Tooltip("Тень от верхней створки на нижнюю у стыка")]
    [Range(0f, 1f)] public float doorShadowAlpha = 0.35f;
    [Tooltip("Сколько двери держатся закрытыми после загрузки сцены, секунд (первый кадр новой сцены)")]
    [Range(0f, 0.5f)] public float doorHoldSeconds = 0.08f;

    [Header("Вибрация (Android). Игрок выключает её в настройках")]
    public bool vibration = true;
    public int vibrateIgniteMs = 15;
    [Range(1, 255)] public int vibrateIgniteAmplitude = 60;
    public int vibrateSuccessMs = 40;
    [Range(1, 255)] public int vibrateSuccessAmplitude = 140;
    public int vibrateErrorMs = 70;
    [Range(1, 255)] public int vibrateErrorAmplitude = 90;

    [System.Serializable]
    public class FlameTint
    {
        public string nameEN, nameRU;
        [Tooltip("На каком из базовых скинов основан (0 оранжевый, 1 зелёный, 2 синий, 3 фиолетовый)")]
        public int baseSkin;
        [Tooltip("Цвет умножается на кадры огня (в меню — на всю картинку превью)")]
        public Color color = Color.white;
        public int price;
    }

    [Header("Оттеночные скины огня (без новых текстур, идут после базовых в магазине)")]
    public FlameTint[] tintSkins =
    {
        new FlameTint { nameEN = "Golden", nameRU = "Золотой", baseSkin = 0, color = Hex("FFE58A"), price = 8 },
        new FlameTint { nameEN = "Ice", nameRU = "Ледяной", baseSkin = 2, color = Hex("C9F4FF"), price = 12 },
        new FlameTint { nameEN = "Ruby", nameRU = "Рубиновый", baseSkin = 0, color = Hex("FF7A7A"), price = 15 },
    };

    /// <summary>Оттенок для индекса скина или null, если это базовый скин.</summary>
    public FlameTint GetTint(int skinIndex, int baseSkinCount)
    {
        int i = skinIndex - baseSkinCount;
        return tintSkins != null && i >= 0 && i < tintSkins.Length ? tintSkins[i] : null;
    }

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }
}
