using System;
using System.Collections.Generic;
using UnityEngine;

public enum FlameMood { Idle, Happy, Curious, Sleepy, Surprised, Dance, Eat, Warm }

public enum LineKind
{
    GreetMorning, GreetDay, GreetEvening, GreetNight, GreetReturn, GreetStreak, Tap, TapSpam, Hold, Feed, FeedEmpty,
    StageUp, Idle, Hint, Tease, Event, DailyDone, Decor, Night
}

[Serializable]
public class FlameLine
{
    public LineKind kind;
    [TextArea(1, 2)] public string ru, en;
}

/// <summary>
/// Вид и характер Очага (Assets/Resources/HearthVisualSettings.asset): каждый эффект выключается флагом; числа анимаций;
/// цвета времени суток; реплики огонька (RU/EN). Тон реплик — тёплый, без упрёков и давления.
/// </summary>
[CreateAssetMenu(menuName = "Match & Flame/Hearth Visual Settings", fileName = "HearthVisualSettings")]
public class HearthVisualSettings : ScriptableObject
{
    private static HearthVisualSettings current;
    public static HearthVisualSettings Current
    {
        get
        {
            if (current == null) current = Resources.Load<HearthVisualSettings>("HearthVisualSettings");
            if (current == null) { current = CreateInstance<HearthVisualSettings>(); FillLines(current); }
            return current;
        }
    }

    [Header("Имя огонька")]
    public string flameNameRU = "Искорка";
    public string flameNameEN = "Sparky";

    [Header("Сцена Очага (флаги)")]
    public bool sceneLight = true, timeOfDay = true, seasonalDecor = true, itemLife = true, decorAppear = true, sceneBreathing = true;

    [Header("Огонёк (флаги)")]
    public bool emotions = true, lookAround = true, tapReactions = true, holdWarm = true, dragFeed = true, lines = true, greeting = true, idleActions = true;
    public bool sounds = true, haptics = true, onboarding = true;

    [Header("Числа")]
    [Tooltip("Через сколько секунд без касаний огонёк делает что-то сам")] public float idleSeconds = 8f;
    [Tooltip("Минимум между реакциями на тап (антиспам; награды за тап нет)")] public float tapCooldown = 0.35f;
    [Tooltip("Минимум между репликами")] public float lineCooldown = 2.5f;
    [Tooltip("Сколько держать палец, чтобы огонёк «грел руки»")] public float holdSeconds = 0.5f;
    [Tooltip("Сколько угольков уходит за одно перетаскивание")] public int feedChunk = 25;
    public float greetSeconds = 4f, bubbleSeconds = 3.2f;
    public float breathe = 0.03f, sway = 2.5f;

    [Header("Время суток: небо (верх, низ) и свет комнаты")]
    public Color morningSky = new Color(1f, 0.78f, 0.6f), daySky = new Color(0.55f, 0.8f, 1f), eveningSky = new Color(0.98f, 0.55f, 0.4f), nightSky = new Color(0.12f, 0.15f, 0.35f);
    public Color morningRoom = new Color(1f, 0.95f, 0.9f), dayRoom = Color.white, eveningRoom = new Color(0.95f, 0.82f, 0.72f), nightRoom = new Color(0.7f, 0.62f, 0.62f);
    [Tooltip("Насколько свет огня ярче при длинной серии (бонус, не наказание)")] public float streakLightBonus = 0.35f;

    [Header("Оформление: цвета (светлые карточки, тёмный текст)")]
    [Tooltip("Основной текст на светлом")] public Color ink = new Color32(58, 30, 16, 255);
    [Tooltip("Подписи и второстепенный текст")] public Color inkSoft = new Color32(110, 64, 38, 255);
    [Tooltip("Заголовки (тёмная медь)")] public Color heading = new Color32(138, 63, 28, 255);
    [Tooltip("Главное действие, заливка прогресса")] public Color amber = new Color32(247, 150, 46, 255);
    [Tooltip("«Готово»: текст и значки")] public Color mint = new Color32(31, 107, 69, 255);
    [Tooltip("«Готово»: светлая подложка карточки")] public Color mintTint = new Color32(222, 247, 230, 255);
    [Tooltip("Не хватает ресурса (приглушённый красный)")] public Color lowRed = new Color32(146, 44, 32, 255);
    [Tooltip("Подсветка доступного (тонировка плашки)")] public Color accentTint = new Color32(255, 236, 196, 255);
    [Tooltip("Затемнение под окнами")] public Color dim = new Color(0.16f, 0.08f, 0.04f, 0.72f);
    [Tooltip("Недоступное: тонировка плашки")] public Color mutedTint = new Color32(232, 222, 210, 255);

