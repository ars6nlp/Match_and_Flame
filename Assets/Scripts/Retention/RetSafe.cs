using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ошибки «Очага» не должны ронять игру или блокировать меню: место сбоя логируется один раз,
/// дальше система возвращения работает со значениями по умолчанию или отключается.
/// </summary>
public static class RetSafe
{
    private static readonly HashSet<string> logged = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetLog() => logged.Clear();

    public static void Report(string where, Exception e)
    {
        if (logged.Add(where)) Debug.LogError("[Retention] " + where + ": " + e);
    }
}
