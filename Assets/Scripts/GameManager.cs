using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Настройки Уровней")]
    [Tooltip("Запасной список, если нет Assets/Resources/LevelCatalog.asset — основной источник уровней каталог (L-03)")]
    public GameObject[] levelPrefabs;

    // Единый источник уровней: каталог, а если его нет — список в инспекторе
    private GameObject[] Levels => LevelCatalog.Current != null && LevelCatalog.Current.Count > 0 ? LevelCatalog.Current.levels : levelPrefabs;
    public int currentLevelIndex;

    [Header("Тестирование")]
    [Tooltip("Включите это, чтобы запустить конкретный уровень для теста")]
    public bool testSpecificLevel = false;
    [Tooltip("Индекс уровня для теста (0 - первый, 1 - второй, 2 - третий и т.д.)")]
    public int testLevelIndex = 0;

    [Header("Интерфейс (UI)")]
    public GameObject winPanel;
    public GameObject nextButton; 
    public GameObject restartButton; 
    public GameObject pausePanel;
    public GameObject losePanel;

    [Header("Камера")]
    [Tooltip("Запас по бокам уровня в мировых единицах: камера отдаляется, если уровень не влезает по ширине экрана")]
    [SerializeField] private float cameraSideMargin = 0.5f; // поле до бокового края кадра (правило расстановки: 0.5)

    [Header("Почти получилось (H-03)")]
    [SerializeField] private AlmostPanel almostPanel;

    [Header("Анимация Победы")]
    public UISequenceAnimator winSequenceAnimator; // Наш новый покадровый аниматор

    [Header("Статистика")]
    public int totalMatches = 0;
    public int burntMatches = 0;
    public int burningMatches = 0;
    
    [Header("Валюта (Золотые спички)")]
    public int goldenMatchesCollectedThisLevel = 0;
    
    private bool isPaused = false;
    public bool IsPaused => isPaused;

    /// <summary>Перед этой попыткой нажимали «Заново» (для задания «без Заново»); сбрасывается при переходе к следующему уровню.</summary>
    public static bool RestartPressed;
    /// <summary>Сейчас идёт уровень дня («Очаг»): прогресс уровней 1–32 не трогается, награда — от Retention.</summary>
    public bool IsDaily { get; private set; }
    private GameObject levelRoot;
    
    public bool isGameStarted = false;
    public bool isLevelEnded = false;

    void Awake()
    {
        Instance = this;
        // Уровень всегда стартует без паузы (на случай, если пауза включилась во время перехода)
        Time.timeScale = 1f;
        isPaused = false;
        isLevelEnded = false;
    }

    void Start()
    {
        if (winPanel != null) winPanel.SetActive(false);
        if (nextButton != null) nextButton.SetActive(false); 

        SpawnLevel();
    }

    private void SpawnLevel()
    {
        totalMatches = 0;
        burntMatches = 0;
        burningMatches = 0;
        goldenMatchesCollectedThisLevel = 0;
        isGameStarted = false;
        isLevelEnded = false;
        ChainAudio.Reset(); // «мелодия» цепи начинается заново
        LevelClock.Begin(); // часы капель и вентиляторов: расписание от появления уровня

        if (Retention.PlayingDaily && DailyLevelPool.Current != null)
        {
            GameObject daily = null;
            try { daily = DailyLevelPool.Current.LoadPrefab(Retention.PendingDaily); }
            catch (System.Exception e) { RetSafe.Report("daily level", e); }   // не загрузился — обычный уровень
            if (daily != null)
            {
                IsDaily = true;
                currentLevelIndex = -1;
                GameObject dl = Instantiate(daily, Vector3.zero, Quaternion.identity);
                levelRoot = dl;
                FitCameraToLevel(dl);
                PlayfieldBacking.Build(dl);
                MatchLogic m = dl.GetComponentInChildren<MatchLogic>();
                if (m != null) GameJuice.PrewarmFires(m.FirePrefab(out _), 6);
                if (HintController.Instance != null) HintController.Instance.Setup(-1, dl);
                return;
            }
            Retention.PendingDaily = -1;
        }

        if (testSpecificLevel)
        {
            currentLevelIndex = testLevelIndex;
            Debug.Log("Включен тестовый режим! Загружаем уровень по индексу: " + testLevelIndex);
        }
        else
        {
            currentLevelIndex = PlayerPrefs.GetInt("CurrentLevel", 0);
        }

        if (currentLevelIndex >= Levels.Length || currentLevelIndex < 0)
        {
            currentLevelIndex = 0;
            if (!testSpecificLevel) PlayerPrefs.SetInt("CurrentLevel", 0);
        }

        if (Levels.Length > 0 && Levels[currentLevelIndex] != null)
        {
            GameObject level = Instantiate(Levels[currentLevelIndex], Vector3.zero, Quaternion.identity);
            levelRoot = level;
            FitCameraToLevel(level);
            PlayfieldBacking.Build(level);
            // Огни из пула: несколько экземпляров заранее, чтобы первая цепочка не создавала их на лету
            MatchLogic anyMatch = level.GetComponentInChildren<MatchLogic>();
            if (anyMatch != null) GameJuice.PrewarmFires(anyMatch.FirePrefab(out _), 6);
            if (TutorialController.Instance != null) TutorialController.Instance.TryStart(currentLevelIndex, level);
            if (HintController.Instance != null) HintController.Instance.Setup(currentLevelIndex, level);
        }
    }

    // Уровни сверстаны под 9:16 и размер камеры 5. На вытянутых экранах (19.5:9, 20:9)
    // видимая ширина меньше и крайние спички уходили за край — отдаляем камеру ровно настолько,
    // чтобы уровень влез. По высоте уровень должен влезть между HUD (с вырезом) и жестовой панелью.
    // Меньше исходного размера камеру не делаем, позицию камеры не меняем.
    private void FitCameraToLevel(GameObject level)
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        // Что должно попасть в кадр — см. CameraFit.LevelContent
        if (!CameraFit.LevelContent(level, out Bounds content)) return;

        VisualTheme theme = VisualTheme.Current;
        CameraFit.ScreenReserves(theme, out float top, out float bottom);
        cam.orthographicSize = CameraFit.Size(content, cam.transform.position, cam.aspect, cam.orthographicSize,
            cameraSideMargin, theme.levelVerticalMargin, top, bottom, MaxCameraZoomOut);
    }

    // Не отдаляем камеру больше чем в 1.5 раза — дальше спички становятся слишком мелкими для пальца
    private const float MaxCameraZoomOut = 1.5f;

    public void NextLevel()
    {
        // Убрали отключение winPanel здесь, чтобы двери оставались закрытыми во время загрузки сцены!
        if (nextButton != null) nextButton.SetActive(false);

        if (IsDaily)
        {
            // Уровень дня один: после него — в меню, к очагу
            RestartPressed = false;
            Retention.PendingDaily = -1;
            AdsManager.OnLevelCompleted(daily: true);   // после уровня дня — окно награды, без межстраничной
            Time.timeScale = 1f;
            SceneLoadGuard.Load("MainScene");
            return;
        }

        // После последнего уровня раньше молча начинался Level_0 — теперь возвращаем к выбору уровня
        bool isLastLevel = currentLevelIndex + 1 >= Levels.Length;

        if (!testSpecificLevel && !isLastLevel)
        {
            PlayerPrefs.SetInt("CurrentLevel", currentLevelIndex + 1);
            PlayerPrefs.Save();
        }

        // Логическая пауза между уровнями — здесь реклама уместна (§4.4).
        // Показываем и сразу грузим уровень: ролик перекрывает загрузку,
        // а ждать закрытия нельзя — при неистёкшем интервале SDK молча
        // выходит и колбэк не приходит.
        AdsManager.OnLevelCompleted();

        if (isLastLevel)
        {
            AndroidToast.Show("All levels completed! New ones are coming soon",
                              "Все уровни пройдены! Скоро появятся новые");
            GoToLevelSelection();
        }
        else
        {
            RestartScene();
            RestartPressed = false;   // это не «Заново», а следующий уровень
        }
    }

    public void RegisterMatch()
    {
        totalMatches++;
    }

    public void MatchIgnited()
    {
        if (!isGameStarted) LevelClock.MarkFirstIgnite();
        burningMatches++;
        isGameStarted = true;
        ChainAudio.Advance();
    }

    public void MatchBurnt()
    {
        burntMatches++;
        burningMatches--;
        CheckWinCondition();
    }

    /// <summary>Спичку потушила вода или ветер: она больше не горит и не считается сгоревшей.</summary>
    public void MatchExtinguished()
    {
        burningMatches--;
        CheckWinCondition();
    }

    public void GoldenMatchBurnt()
    {
        goldenMatchesCollectedThisLevel++;
    }



    private void CheckWinCondition()
    {
        if (isLevelEnded) return;

        // Уровень заканчивается, когда игра началась, и больше нет горящих спичек
        if (isGameStarted && burningMatches <= 0)
        {
            isLevelEnded = true;
            Debug.Log("Огонь потух! Уровень завершен.");

            // Рекламу здесь НЕ показываем: ролик перебил бы анимацию победы
            // и начисление звёзд. Она вызывается из NextLevel(), когда игрок
            // сам нажал "Next" — это логическая пауза между уровнями (§4.4).

            // Звёзды по доле сгоревших спичек (H-03), пороги — VisualTheme
            int stars = VisualTheme.Current.StarsFor(burntMatches, totalMatches);
            Debug.Log("Звезд получено: " + stars);

            if (stars == 0)
            {
                // «Почти получилось»: без звёзд и монет, предлагаем попробовать снова
                ReportToRetention(false, 0);
                int goldsLost = goldenMatchesCollectedThisLevel;
                goldenMatchesCollectedThisLevel = 0;
                if (almostPanel != null) almostPanel.Show(burntMatches, totalMatches, goldsLost);
                else if (winPanel != null) winPanel.SetActive(true);
                GameJuice.OnLevelEnd(0, false);
                return;
            }

            ReportToRetention(true, stars);
            ShowWin(stars, true);
        }
    }

    /// <summary>Итог уровня для «Очага»: задания, серия, неделя, награда уровня дня. Старые ключи не трогает.</summary>
    private void ReportToRetention(bool won, int stars)
    {
        // Ошибка в «Очаге» не должна мешать экрану итога уровня
        try { ReportToRetentionUnsafe(won, stars); }
        catch (System.Exception e) { RetSafe.Report("level end", e); }
    }

    private void ReportToRetentionUnsafe(bool won, int stars)
    {
        if (!Retention.Active || levelRoot == null) return;
        RetTags tags = RetTags.None;
        if (levelRoot.GetComponentInChildren<WaterDripper>(true) != null) tags |= RetTags.Water;
        if (levelRoot.GetComponentInChildren<Fan>(true) != null) tags |= RetTags.Wind;
        if (levelRoot.GetComponentInChildren<Oscillator>(true) != null) tags |= RetTags.Pendulum;
        foreach (MatchLogic m in levelRoot.GetComponentsInChildren<MatchLogic>(true))
        {
            if (m.ignorePlayerMatch) tags |= RetTags.Immune;
            if (m.isGolden) tags |= RetTags.Gold;
        }
        if (!IsDaily && LevelCatalog.Current != null && LevelCatalog.Current.GetDifficulty(currentLevelIndex) >= 3) tags |= RetTags.Hard;
        if (IsDaily && DailyLevelPool.Current != null && Retention.PendingDaily >= 0
            && DailyLevelPool.Current.entries[Retention.PendingDaily].tier >= 2) tags |= RetTags.Hard;
        Retention.Instance.ReportLevelEnd(new RetLevelResult
        {
            won = won, stars = stars, burnt = burntMatches, total = totalMatches, golds = goldenMatchesCollectedThisLevel,
            tags = tags, restarted = RestartPressed, daily = IsDaily, levelIndex = IsDaily ? -1 : currentLevelIndex,
            seconds = LevelClock.HasIgnited ? LevelClock.Now - LevelClock.FirstIgniteTime : 0f,
        });
    }


    /// <summary>Победа с заданным числом звёзд (1–3): сохранение (record), анимация, монеты, звук и эффекты.</summary>
    private void ShowWin(int stars, bool record)
    {
        // Сохраняем звезды, если результат лучше предыдущего (не в отладочной победе)
        // Уровень дня — без записи в прогресс 1–32 и без монет за золотые (награду даёт «Очаг», раз в день)
        if (IsDaily) record = false;
        string levelKey = "Level_" + currentLevelIndex + "_Stars";
        int previousStars = PlayerPrefs.GetInt(levelKey, 0);
        if (record && stars > previousStars)
        {
            PlayerPrefs.SetInt(levelKey, stars);
            PlayerPrefs.Save();
        }

        if (nextButton != null) nextButton.SetActive(false); // Изначально скрываем кнопку Next

        // Новая система: Покадровая анимация (Image Sequence)
        if (winSequenceAnimator != null)
        {
            winSequenceAnimator.onAnimationComplete = () =>
            {
                // Уровень дня: вместо «Новый уровень» — карточка «Очага» с кнопками «В Очаг» и «В меню»
                if (nextButton != null && !IsDaily) nextButton.SetActive(true); // Показываем кнопку только после завершения анимации!
            };
            winSequenceAnimator.Play(stars);
        }
        else
        {
            if (nextButton != null && !IsDaily) nextButton.SetActive(true);
        }

        // Зачисляем только НОВЫЕ собранные золотые спички для этого уровня
        if (record && goldenMatchesCollectedThisLevel > 0)
        {
            string goldenKey = "Level_" + currentLevelIndex + "_GoldenMatches";
            int previousGolden = PlayerPrefs.GetInt(goldenKey, 0);
            
            if (goldenMatchesCollectedThisLevel > previousGolden)
            {
                int newGoldenToAdd = goldenMatchesCollectedThisLevel - previousGolden;
                CurrencyManager.AddGoldenMatches(newGoldenToAdd);
                Debug.Log("Добавлено новых золотых спичек в баланс: " + newGoldenToAdd);
                
                PlayerPrefs.SetInt(goldenKey, goldenMatchesCollectedThisLevel);
                PlayerPrefs.Save();
            }
            else
            {
                Debug.Log("Эти золотые спички уже были собраны ранее. Новых не добавлено.");
            }
            
            goldenMatchesCollectedThisLevel = 0;
        }

        if (winPanel != null) winPanel.SetActive(true);
        GameJuice.OnLevelEnd(stars, true); // тряска, конфетти, пружинки звёзд, звук и вибрация по звёздам
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>Отладка: показать итог уровня с заданным числом звёзд (0 — «Почти получилось»), без сохранения прогресса.</summary>
    public void DebugWin(int stars)
    {
        if (isLevelEnded) return;
        isLevelEnded = true;
        goldenMatchesCollectedThisLevel = 0;
        if (stars <= 0)
        {
            if (almostPanel != null) almostPanel.Show(burntMatches, Mathf.Max(1, totalMatches));
            GameJuice.OnLevelEnd(0, false);
            return;
        }
        ShowWin(Mathf.Clamp(stars, 1, 3), false);
    }

    [ContextMenu("Debug: итог 0★ (почти)")] private void DebugWin0() => DebugWin(0);
    [ContextMenu("Debug: победа 1★")] private void DebugWin1() => DebugWin(1);
    [ContextMenu("Debug: победа 2★")] private void DebugWin2() => DebugWin(2);
    [ContextMenu("Debug: победа 3★")] private void DebugWin3() => DebugWin(3);
#endif

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (SceneLoadGuard.IsTransitioning) return;   // двери закрываются/открываются — «Назад» ждёт
            if (isLevelEnded)
            {
                GoToLevelSelection();
            }
            else
            {
                TogglePause();
            }
        }
    }