    [Header("Оформление: шрифт (единицы Canvas при ширине 1080; 14 sp ≈ 37, 16 sp ≈ 42)")]
    public float titleSize = 64f;
    public float subtitleSize = 46f, bodySize = 42f, captionSize = 38f;
    [Tooltip("Автоподбор кегля не опускается ниже (14 sp)")] public float minAutoSize = 37f;

    [Header("Оформление: размеры")]
    [Tooltip("Ширина всех окон «Очага»")] public float windowWidth = 1000f;
    [Tooltip("Наибольшая высота окна (на коротких экранах окно масштабируется RetFit)")] public float windowMaxHeight = 1460f;
    [Tooltip("Высота главных и вторичных кнопок (56 dp)")] public float buttonHeight = 148f;
    [Tooltip("Отступ между блоками и внутренний отступ карточек")] public float gap = 24f, padding = 36f;

    [Header("Оформление: анимации (время без учёта паузы)")]
    [Tooltip("Анимации интерфейса «Очага»: появление, полосы, бейджи, ценники, сбор награды")] public bool uiAnimations = true;
    [Tooltip("Появление элемента (fade + scale), с")] [Range(0.05f, 0.3f)] public float appearSeconds = 0.22f;
    [Tooltip("Заполнение полосы прогресса, с")] public float barSeconds = 0.45f;

    [Header("Огонёк: вид по стадиям (0 Искра … 7 Солнце)")]
    [Tooltip("Ширина огонька — доля ширины комнаты (комната вписана в ширину/высоту экрана). Огонёк не выходит за проём камина: высота с обликом ≤ арки")]
    public float[] flameWidth = { 0.095f, 0.117f, 0.136f, 0.153f, 0.167f, 0.178f, 0.187f, 0.195f };
    [Tooltip("Высота / ширина — силуэт стадии")] public float[] flameAspect = { 1.05f, 1.14f, 1.2f, 1.22f, 1.32f, 1.28f, 1.25f, 1.22f };
    [Tooltip("Язычков вокруг (0–5)")] public int[] flameTongues = { 1, 2, 3, 3, 4, 4, 5, 5 };
    [Tooltip("Искр в секунду")] public float[] flameSparks = { 0f, 0.6f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f };
    [Tooltip("Аксессуар стадии: 0 нет, 1 поленья, 2 поленья и луч маяка, 3 звёзды и нимб, 4 лучи солнца и нимб")]
    public int[] flameAccessory = { 0, 0, 0, 1, 1, 2, 3, 4 };
    [Tooltip("Огонёк в окнах (подарок, рост стадии): ширина комнаты, от которой считаются доли")] public float popupFlameUnit = 1700f;

    [Header("Огонёк: цвета линий (сосна, дуб, вишня)")]
    public Color[] lineOuter = { new Color32(52, 128, 255, 255), new Color32(255, 84, 30, 255), new Color32(240, 52, 86, 255) };
    public Color[] lineMid = { new Color32(96, 190, 255, 255), new Color32(255, 156, 46, 255), new Color32(255, 120, 140, 255) };
    public Color[] lineCore = { new Color32(220, 246, 255, 255), new Color32(255, 232, 150, 255), new Color32(255, 226, 232, 255) };
    public Color[] lineGlow = { new Color32(90, 170, 255, 255), new Color32(255, 140, 50, 255), new Color32(255, 90, 120, 255) };
    [Tooltip("Плавный переход цвета при смене линии или облика, с")] public float colorBlendSeconds = 0.6f;

    [Header("Огонёк: эффекты (флаги; частиц на экране Очага ≤ 30)")]
    public bool flameGlow = true;
    public bool flameShadow = true, flameReflection = true, flameSparksOn = true, flameTongueMotion = true, flameAccessories = true;
    [Tooltip("При «Эффекты: низкие» (FxQuality): без искр, отражения, лишних язычков и вращения аксессуаров")] public bool simplifyOnLowFx = true;

