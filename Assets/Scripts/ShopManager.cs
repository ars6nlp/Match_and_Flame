using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

public class ShopManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject shopPanel;

    [Header("UI & References")]
    public SmoothVideoLoader videoLoader;
    public Image equipButtonImage; 
    public TextMeshProUGUI priceText; // Текст для цены
    
    [Header("Button Sprites")]
    public Sprite equipSprite;
    public Sprite equippedSprite;
    public Sprite buySprite; // Спрайт для покупки
    public Sprite watchAdSprite; // Спрайт для просмотра рекламы (опционально)

    [Header("Button Sprites — русские варианты")]
    [Tooltip("Если поле пустое, останется английский спрайт")]
    public Sprite equipSpriteRU;
    public Sprite equippedSpriteRU;
    public Sprite buySpriteRU;
    public Sprite watchAdSpriteRU;

    /// <summary>
    /// Выбирает спрайт под текущий язык платформы.
    /// Спрайты кнопки переключаются в рантайме по состоянию скина,
    /// поэтому LocalizeImage здесь не подходит — язык учитываем прямо тут.
    /// </summary>
    private Sprite Loc(Sprite en, Sprite ru)
    {
        if (LanguageInit.CurrentLang == "ru" && ru != null) return ru;
        return en != null ? en : ru;
    }

    [Header("Skins (Videos)")]
    public VideoClip[] skinVideos;
    public int[] skinPrices; // Цены на скины (например: 0, 3, 7, 10)
    [Tooltip("Индекс скина, который открывается за просмотр рекламы. -1 — последний скин")]
    public int adSkinIndex = -1;

    [Header("Sequence Skin Animator (Новая система)")]
    public UISkinSequenceAnimator skinSequenceAnimator;

    [Header("Эффекты магазина (polish)")]
    [SerializeField] private ShopFX fx;

    [Header("Кнопка-спичка: надпись и цена отдельно (без наложения на текст спрайта)")]
    [SerializeField] private ShopActionButton actionButton;
    private float adRequestBlockedUntil;
    public ShopActionButton ActionButton => actionButton;

    private int currentIndex = 0;
    private int equippedIndex = 0;

    private const string EQUIPPED_SKIN_KEY = "EquippedFireSkin";

    private void OnEnable() { CurrencyManager.OnCurrencyChanged += UpdateUI; }
    private void OnDisable() { CurrencyManager.OnCurrencyChanged -= UpdateUI; }

    void Start()
    {
        if (shopPanel != null) shopPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);

        equippedIndex = PlayerPrefs.GetInt(EQUIPPED_SKIN_KEY, 0);
        
        int totalSkins = GetTotalSkins();
        if (equippedIndex >= totalSkins || equippedIndex < 0) 
            equippedIndex = 0;

        // Первый скин всегда разблокирован
        UnlockSkin(0);

        currentIndex = equippedIndex;

        ApplySkin(currentIndex);
        UpdateUI();
    }

    public void OpenShop()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(true);

        currentIndex = equippedIndex;
        ChangePreview();
    }

    public bool IsOpen => shopPanel != null && shopPanel.activeSelf;

    public void CloseShop()
    {
        if (shopPanel != null) shopPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);

        ApplySkin(equippedIndex);
    }

    public void NextSkin()
    {
        int totalSkins = GetTotalSkins();
        if (totalSkins == 0) return;
        
        currentIndex++;
        if (currentIndex >= totalSkins) currentIndex = 0;
        
        ChangePreview();
    }

    public void PreviousSkin()
    {
        int totalSkins = GetTotalSkins();
        if (totalSkins == 0) return;
        
        currentIndex--;
        if (currentIndex < 0) currentIndex = totalSkins - 1;
        
        ChangePreview();
    }

    private void ChangePreview()
    {
        ApplySkin(currentIndex);
        UpdateUI();
    }

    private void ApplySkin(int index)
    {
        if (skinSequenceAnimator != null)
        {
            skinSequenceAnimator.SetSkin(index);
        }
        else if (videoLoader != null && skinVideos != null && skinVideos.Length > 0)
        {
            videoLoader.ChangeVideo(skinVideos[index]);
        }
    }

    // Базовые скины (свои кадры огня) + оттеночные из VisualTheme (те же кадры, другой цвет)
    private int GetTotalSkins()
    {
        int baseCount = GetBaseSkinCount();
        VisualTheme theme = VisualTheme.Current;
        return baseCount + (baseCount > 0 && theme.tintSkins != null ? theme.tintSkins.Length : 0);
    }

    private int GetBaseSkinCount()
    {
        if (skinSequenceAnimator != null && skinSequenceAnimator.skins != null && skinSequenceAnimator.skins.Length > 0)
            return skinSequenceAnimator.skins.Length;
        if (skinVideos != null)
            return skinVideos.Length;
        return 0;
    }

    // Рекламный скин раньше определялся по длине skinVideos — массива видео, которые
    // больше не проигрываются; очистка массива сделала бы его покупным за монеты.
    private bool IsAdSkin(int index)
    {
        // -1 — последний из БАЗОВЫХ скинов: оттеночные добавлены после него и покупаются за монеты
        int adIndex = adSkinIndex >= 0 ? adSkinIndex : GetBaseSkinCount() - 1;
        return index == adIndex;
    }

    // Вызывается при нажатии на кнопку Equip/Buy
    public void OnActionButtonClick()
    {
        if (IsSkinUnlocked(currentIndex))
        {
            // Скин куплен - Экипируем
            equippedIndex = currentIndex;
            PlayerPrefs.SetInt(EQUIPPED_SKIN_KEY, equippedIndex);
            PlayerPrefs.Save();
            
            UpdateUI();
            CloseShop();
        }
        else
        {
            // Скин НЕ куплен - пытаемся купить
            
            // ПРОВЕРЯЕМ: если это самый последний скин, то он за видео
            if (IsAdSkin(currentIndex))
            {
                if (Time.unscaledTime < adRequestBlockedUntil) return;   // двойной тап по «Смотреть»
                adRequestBlockedUntil = Time.unscaledTime + 2f;
                int skin = currentIndex;   // какой скин выбран в момент нажатия, а не когда пришла награда
                AdsManager.ShowRewardedAd(() =>
                {
                    UnlockSkin(skin);
                    UpdateUI();
                    if (fx != null) fx.PlayPurchase();
                });
                return; // Выходим из метода, монеты не списываем
            }

            int price = GetSkinPrice(currentIndex);
            
            // Списываем валюту, если ее хватает
            if (CurrencyManager.SpendGoldenMatches(price))
            {
                UnlockSkin(currentIndex);
                UpdateUI(); // Обновляем кнопку
                if (fx != null) fx.PlayPurchase();
            }
            else
            {
                // Не хватает золотых спичек: трясём цену вместо молчаливого лога (L-08)
                if (fx != null) fx.ShakePrice();
            }
        }
    }

    private void UpdateUI()
    {
        bool isUnlocked = IsSkinUnlocked(currentIndex);

        // Новая кнопка: пустая спичка, надпись и бейдж цены раскладываются кодом и не пересекаются
        if (actionButton != null)
        {
            ShopActionButton.State state = !isUnlocked
                ? (IsAdSkin(currentIndex) ? ShopActionButton.State.WatchAd : ShopActionButton.State.Buy)
                : currentIndex == equippedIndex ? ShopActionButton.State.Equipped : ShopActionButton.State.Equip;
            int price = GetSkinPrice(currentIndex);
            actionButton.Show(state, price, CurrencyManager.GetGoldenMatches() >= price);
            return;
        }

        if (equipButtonImage != null)
        {
            if (!isUnlocked)
            {
                // НЕ КУПЛЕНО
                bool isAdSkin = IsAdSkin(currentIndex);

                if (isAdSkin)
                {
                    // Логика кнопки для рекламного скина
                    Sprite ad = Loc(watchAdSprite, watchAdSpriteRU);
                    if (ad != null) equipButtonImage.sprite = ad;
                    else
                    {
                        Sprite buyFallback = Loc(buySprite, buySpriteRU); // Фолбек
                        if (buyFallback != null) equipButtonImage.sprite = buyFallback;
                    }

                    if (priceText != null)
                    {
                        priceText.gameObject.SetActive(true);
                        // Показываем пометку рекламы вместо цены
                        priceText.text = LanguageInit.CurrentLang == "ru" ? "РЕКЛАМА" : "AD";
                    }
                }
                else
                {
                    // Логика кнопки для обычного скина за монеты
                    Sprite buy = Loc(buySprite, buySpriteRU);
                    if (buy != null) equipButtonImage.sprite = buy;

                    if (priceText != null)
                    {
                        priceText.gameObject.SetActive(true);
                        priceText.text = GetSkinPrice(currentIndex).ToString();
                    }
                }
            }
            else if (currentIndex == equippedIndex)
            {
                // ЭКИПИРОВАНО
                Sprite equipped = Loc(equippedSprite, equippedSpriteRU);
                if (equipped != null) equipButtonImage.sprite = equipped;
                if (priceText != null) priceText.gameObject.SetActive(false);
            }
            else
            {
                // КУПЛЕНО, НО НЕ ЭКИПИРОВАНО
                Sprite equip = Loc(equipSprite, equipSpriteRU);
                if (equip != null) equipButtonImage.sprite = equip;
                if (priceText != null) priceText.gameObject.SetActive(false);
            }
        }
    }

    private bool IsSkinUnlocked(int index)
    {
        return PlayerPrefs.GetInt("SkinUnlocked_" + index, 0) == 1;
    }

    private void UnlockSkin(int index)
    {
        PlayerPrefs.SetInt("SkinUnlocked_" + index, 1);
        PlayerPrefs.Save();
    }

    private int GetSkinPrice(int index)
    {
        // Оттеночные скины: цена из VisualTheme
        VisualTheme.FlameTint tint = VisualTheme.Current.GetTint(index, GetBaseSkinCount());
        if (tint != null) return tint.price;

        if (skinPrices == null || index < 0 || index >= skinPrices.Length)
        {
            return 0; 
        }
        return skinPrices[index];
    }
}
