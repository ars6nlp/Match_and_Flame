using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelButton : MonoBehaviour
{
    [Header("Настройки UI")]
    [SerializeField] private int levelIndex; // Номер уровня, который меняем в Инспекторе
    [SerializeField] private Text levelText; // Старая подпись (legacy Text)
    [Tooltip("Новая подпись на TextMeshPro; если назначена, legacy-текст можно выключить")]
    [SerializeField] private TMP_Text levelLabel;

    [Header("Спрайты состояний")]
    [SerializeField] private Image buttonImage; // Картинка самой кнопки
    [SerializeField] private Sprite notPassedSprite; // Не пройден (0 звезд)
    [SerializeField] private Sprite oneStarSprite;   // 1 звезда
    [SerializeField] private Sprite twoStarsSprite;  // 2 звезды
    [SerializeField] private Sprite threeStarsSprite;// 3 звезды

    [Header("Оформление (polish)")]
    [Tooltip("Подсветка текущего уровня (следующего непройденного)")]
    [SerializeField] private GameObject currentGlow;
    [Tooltip("Огоньки сложности: показываются первые 1–3")]
    [SerializeField] private Image[] difficultyMarks;

    private bool locked;

    // Метод, который мы вызовем при нажатии
    public void OnClick()
    {
        if (locked) return;
        // Уже грузим уровень по другой кнопке — не перезаписываем CurrentLevel
        if (SceneLoadGuard.IsLoading) return;

        // Сохраняем выбранный уровень напрямую в память
        PlayerPrefs.SetInt("CurrentLevel", levelIndex);
        PlayerPrefs.Save();

        // Переходим в игру
        SceneLoadGuard.Load("GameScene");
    }

    // Старый вызов: без оформления
    public void Setup(int index) => Setup(index, false, false, 0, -1f);

    /// <summary>
    /// Настройка карточки. difficulty 0 — не показывать огоньки; appearDelay меньше 0 — без анимации появления.
    /// </summary>
    public void Setup(int index, bool isCurrent, bool isLocked, int difficulty, float appearDelay)
    {
        levelIndex = index;
        locked = isLocked;
        string label = (index + 1).ToString(); // игроку показываем с единицы, индекс остаётся с нуля
        if (levelText != null) levelText.text = label;
        if (levelLabel != null) levelLabel.text = label;

        // Проверяем, сколько звезд получено за этот уровень
        int earnedStars = PlayerPrefs.GetInt("Level_" + index + "_Stars", 0);

        if (buttonImage != null)
        {
            if (earnedStars == 3 && threeStarsSprite != null)
            {
                buttonImage.sprite = threeStarsSprite;
            }
            else if (earnedStars == 2 && twoStarsSprite != null)
            {
                buttonImage.sprite = twoStarsSprite;
            }
            else if (earnedStars == 1 && oneStarSprite != null)
            {
                buttonImage.sprite = oneStarSprite;
            }
            else if (earnedStars == 0 && notPassedSprite != null)
            {
                buttonImage.sprite = notPassedSprite;
            }
        }

        ApplyLook(isCurrent, difficulty, appearDelay);
    }

    private void ApplyLook(bool isCurrent, int difficulty, float appearDelay)
    {
        VisualTheme theme = VisualTheme.Current;

        Button button = GetComponent<Button>();
        if (button != null) button.interactable = !locked;
        if (locked)
        {
            float k = theme.lockedCardBrightness;
            if (buttonImage != null) buttonImage.color = new Color(k, k, k, 1f);
            if (levelLabel != null) levelLabel.alpha = k;
        }

        if (currentGlow != null) currentGlow.SetActive(isCurrent && !locked);
        if (isCurrent && !locked)
        {
            UIPulse pulse = GetComponent<UIPulse>();
            if (pulse == null) pulse = gameObject.AddComponent<UIPulse>();
            pulse.amplitude = theme.pulseAmplitude * 0.8f;
        }

        if (difficultyMarks != null)
            for (int i = 0; i < difficultyMarks.Length; i++)
                if (difficultyMarks[i] != null) difficultyMarks[i].gameObject.SetActive(i < difficulty);

        if (appearDelay >= 0f && theme.panelAnimation)
            UIScaleDriver.For(this).PlayAppear(0.6f, theme.cardAppearDuration, null, appearDelay, Ease.Type.OutBack, 1.6f);
    }
}
