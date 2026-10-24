using System;

/// <summary>
/// Календарь «Очага»: день = локальная календарная дата устройства (летнее время и смена пояса не ломают счёт —
/// важна дата, а не часы). Номер дня — дни от 2000-01-01; неделя начинается в понедельник.
/// В Development Build и редакторе дату можно сдвинуть (Ret_DebugDayOffset) — для проверки серии и событий.
/// </summary>
public static class DayClock
{
    private static readonly DateTime Epoch = new DateTime(2000, 1, 1);   // суббота
    private const int FirstMonday = 2;                                   // 2000-01-03

    public static int DayNumber(DateTime local) => (int)(local.Date - Epoch).TotalDays;
    public static DateTime DateOf(int day) => Epoch.AddDays(day);

    /// <summary>Номер недели (понедельник — первый день).</summary>
    public static int Week(int day) => FloorDiv(day - FirstMonday, 7);
    /// <summary>0 — понедельник … 6 — воскресенье.</summary>
    public static int Weekday(int day) => Mod(day - FirstMonday, 7);
    public static int WeekStart(int week) => week * 7 + FirstMonday;

    public static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);
    public static int Mod(int a, int b) { int r = a % b; return r < 0 ? r + b : r; }

    /// <summary>Сдвиг дат для отладки (только редактор и Development Build).</summary>
    public static int DebugOffset
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return UnityEngine.PlayerPrefs.GetInt("Ret_DebugDayOffset", 0);
#else
            return 0;
#endif
        }
        set
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.PlayerPrefs.SetInt("Ret_DebugDayOffset", value);
            UnityEngine.PlayerPrefs.Save();
#endif
        }
    }

    public static DateTime Now => DateTime.Now.AddDays(DebugOffset);
    public static int Today => DayNumber(Now);

    /// <summary>Сколько осталось до следующей локальной полуночи.</summary>
    public static TimeSpan UntilMidnight => DateTime.Now.Date.AddDays(1) - DateTime.Now;
}

/// <summary>Детерминированный генератор (одинаковый на всех платформах): xorshift32 от хеша.</summary>
public struct RetRng
{
    private uint x;
    public RetRng(int seed) { x = Hash((uint)seed); if (x == 0) x = 0x9E3779B9u; }
    public static uint Hash(uint v) { v ^= v >> 16; v *= 0x7feb352dU; v ^= v >> 15; v *= 0x846ca68bU; v ^= v >> 16; return v; }
    public static int Mix(int a, int b) => (int)Hash((uint)a * 0x9E3779B1u ^ Hash((uint)b));
    public uint Next() { x ^= x << 13; x ^= x >> 17; x ^= x << 5; return x; }
    public int Range(int n) => n <= 0 ? 0 : (int)(Next() % (uint)n);
}
