using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_ANDROID || UNITY_IOS
using UnityEngine.SceneManagement;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
#endif

#if UNITY_ANDROID || UNITY_IOS

public class GoogleAdsManager : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Тестовые сборки — только официальные тестовые блоки Google: показы и клики
    // на своих устройствах по боевым ID AdMob считает недействительным трафиком.
    private string bannerId = "ca-app-pub-3940256099942544/6300978111";
    private string interstitialId = "ca-app-pub-3940256099942544/1033173712";
    private string rewardedId = "ca-app-pub-3940256099942544/5224354917";
#else
    // Боевые ID лежат в AdIds.cs (не в git), пример — BuildSetup/AdIds.Example.cs
    private string bannerId = AdIds.Banner;
    private string interstitialId = AdIds.Interstitial;
    private string rewardedId = AdIds.Rewarded;
#endif

    private BannerView bannerView;
    private InterstitialAd interstitialAd;
    private RewardedAd rewardedAd;


    // Очередь для выполнения кода строго в главном потоке Unity.
    // Google Mobile Ads вызывает некоторые колбэки (например onReward) из фонового потока,
    // а PlayerPrefs, UI и большинство Unity API можно трогать только из главного потока.
    private readonly Queue<Action> _mainThreadActions = new Queue<Action>();

    public static GoogleAdsManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    // --- Баннер. Во время уровня он внизу экрана, прямо там, где игрок таскает спички:
    // случайные клики по рекламе нарушают политику AdMob. Поэтому в GameScene баннер скрыт.
    [Header("Баннер")]
    [Tooltip("Показывать баннер во время уровня (GameScene). Выключено — меньше дохода, но нет случайных кликов")]
    [SerializeField] private bool showBannerDuringGameplay = false;
    private const string GameplaySceneName = "GameScene";

    private bool BannerAllowedNow =>
        showBannerDuringGameplay || SceneManager.GetActiveScene().name != GameplaySceneName;

    private void OnActiveSceneChanged(Scene from, Scene to) => UpdateBannerVisibility();

    private void UpdateBannerVisibility()
    {
        if (bannerView == null) return;
        if (BannerAllowedNow) bannerView.Show();
        else bannerView.Hide();
    }

    private void Update()
    {
        // Выполняем накопленные действия из фоновых потоков здесь, в главном потоке.
        lock (_mainThreadActions)
        {
            while (_mainThreadActions.Count > 0)
            {
                _mainThreadActions.Dequeue()?.Invoke();
            }
        }
    }

    private void RunOnMainThread(Action action)
    {
        lock (_mainThreadActions)
        {
            _mainThreadActions.Enqueue(action);
        }
    }

    private void Start()
    {
        // На устройствах из AdIds.TestDevices даже релизная сборка получает тестовую рекламу
        MobileAds.SetRequestConfiguration(new RequestConfiguration
        {
            TestDeviceIds = new List<string>(AdIds.TestDevices)
        });

        RequestConsent();

        // Согласие, полученное в прошлой сессии, позволяет не ждать сети и формы
        InitializeAdsIfAllowed();
    }

    // --- Согласие GDPR (UMP). AdMob требует сертифицированную CMP для ЕЭЗ, UK и Швейцарии.
    // Форма показывается только там, где она обязательна; рекламу запрашиваем
    // только после ConsentInformation.CanRequestAds().

    /// <summary>Форма UMP закончила работу (показана, не нужна или не загрузилась).</summary>
    public event Action ConsentFlowFinished;

    private bool adsInitialized;

    private void RequestConsent()
    {
        var request = new ConsentRequestParameters();
#if DEVELOPMENT_BUILD
        // В Development Build притворяемся, что мы в ЕЭЗ, чтобы увидеть форму.
        // Работает только на устройствах из AdIds.TestDevices.
        request.ConsentDebugSettings = new ConsentDebugSettings
        {
            DebugGeography = DebugGeography.EEA,
            TestDeviceHashedIds = new List<string>(AdIds.TestDevices)
        };
#endif
        ConsentInformation.Update(request, updateError => RunOnMainThread(() =>
        {
            if (updateError != null)
            {
                // Нет сети и т. п. — работаем с согласием из прошлой сессии, если оно было
                Debug.LogWarning("UMP: не удалось обновить статус согласия: " + updateError.Message);
                FinishConsentFlow();
                return;
            }

            ConsentForm.LoadAndShowConsentFormIfRequired(formError => RunOnMainThread(() =>
            {
                if (formError != null)
                    Debug.LogWarning("UMP: ошибка формы согласия: " + formError.Message);
                FinishConsentFlow();
            }));
        }));
    }

    private void FinishConsentFlow()
    {
        InitializeAdsIfAllowed();
        ConsentFlowFinished?.Invoke();
    }

    private void InitializeAdsIfAllowed()
    {
        if (adsInitialized || !ConsentInformation.CanRequestAds()) return;
        adsInitialized = true;

        MobileAds.Initialize(initStatus => RunOnMainThread(() =>
        {
            Debug.Log("Google Mobile Ads инициализирован");
            LoadBanner();
            LoadInterstitial();
            LoadRewarded();
        }));
    }

    /// <summary>Нужна ли кнопка «Настройки конфиденциальности» (обязательна, если форма UMP применима).</summary>
    public bool PrivacyOptionsRequired =>
        ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

    /// <summary>Повторно открыть форму согласия — вызывается кнопкой в настройках.</summary>
    public void ShowPrivacyOptions()
    {
        ConsentForm.ShowPrivacyOptionsForm(formError => RunOnMainThread(() =>
        {
            if (formError != null)
                Debug.LogWarning("UMP: ошибка формы конфиденциальности: " + formError.Message);
            // Игрок мог отозвать или дать согласие — если реклама ещё не запускалась, пробуем сейчас
            InitializeAdsIfAllowed();
        }));
    }

    // --- Повторная загрузка после ошибки (нет сети, нет заполнения):
    // 2, 4, 8 ... 64 с, сброс после успешной загрузки.
    private const float MaxRetryDelay = 64f;
    private int bannerAttempts, interstitialAttempts, rewardedAttempts;
    private bool interstitialLoading, rewardedLoading;

    private static float NextRetryDelay(ref int attempts)
    {
        attempts++;
        return Mathf.Min(MaxRetryDelay, Mathf.Pow(2f, attempts));
    }

    private IEnumerator RetryAfter(float seconds, Action load)
    {
        // Realtime: на паузе (timeScale = 0) загрузка тоже должна повторяться
        yield return new WaitForSecondsRealtime(seconds);
        load();
    }

    private void LoadBanner()
    {
        if (bannerView != null) bannerView.Destroy();
        // Адаптивный якорный баннер во всю ширину вместо фиксированного 320×50:
        // высота подбирается под экран, SDK сам держит его в безопасной зоне
        AdSize size = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
        bannerView = new BannerView(bannerId, size, AdPosition.Bottom);
        bannerView.OnBannerAdLoaded += () => RunOnMainThread(() =>
        {
            Debug.Log("Banner loaded!");
            bannerAttempts = 0;
            UpdateBannerVisibility(); // загруженный баннер показывается сам — прячем, если сейчас уровень
        });
        bannerView.OnBannerAdLoadFailed += (error) =>
        {
            Debug.Log("Banner failed: " + error);
            RunOnMainThread(() => StartCoroutine(RetryAfter(NextRetryDelay(ref bannerAttempts), LoadBanner)));
        };
        bannerView.LoadAd(new AdRequest());
    }

    private void LoadInterstitial()
    {
        if (interstitialLoading) return;
        interstitialLoading = true;

        InterstitialAd.Load(interstitialId, new AdRequest(), (ad, error) =>
        {
            if (error != null || ad == null)
            {
                Debug.Log("Interstitial failed: " + error);
                RunOnMainThread(() =>
                {
                    interstitialLoading = false;
                    StartCoroutine(RetryAfter(NextRetryDelay(ref interstitialAttempts), LoadInterstitial));
                });
                return;
            }
            // Колбэки SDK приходят не из главного потока Unity — всё дальше выполняем в нём
            RunOnMainThread(() =>
            {
                interstitialLoading = false;
                interstitialAttempts = 0;
                interstitialAd?.Destroy(); // протухшая реклама, которую так и не показали
                interstitialAd = ad;
                ad.OnAdFullScreenContentClosed += () => RunOnMainThread(() => ReplaceInterstitial(ad));
                // Не смогли показать (протухла, сбой SDK) — без этого следующей рекламы не было бы никогда
                ad.OnAdFullScreenContentFailed += _ => RunOnMainThread(() => ReplaceInterstitial(ad));
            });
        });
    }

    // Показанную рекламу повторно использовать нельзя: освобождаем нативный объект и грузим новую
    private void ReplaceInterstitial(InterstitialAd used)
    {
        used.Destroy();
        if (interstitialAd == used) interstitialAd = null;
        LoadInterstitial();
    }

    /// <summary>true — реклама действительно показана (только тогда сбрасываются счётчик и кулдаун).</summary>
    private bool ShowInterstitialAd()
    {
#if UNITY_EDITOR
        Debug.Log("EDITOR: имитация Interstitial");
        return true;
#else
        if (interstitialAd != null && interstitialAd.CanShowAd())
        {
            interstitialAd.Show();
            return true;
        }
        Debug.Log("Interstitial ещё не готов");
        // Без согласия (C-02) реклама не инициализирована — грузить нельзя
        if (adsInitialized) LoadInterstitial();
        return false;
#endif
    }

    private void LoadRewarded()
    {
        if (rewardedLoading) return;
        rewardedLoading = true;

        RewardedAd.Load(rewardedId, new AdRequest(), (ad, error) =>
        {
            if (error != null || ad == null)
            {
                Debug.Log("Rewarded failed: " + error);
                RunOnMainThread(() =>
                {
                    rewardedLoading = false;
                    StartCoroutine(RetryAfter(NextRetryDelay(ref rewardedAttempts), LoadRewarded));
                });
                return;
            }
            RunOnMainThread(() =>
            {
                rewardedLoading = false;
                rewardedAttempts = 0;
                rewardedAd?.Destroy();
                rewardedAd = ad;
                ad.OnAdFullScreenContentClosed += () => RunOnMainThread(() => ReplaceRewarded(ad));
                ad.OnAdFullScreenContentFailed += _ => RunOnMainThread(() => ReplaceRewarded(ad));
            });
        });
    }

    // Награда к этому моменту уже в очереди главного потока (она приходит до закрытия)
    private void ReplaceRewarded(RewardedAd used)
    {
        used.Destroy();
        if (rewardedAd == used) rewardedAd = null;
        LoadRewarded();
    }

    public void ShowRewardedAd(Action onReward)
    {
#if UNITY_EDITOR
        Debug.Log("EDITOR: имитация успешного просмотра.");
        onReward?.Invoke();
        return;
#endif
        if (rewardedAd != null && rewardedAd.CanShowAd())
        {
            rewardedAd.Show((Reward reward) =>
            {
                Debug.Log("Пользователь получил награду!");
                // Колбэк может прийти из фонового потока — переносим выполнение в главный поток
                RunOnMainThread(() => onReward?.Invoke());
            });
        }
        else
        {
            Debug.Log("Rewarded ещё не готов");
            AndroidToast.Show("Ad is not available yet. Please try again later",
                              "Реклама пока недоступна. Попробуйте позже");
            if (adsInitialized) LoadRewarded();
        }
    }

    // Когда показывать — InterstitialPolicy: общий счётчик пауз, кулдаун, не в момент появления меню
    public void OnLevelCompleted(bool daily = false)
    {
        float now = Time.realtimeSinceStartup;
        if (InterstitialPolicy.LevelCompleted(now, daily) && ShowInterstitialAd())
            InterstitialPolicy.Shown(now);
    }

    public void OnMenuEntered() => InterstitialPolicy.MenuEntered();
}

#else

// На WebGL (Яндекс Игры) реклама идёт через Yandex SDK — см. AdsManager -> YG2.
// Google Mobile Ads здесь не подключается, но объект "YandexAdsManager" лежит в MainScene.
// Без этой заглушки класс не существовал бы в WebGL-сборке, компонент стал бы
// "missing script" и Unity писал бы ошибку в консоль браузера при загрузке меню.
public class GoogleAdsManager : MonoBehaviour
{
    private void Awake()
    {
        Destroy(gameObject);
    }
}

#endif