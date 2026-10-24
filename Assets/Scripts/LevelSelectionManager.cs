using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LevelSelectionManager : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Запасное число уровней, если нет Assets/Resources/LevelCatalog.asset (основной источник — каталог)")]
    public int totalLevels = 5;

    [Header("Ссылки")]
    [Tooltip("Префаб вашей кнопки уровня")]
    public GameObject levelButtonPrefab;

    [Tooltip("Куда будем складывать кнопки (ваш GridContainer)")]
    public Transform gridContainer;

    void Start()
    {
        GenerateLevelButtons();
    }

    // Единый источник числа уровней — каталог (L-03)
    private int LevelCount =>
        LevelCatalog.Current != null && LevelCatalog.Current.Count > 0 ? LevelCatalog.Current.Count : totalLevels;

    private static int Stars(int index) => PlayerPrefs.GetInt("Level_" + index + "_Stars", 0);

    void GenerateLevelButtons()
    {
        VisualTheme theme = VisualTheme.Current;
        int count = LevelCount;

        // Текущий уровень — первый без звёзд (следующий, который стоит пройти)
        int current = -1;
        for (int i = 0; i < count && current < 0; i++)
            if (Stars(i) == 0) current = i;

        // Главы «Очага»: та же сетка, разбитая на группы с заголовками (индексы уровней не меняются)
        Transform[] parents = ChapterUI.Enabled ? ChapterUI.Build(gridContainer, count) : null;

        for (int i = 0; i < count; i++)
        {
            // Создаем новую кнопку внутри GridContainer (или в сетке своей главы)
            GameObject newButton = Instantiate(levelButtonPrefab, parents != null ? parents[i] : gridContainer);
            newButton.SetActive(true);

            // Получаем скрипт LevelButton
            LevelButton buttonScript = newButton.GetComponent<LevelButton>();

            if (buttonScript != null)
            {
                bool locked = theme.levelLock && i > 0 && Stars(i - 1) == 0;
                int difficulty = LevelCatalog.Current != null ? LevelCatalog.Current.GetDifficulty(i) : 0;
                buttonScript.Setup(i, i == current, locked, difficulty, i * theme.cardCascadeDelay);

                // Автоматически подключаем функцию OnClick к нажатию на кнопку
                Button uiButton = newButton.GetComponent<Button>();
                if (uiButton != null)
                {
                    uiButton.onClick.AddListener(buttonScript.OnClick);
                }
            }
        }
        ChapterUI.MaybeIntro(current);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (ChapterIntro.CloseOpen()) return;   // «Назад» сначала закрывает вступление главы
            GoToMainMenu();
        }
    }

    public void GoToMainMenu()
    {
        SceneLoadGuard.Load("MainScene");
    }
}
