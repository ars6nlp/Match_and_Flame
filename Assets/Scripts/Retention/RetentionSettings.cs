using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Все параметры системы «Очаг» (Assets/Resources/RetentionSettings.asset). Каждая фича выключается флагом;
/// числа баланса — здесь же (обоснование — RETENTION_DESIGN.md). Списки контента: добавлять в конец, не удалять из середины.
/// </summary>
[CreateAssetMenu(menuName = "Match & Flame/Retention Settings", fileName = "RetentionSettings")]
public class RetentionSettings : ScriptableObject
{
    private static RetentionSettings current;
    public static RetentionSettings Current
    {
        get
        {
            if (current == null) current = Resources.Load<RetentionSettings>("RetentionSettings");
            if (current == null) { current = CreateInstance<RetentionSettings>(); RetentionDefaults.Fill(current); }
            return current;
        }
    }

    [Header("Мягкий вход")]
    [Tooltip("Очаг, «Сегодня» и задания появляются после стольких пройденных уровней (новичок видит простой интерфейс)")] public int unlockLevels = 3;
    [Tooltip("Коллекция, события и тизер — со стадии (0 Искра, 1 Язычок…)")] public int extrasStage = 1;
    [Tooltip("Угольки для первого знакомства (один раз, если угольков нет)")] public int starterEmbers = 20;

    [Header("Фичи (каждая выключается отдельно)")]
    public bool systemEnabled = true;
    public bool dailyGift = true, streak = true, dailyLevel = true, hearth = true, quests = true;
    public bool weeklyGoal = true, weeklyEvent = true, seasonal = true, collection = true, secretBox = true;
    public bool chapters = true, teaser = true, whatsNew = true, notifications = false;
    [Tooltip("Отладочная панель (только Development Build и редактор)")] public bool debugPanel = true;

    [Header("Подарок дня")]
    public int giftEmbers = 10;
    public int giftStreakBonusPerDay = 1, giftStreakBonusMax = 10;
    [Tooltip("Монета за каждый N-й день серии (0 — нет)")] public int giftCoinEvery = 7;
    [Tooltip("Сколько секунд окно подарка держится само (закрывается и тапом)")] public float giftPopupSeconds = 4.5f;

    [Header("Серия и щиты")]
    public int shieldMax = 2;
    [Tooltip("Перерыв дольше этого — серия начинается заново (лучший результат хранится)")] public int longBreakDays = 7;
    public int[] streakMilestones = { 3, 5, 7, 14, 21, 30 };
    public int[] milestoneEmbers = { 30, 40, 60, 100, 150, 200 };
    [Tooltip("Яркость очага за день серии и предел")] public float glowPerStreakDay = 0.1f, glowMax = 0.7f;

    [Header("Уровень дня")]
    public int dailyWinEmbers = 25, dailyWinCoins = 1, daily3StarEmbers = 15, daily3StarCoins = 0;
    [Tooltip("Аромат за механику уровня дня")] public int dailyAroma = 10;
    [Tooltip("Соль перестановки пула (одинакова у всех игроков)")] public int dailySeed = 20261017;

    [Header("Задания дня")]
    public int questEmbers = 15, questAllBonus = 15, questAroma = 10;
    [Tooltip("Сколько заданий в день можно заменить бесплатно")] public int questRerollsPerDay = 1;
    public int regularWinEmbers = 3, regularWinCap = 5;
    public List<RetQuest> questPool = new List<RetQuest>();

    [Header("Неделя")]
    public int weeklyGoalWins = 12, weeklyGoalEmbers = 80, weeklyGoalCoins = 4;
    [Tooltip("Этапы дорожки недели до цели (победы) и их угольки")] public int[] weekSteps = { 4, 8 };
    public int[] weekStepEmbers = { 20, 30 };
    public int weeklyEventEmbers = 60, weeklyEventDupEmbers = 100;
    public List<RetWeekTheme> weekThemes = new List<RetWeekTheme>();

    [Header("Очаг: стадии (тепло накапливается)")]
    public List<RetStage> stages = new List<RetStage>();
    [Tooltip("Цвета линий: сосна, дуб, вишня")] public Color[] fuelColors = { new Color(0.45f, 0.8f, 1f), new Color(1f, 0.78f, 0.3f), new Color(1f, 0.35f, 0.4f) };
    [Tooltip("Скин кадров огонька для линии (индекс базового скина: 0 оранж, 1 зелёный, 2 синий, 3 фиолетовый)")] public int[] fuelSkin = { 2, 0, 3 };
    public string[] fuelNamesRU = { "Сосна", "Дуб", "Вишня" }, fuelNamesEN = { "Pine", "Oak", "Cherry" };

    [Header("Комната очага")]
    public List<RetDecor> decor = new List<RetDecor>();

    [Header("Облики огонька (аксессуары)")]
    public List<RetOutfit> outfits = new List<RetOutfit>();

    [Header("Коллекция коробков")]
    public List<RetBox> boxes = new List<RetBox>();
    [Tooltip("Шансы редкостей, % (показываются игроку)")] public int[] rarityOdds = { 60, 30, 10 };
    public int[] dupEmbers = { 20, 40, 80 };
    public int setEmbers = 100;

    [Header("Секретный коробок")]
    public int secretStreak = 5;
    [Tooltip("Награды по кругу: облики (индексы)")] public int[] secretOutfits = { 15, 16, 17 };

    [Header("Главы (группировка уровней, индексы не меняются)")]
    public List<RetChapter> chapterList = new List<RetChapter>();
    [Tooltip("Пусто — тизер следующей главы не показывается (пока она не запланирована). Пример: «Глава {0} — скоро»")]
    public string nextChapterRU = "", nextChapterEN = "";

    [Header("Механики уровней каталога (заполняет Editor-скрипт)")]
    public int[] levelTags = new int[0];
    public int[] levelDifficulty = new int[0];

    [Header("Напоминания (после решения по пакету уведомлений)")]
    public int quietFrom = 22, quietTo = 9, defaultReminderHour = 18;

    [Header("Оформление (ссылки на существующие ассеты)")]
    public TMP_FontAsset font;
    public Sprite card, button, buttonLight, pill, shadow, glow;
    [Tooltip("Процедурные спрайты (атлас очага)")] public Sprite[] art = new Sprite[0];
    public AudioClip rewardSound, stageSound, tapSound;
    public Color title = new Color(1f, 0.71f, 0.28f), textLight = new Color(0.95f, 0.81f, 0.5f), textDark = new Color(0.16f, 0.06f, 0.03f);

    private Dictionary<string, Sprite> artMap;
    public Sprite Art(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (artMap == null || artMap.Count != art.Length)
        {
            artMap = new Dictionary<string, Sprite>();
            foreach (Sprite s in art) if (s != null) artMap[s.name] = s;
        }
        return artMap.TryGetValue(name, out Sprite r) ? r : null;
    }

    public RetTags TagsOf(int level) => level >= 0 && level < levelTags.Length ? (RetTags)levelTags[level] : RetTags.None;

    [ContextMenu("Заполнить контент по умолчанию (стадии, декор, облики, задания, темы, коробки, главы)")]
    private void ResetContent() => RetentionDefaults.Fill(this);
}
