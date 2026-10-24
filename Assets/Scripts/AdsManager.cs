using System;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using YG;
#endif

public static class AdsManager
{
#if UNITY_WEBGL && !UNITY_EDITOR
    // Счётчики Яндекс Игр (WebGL); на Android частотой управляет InterstitialPolicy
    private static int levelCompleteCount = 0;
    private static int menuReturnCount = 0;
#endif

    public static void OnLevelCompleted(bool daily = false)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        levelCompleteCount++;
        if (levelCompleteCount >= 5)
        {
            levelCompleteCount = 0;
            YG2.InterstitialAdvShow();
        }
#elif UNITY_ANDROID || UNITY_IOS
        if (GoogleAdsManager.Instance != null)
            GoogleAdsManager.Instance.OnLevelCompleted(daily);
#endif
    }

    public static void OnMenuEntered()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        menuReturnCount++;
        if (menuReturnCount >= 3)
        {
            menuReturnCount = 0;
            YG2.InterstitialAdvShow();
        }
#elif UNITY_ANDROID || UNITY_IOS
        if (GoogleAdsManager.Instance != null)
            GoogleAdsManager.Instance.OnMenuEntered();
#endif
    }

    public static void ShowRewardedAd(Action onReward)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        YG2.RewardedAdvShow("bonus_skin", () => onReward?.Invoke());
#elif UNITY_ANDROID || UNITY_IOS
        if (GoogleAdsManager.Instance != null)
        {
            GoogleAdsManager.Instance.ShowRewardedAd(onReward);
        }
        else
        {
            // Рекламный менеджер не создан (например, игра запущена не с MainScene) —
            // награду без просмотра рекламы не выдаём
            AndroidToast.Show("Ad is not available yet. Please try again later",
                              "Реклама пока недоступна. Попробуйте позже");
        }
#else
        // В обычном Editor-режиме (не WebGL/Android сборка) сразу выдаём награду для удобства тестирования
        onReward?.Invoke();
#endif
    }
}