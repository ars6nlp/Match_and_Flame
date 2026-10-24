using System;
using UnityEngine;

// Данные системы «Очаг»: описания стадий, декора, обликов, заданий, тем недели, коробков, глав, праздников.
// Списки живут в RetentionSettings.asset; новые элементы добавлять только в конец (индексы сохранены у игроков).

/// <summary>Вид «топлива»: от него зависит облик огонька на следующей стадии.</summary>
public enum RetFuel { Pine = 0, Oak = 1, Cherry = 2 }

/// <summary>Механики уровня (битовая маска) — для заданий и тем недели.</summary>
[Flags]
public enum RetTags { None = 0, Water = 1, Wind = 2, Pendulum = 4, Immune = 8, Gold = 16, Hard = 32 }

public enum QuestKind
{
    WinLevels, WinStars3, BurnMatches, ChainInLevel, BurnGold, WinWater, WinWind, WinPendulum, WinImmune,
    WinNoRestart, WinFast, PlayDaily, Daily3Stars, FeedHearth, EarnStars, WinHard, WinDifferent
}

public enum BoxRarity { Common = 0, Rare = 1, Legendary = 2 }

[Serializable]
public class RetStage
{
    public string nameRU, nameEN;
    [Tooltip("Сколько тепла нужно накопить (с начала игры), чтобы достичь стадии")] public int warmth;
    [Tooltip("Монеты за достижение стадии")] public int coins;
    [Tooltip("Облик, который открывается на стадии (−1 — нет)")] public int outfit = -1;
    [Tooltip("Размер огонька в очаге (1 — как в меню)")] public float scale = 1f;
    public string lineRU, lineEN;
}

[Serializable]
public class RetDecor
{
    public bool enabled = true;
    public string nameRU, nameEN;
    public int priceCoins, priceEmbers;
    [Tooltip("Имя спрайта в атласе очага")] public string sprite;
    [Tooltip("Место в комнате: центр (доли экрана комнаты) и ширина (доля ширины)")] public Vector2 slot;
    public float width = 0.2f;
    [Tooltip("Рисовать за огоньком (фон) или перед ним")] public bool behind = true;
    [Tooltip("Лежит на полу под огоньком (коврик): после камина, но до огонька")] public bool underFlame;
    [Tooltip("Слой в сцене Очага: 0 стена, 1 полка камина (низ по линии полки), 2 пол под огоньком, 3 перед огоньком")] public int layer;
    [Tooltip("Живёт: 0 нет, 1 маятник, 2 мерцает, 3 колышется, 4 пар, 5 покачивается")] public int life;
}

[Serializable]
public class RetOutfit
{
    public bool enabled = true;
    public string nameRU, nameEN;
    public string sprite;
    [Tooltip("Смещение над макушкой огонька (доли высоты огонька) и масштаб")] public Vector2 offset = new Vector2(0f, 0.42f);
    public float size = 0.36f;
    [Tooltip("Откуда берётся (для подписи): стадия, праздник, неделя, секретный коробок")] public string sourceRU, sourceEN;
}

[Serializable]
public class RetQuest
{
    public bool enabled = true;
    public QuestKind kind;
    public int target = 1;
    public RetFuel fuel;
    [Tooltip("{0} — число")] public string textRU, textEN;
}

[Serializable]
public class RetWeekTheme
{
    public bool enabled = true;
    public QuestKind kind;
    public int target;
    public string nameRU, nameEN, textRU, textEN;
    [Tooltip("Облик-награда (−1 — нет)")] public int outfit = -1;
    [Tooltip("Цвет темы")] public Color color = new Color(1f, 0.7f, 0.3f);
}

[Serializable]
public class RetBox
{
    public bool enabled = true;
    public string nameRU, nameEN;
    public BoxRarity rarity;
    [Tooltip("Набор из 4 коробков (−1 — праздничный, вне наборов)")] public int set = -1;
    [Tooltip("Праздничный: выпадает только на празднике")] public bool seasonal;
    public string sprite;
}

[Serializable]
public class RetChapter
{
    public int first, last;
    public string nameRU, nameEN;
    public Color color = new Color(1f, 0.6f, 0.25f);
    [Tooltip("Мотив заставки: 0 искры, 1 маятник, 2 стрелки, 3 капли, 4 вихрь, 5 звёзды")] public int motif;
}

/// <summary>Праздник из Resources/Retention/events.json (даты — данные, а не код).</summary>
[Serializable]
public class RetSeasonal
{
    public string id;
    public string start, end;      // "MM-DD", включительно; конец может быть в следующем году (25.12 → 07.01)
    public string nameRU, nameEN;
    public string textRU, textEN;
    public string tint = "#FFB060";
    public int outfit = -1;        // костюм-награда (индекс облика)
    public int box = -1;           // праздничный коробок (индекс в списке коробков)
    public int dailyTarget = 3;    // сколько уровней дня пройти за праздник

    public static bool TryParse(string mmdd, out int month, out int day)
    {
        month = day = 0;
        if (string.IsNullOrEmpty(mmdd) || mmdd.Length != 5 || mmdd[2] != '-') return false;
        return int.TryParse(mmdd.Substring(0, 2), out month) && int.TryParse(mmdd.Substring(3, 2), out day)
               && month >= 1 && month <= 12 && day >= 1 && day <= 31;
    }

    /// <summary>Идёт ли праздник в эту дату; year — год начала праздника (для ключа «получено»).</summary>
    public bool Contains(DateTime date, out int year)
    {
        year = date.Year;
        if (!TryParse(start, out int sm, out int sd) || !TryParse(end, out int em, out int ed)) return false;
        int d = date.Month * 100 + date.Day, s = sm * 100 + sd, e = em * 100 + ed;
        if (s <= e) return d >= s && d <= e;
        // через Новый год: 25.12 … 07.01
        if (d >= s) return true;
        if (d <= e) { year = date.Year - 1; return true; }
        return false;
    }

    /// <summary>Ближайшая дата начала не раньше date.</summary>
    public DateTime NextStart(DateTime date)
    {
        if (!TryParse(start, out int sm, out int sd)) return DateTime.MaxValue;
        DateTime s = SafeDate(date.Year, sm, sd);
        if (s < date.Date) s = SafeDate(date.Year + 1, sm, sd);
        return s;
    }

    private static DateTime SafeDate(int y, int m, int d) => new DateTime(y, m, Math.Min(d, DateTime.DaysInMonth(y, m)));
}

[Serializable] public class RetSeasonalList { public RetSeasonal[] events; }

[Serializable]
public class RetWhatsNew
{
    public string version;
    public string titleRU, titleEN;
    public string[] linesRU, linesEN;
}

[Serializable] public class RetWhatsNewList { public RetWhatsNew[] entries; }

/// <summary>Итог уровня для заданий, недели и серии (собирает GameManager).</summary>
public struct RetLevelResult
{
    public bool won;
    public int stars, burnt, total, golds;
    public RetTags tags;
    public bool restarted;        // перед этим уровнем нажимали «Заново»
    public float seconds;         // от первого поджога до конца
    public bool daily;
    public int levelIndex;        // −1 для уровня дня
}
