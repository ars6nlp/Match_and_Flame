using UnityEngine;
using System;

public static class CurrencyManager
{
    private const string GOLDEN_MATCH_KEY = "GoldenMatchesCurrency";

    public static event Action OnCurrencyChanged;

    public static int GetGoldenMatches()
    {
        return PlayerPrefs.GetInt(GOLDEN_MATCH_KEY, 0);
    }

    public static void AddGoldenMatches(int amount)
    {
        if (amount <= 0) return;

        int current = GetGoldenMatches();
        PlayerPrefs.SetInt(GOLDEN_MATCH_KEY, current + amount);
        PlayerPrefs.Save();

        OnCurrencyChanged?.Invoke();
    }

    public static bool SpendGoldenMatches(int amount)
    {
        int current = GetGoldenMatches();
        if (current >= amount)
        {
            PlayerPrefs.SetInt(GOLDEN_MATCH_KEY, current - amount);
            PlayerPrefs.Save();
            OnCurrencyChanged?.Invoke();
            return true;
        }
        return false;
    }
}