    [Header("Звуки (существующие клипы, разная высота)")]
    public AudioClip tapSound, feedSound, crackleSound, rewardSound;
    public Vector2 tapPitch = new Vector2(1.1f, 1.4f), feedPitch = new Vector2(0.9f, 1.1f);
    [Range(0f, 1f)] public float volume = 0.6f;

    [Header("Реплики (добавлять в конец)")]
    public List<FlameLine> flameLines = new List<FlameLine>();

    [ContextMenu("Заполнить реплики по умолчанию")]
    private void ResetLines() => FillLines(this);

    public static void FillLines(HearthVisualSettings s)
    {
        s.flameLines = new List<FlameLine>();
        void L(LineKind k, string ru, string en) => s.flameLines.Add(new FlameLine { kind = k, ru = ru, en = en });
        L(LineKind.GreetMorning, "Доброе утро! Я уже разогрелся.", "Good morning! I'm all warmed up.");
        L(LineKind.GreetMorning, "Утро! Самое время для одной головоломки.", "Morning! Perfect time for one puzzle.");
        L(LineKind.GreetMorning, "О, ты пришёл! А я как раз проснулся.", "Oh, you're here! I just woke up.");
        L(LineKind.GreetDay, "Привет! Камин тёплый, садись поближе.", "Hi! The hearth is warm, come closer.");
        L(LineKind.GreetDay, "Привет-привет! Я скучал. Немножко.", "Hello hello! I missed you. A little.");
        L(LineKind.GreetDay, "Ура, гости! Ну, точнее, ты.", "Yay, a guest! Well, you.");
        L(LineKind.GreetEvening, "Добрый вечер! Самое уютное время.", "Good evening! The coziest time of day.");
        L(LineKind.GreetEvening, "Вечер — время чая и спичек.", "Evening: time for tea and matches.");
        L(LineKind.GreetNight, "Тсс… ночь. Я тихонько потрескиваю.", "Shh… it's night. I'm crackling quietly.");
        L(LineKind.GreetNight, "Не спится? Мне тоже. Давай посидим.", "Can't sleep? Me neither. Let's sit a bit.");
        L(LineKind.GreetReturn, "С возвращением! Я берёг для тебя тепло.", "Welcome back! I kept the warmth for you.");
        L(LineKind.GreetReturn, "Ты вернулся! Начнём новую серию — щит при нас.", "You're back! Let's start a new streak, the shield is with us.");
        L(LineKind.GreetStreak, "Уже столько дней вместе! Мне светлее.", "So many days together! I'm glowing.");
        L(LineKind.GreetStreak, "Серия держится! Я горжусь нами.", "The streak holds! I'm proud of us.");
        L(LineKind.Tap, "Хи-хи, щекотно!", "Hee-hee, that tickles!");
        L(LineKind.Tap, "Бу! Испугался? Я тоже.", "Boo! Scared? Me too.");
        L(LineKind.Tap, "Привет, палец!", "Hello, finger!");
        L(LineKind.Tap, "Я тёплый, но не кусаюсь.", "I'm warm, but I don't bite.");
        L(LineKind.Tap, "Ещё разок подпрыгнуть?", "Jump once more?");
        L(LineKind.TapSpam, "Ой-ой, голова кружится!", "Whoa, I'm getting dizzy!");
        L(LineKind.TapSpam, "Дай отдышаться… то есть отгореться.", "Let me catch my breath… or my flame.");
        L(LineKind.Hold, "Грей ладошки, я не против.", "Warm your hands, I don't mind.");
        L(LineKind.Hold, "Ммм, как уютно.", "Mmm, so cozy.");
        L(LineKind.Feed, "Ням! Вкусный уголёк.", "Yum! Tasty ember.");
        L(LineKind.Feed, "Спасибо! Чувствую, расту.", "Thanks! I can feel myself growing.");
        L(LineKind.Feed, "Хрум-хрум. Ещё?", "Crunch crunch. More?");
        L(LineKind.Feed, "Этот пахнет лесом!", "This one smells like a forest!");
        L(LineKind.FeedEmpty, "Угольки закончились — их дают уровень дня и задания.", "Out of embers — the daily level and tasks give more.");
        L(LineKind.StageUp, "Смотри! Я стал больше!", "Look! I got bigger!");
        L(LineKind.StageUp, "Ух ты, новые искры!", "Wow, new sparks!");
        L(LineKind.Idle, "Ааа-уф… Кхм. Это я не зевал.", "Yaaawn… Ahem. That wasn't a yawn.");
        L(LineKind.Idle, "Интересно, что в том коробке…", "I wonder what's in that box…");
        L(LineKind.Idle, "Тик-так, часы считают угольки.", "Tick-tock, the clock counts embers.");
        L(LineKind.Idle, "Я сочиняю песню про спички.", "I'm writing a song about matches.");
        L(LineKind.Idle, "Пф! Искорка случайно вылетела.", "Pff! A spark slipped out.");
        L(LineKind.Hint, "Пройди уровень дня — он сегодня новый.", "Try the daily level — it's new today.");
        L(LineKind.Hint, "Перетащи уголёк ко мне — и я вырасту.", "Drag an ember to me and I'll grow.");
        L(LineKind.Hint, "Задания дня дают угольки и аромат дров.", "Daily tasks give embers and a wood aroma.");
        L(LineKind.Hint, "От дров зависит, каким я стану.", "The wood decides what I become.");
        L(LineKind.Tease, "Скоро праздник… я уже примерил костюм!", "A holiday is coming… I tried on the outfit already!");
        L(LineKind.Tease, "Секретный коробок шуршит. Что там?", "The secret box is rustling. What's inside?");
        L(LineKind.Tease, "Завтра будет новый уровень. Я подсмотрел!", "There's a new level tomorrow. I peeked!");
        L(LineKind.Event, "Праздник! Я в костюме — как тебе?", "Holiday! I'm in costume — what do you think?");
        L(LineKind.Event, "Праздничные коробки уже здесь!", "Holiday boxes are here!");   // без «только сейчас» (R-17)
        L(LineKind.DailyDone, "Уровень дня пройден! Танцую!", "Daily level done! I'm dancing!");
        L(LineKind.DailyDone, "Ты справился! Завтра будет новый.", "You did it! A new one tomorrow.");
        L(LineKind.Decor, "Ой, как красиво стало!", "Oh, how pretty!");
        L(LineKind.Decor, "Новая вещь! Я буду её охранять.", "Something new! I'll guard it.");
        L(LineKind.Night, "Ночью я горю потише. Спи сладко.", "At night I burn softly. Sleep tight.");
    }