#if !UNITY_WEBGL
    // Свернули приложение, входящий звонок, блокировка экрана — ставим уровень на паузу,
    // чтобы после возвращения игрок не увидел наполовину догоревшую цепочку.
    // В WebGL паузой при потере фокуса и рекламе управляет плагин Яндекс Игр.
    private void OnApplicationPause(bool paused)
    {
        if (paused && !isPaused && !isLevelEnded && !SceneLoadGuard.IsTransitioning) TogglePause();
    }
#endif

    public void TogglePause()
    {
        // Если уровень уже закончен (победа), на паузу ставить нельзя; во время перехода между сценами — тоже
        if (isLevelEnded) return;
        if (!isPaused && SceneLoadGuard.IsTransitioning) return;

        isPaused = !isPaused;
        
        if (isPaused)
        {
            Time.timeScale = 0f; // Останавливаем время в игре (физику и анимации)
            if (pausePanel != null) pausePanel.SetActive(true);
        }
        else
        {
            Time.timeScale = 1f; // Запускаем время обратно
            if (pausePanel != null) pausePanel.SetActive(false);
        }
    }

    public void RestartScene()
    {
        RestartPressed = true;
        Time.timeScale = 1f; // Обязательно возвращаем время перед перезагрузкой
        SceneLoadGuard.Load(SceneManager.GetActiveScene().name);
    }

    public void GoToLevelSelection()
    {
        Time.timeScale = 1f; // Обязательно возвращаем время перед выходом
        RestartPressed = false;
        if (IsDaily) { Retention.PendingDaily = -1; SceneLoadGuard.Load("MainScene"); return; }
        SceneLoadGuard.Load("LevelSelectionScene");
    }
}
