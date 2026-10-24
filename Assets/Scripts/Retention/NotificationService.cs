using System;

/// <summary>
/// Локальные напоминания «Очага». Пока реализация пустая: пакет com.unity.mobile.notifications не установлен —
/// решение за владельцем (RETENTION_DESIGN.md, раздел 10). После установки достаточно добавить реализацию
/// под #if UNITY_ANDROID и вернуть её из Create().
/// </summary>
public interface INotificationService
{
    bool Available { get; }
    bool HasPermission { get; }
    void RequestPermission(Action<bool> done);
    void ScheduleOnce(DateTime when, string title, string body);
    void CancelAll();
}

public class NullNotificationService : INotificationService
{
    public bool Available => false;
    public bool HasPermission => false;
    public void RequestPermission(Action<bool> done) => done?.Invoke(false);
    public void ScheduleOnce(DateTime when, string title, string body) { }
    public void CancelAll() { }
}

/// <summary>Когда и что напомнить: не чаще раза в день, без тихих часов, без упрёков; отмена, если всё уже сделано.</summary>
public static class NotificationPlanner
{
    private static INotificationService service;
    public static INotificationService Service => service ?? (service = new NullNotificationService());

    /// <summary>Время следующего напоминания или null (выключено / уже всё сделано сегодня и завтра рано).</summary>
    public static DateTime? Next(DateTime now, int hour, int quietFrom, int quietTo, bool doneToday)
    {
        if (hour < 0) return null;
        hour = Clamp(hour, quietTo, quietFrom - 1);   // в тихие часы (22:00–09:00) не напоминаем
        DateTime today = now.Date.AddHours(hour);
        DateTime when = !doneToday && today > now.AddMinutes(30) ? today : today.AddDays(1);
        return when;
    }

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

    public static readonly string[] TextsRU =
    {
        "Очаг ждёт: сегодняшний коробок с уровнем уже на месте.",
        "Огонёк приготовил новую головоломку дня.",
        "В очаге потрескивают угольки — заглянешь на пару минут?",
        "Новые задания дня готовы. Огонёк держит для тебя место у огня.",
    };
    public static readonly string[] TextsEN =
    {
        "The hearth is warm: today's puzzle box has arrived.",
        "Your little flame has a new daily puzzle ready.",
        "Embers are crackling in the hearth — got a couple of minutes?",
        "New daily tasks are ready. The flame saved you a spot by the fire.",
    };

    /// <summary>Перепланировать (вызывается при выходе из игры и после выполнения уровня дня).</summary>
    public static void Reschedule(RetentionCore core)
    {
        INotificationService svc = Service;
        if (!svc.Available || core == null) return;
        svc.CancelAll();
        if (!core.cfg.notifications || !core.s.notifEnabled || !svc.HasPermission) return;
        int hour = core.s.notifHour >= 0 ? core.s.notifHour : core.cfg.defaultReminderHour;
        DateTime? when = Next(DateTime.Now, hour, core.cfg.quietFrom, core.cfg.quietTo, core.DailyDoneToday);
        if (when == null) return;
        int i = DayClock.Mod(DayClock.DayNumber(when.Value), TextsRU.Length);
        bool ru = LanguageInit.CurrentLang == "ru";
        svc.ScheduleOnce(when.Value, ru ? "Спичка и Пламя" : "Match & Flame", ru ? TextsRU[i] : TextsEN[i]);
    }
}