    /// <summary>Подмена часа для отладки и снимков (−1 — настоящее время).</summary>
    public static int DebugHour = -1;
    public static int Hour => DebugHour >= 0 ? DebugHour : DateTime.Now.Hour;
    /// <summary>0 утро (5–10), 1 день (10–17), 2 вечер (17–21), 3 ночь.</summary>
    public static int TimeOfDay(int hour) => hour >= 5 && hour < 10 ? 0 : hour >= 10 && hour < 17 ? 1 : hour >= 17 && hour < 21 ? 2 : 3;
}

/// <summary>Выбор реплики: без повторов подряд (по виду и вообще).</summary>
public static class FlameLines
{
    private static readonly int[] last = NewLast();
    private static int[] NewLast() { var a = new int[32]; for (int i = 0; i < a.Length; i++) a[i] = -1; return a; }
    private static int lastAny = -1;
    private static readonly List<int> buf = new List<int>(16);

    public static FlameLine Pick(LineKind kind)
    {
        List<FlameLine> all = HearthVisualSettings.Current.flameLines;
        buf.Clear();
        for (int i = 0; i < all.Count; i++) if (all[i].kind == kind && i != lastAny) buf.Add(i);
        if (buf.Count == 0) for (int i = 0; i < all.Count; i++) if (all[i].kind == kind) buf.Add(i);
        if (buf.Count == 0) return null;
        int k = (int)kind;
        int pick = buf[UnityEngine.Random.Range(0, buf.Count)];
        if (buf.Count > 1 && pick == last[k]) pick = buf[(buf.IndexOf(pick) + 1) % buf.Count];
        last[k] = pick; lastAny = pick;
        return all[pick];
    }
}
