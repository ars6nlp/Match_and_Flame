using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Пул уровней дня (Assets/Resources/DailyLevelPool.asset). Префабы лежат в Resources/DailyLevels и грузятся по имени
/// только в момент запуска — таблица не тянет их в память меню. Уровень дня детерминирован по дате:
/// у всех игроков в один день один уровень; внутри уровня сложности — перестановка без повторов на весь цикл.
/// </summary>
[CreateAssetMenu(menuName = "Match & Flame/Daily Level Pool", fileName = "DailyLevelPool")]
public class DailyLevelPool : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string prefab;            // имя в Resources/DailyLevels
        [Tooltip("0 лёгкий (пн–вт), 1 средний (ср–чт), 2 сложный (пт–сб), 3 особый (вс)")] public int tier;
        public int tags;                 // RetTags
        public Vector2 solutionCenter;
        public float solutionRot, solutionPhase;
        public int matches;
        public float window;             // окно 3★, с (0 — без времени)
        public float randomWinShare;     // доля случайных побед
        public bool enabled = true;
    }

    public List<Entry> entries = new List<Entry>();

    private static DailyLevelPool current;
    public static DailyLevelPool Current => current != null ? current : current = Resources.Load<DailyLevelPool>("DailyLevelPool");

    public static int TierOf(int day)
    {
        int wd = DayClock.Weekday(day);
        return wd <= 1 ? 0 : wd <= 3 ? 1 : wd <= 5 ? 2 : 3;
    }

    /// <summary>Индекс записи для дня или −1.</summary>
    public int IndexFor(int day, int seed)
    {
        int tier = TierOf(day);
        var list = new List<int>();
        for (int i = 0; i < entries.Count; i++) if (entries[i].enabled && entries[i].tier == tier) list.Add(i);
        if (list.Count == 0) for (int i = 0; i < entries.Count; i++) if (entries[i].enabled) list.Add(i);   // запасной вариант
        if (list.Count == 0) return -1;
        int week = DayClock.Week(day), wd = DayClock.Weekday(day);
        int perWeek = tier == 3 ? 1 : 2;
        int k = week * perWeek + (tier == 3 ? 0 : wd % 2);
        int n = list.Count;
        int cycle = DayClock.FloorDiv(k, n), pos = DayClock.Mod(k, n);
        return list[CyclePerm(n, seed + tier * 7919, cycle)[pos]];
    }

    /// <summary>Перестановка цикла; если её первый уровень совпал с последним прошлого цикла — меняем первые два
    /// (одинаково для всех дней цикла, иначе соседние дни могли бы получить один уровень).</summary>
    private static int[] CyclePerm(int n, int salt, int cycle)
    {
        int[] perm = Permutation(n, RetRng.Mix(salt, cycle));
        if (n > 1)
        {
            int[] prev = Permutation(n, RetRng.Mix(salt, cycle - 1));
            if (perm[0] == prev[n - 1]) { int t = perm[0]; perm[0] = perm[1]; perm[1] = t; }
        }
        return perm;
    }

    private static int[] Permutation(int n, int seed)
    {
        int[] p = new int[n];
        for (int i = 0; i < n; i++) p[i] = i;
        var rng = new RetRng(seed);
        for (int i = n - 1; i > 0; i--) { int j = rng.Range(i + 1); int t = p[i]; p[i] = p[j]; p[j] = t; }
        return p;
    }

    public GameObject LoadPrefab(int index) =>
        index >= 0 && index < entries.Count ? Resources.Load<GameObject>("DailyLevels/" + entries[index].prefab) : null;
}
