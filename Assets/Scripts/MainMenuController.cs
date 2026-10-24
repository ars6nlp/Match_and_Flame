using UnityEngine;
using UnityEngine.SceneManagement; 
using TMPro;

public class MainMenuController : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject mainPanel;
    [Tooltip("Если пусто — берётся ShopManager с этого же объекта")]
    [SerializeField] private ShopManager shopManager;

    // Выход только по второму нажатию «Назад» в течение этого времени
    private const float ExitConfirmWindow = 2f;
    private float lastBackPressTime = float.NegativeInfinity;

    private void Awake()
    {
        if (shopManager == null) shopManager = GetComponent<ShopManager>();
        if (shopManager == null) shopManager = FindFirstObjectByType<ShopManager>();
    }

    private void Start()
    {
        AdsManager.OnMenuEntered();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Окна «Очага» закрываются первыми — по стеку
            try { if (RetentionMenuUI.HandleBack()) return; }
            catch (System.Exception e) { RetSafe.Report("back", e); }
            // Кнопка Назад закрывает открытую панель: сначала магазин, потом настройки
            if (shopManager != null && shopManager.IsOpen)
            {
                shopManager.CloseShop();
            }
            else if (settingsPanel != null && settingsPanel.activeSelf)
            {
                CloseSettings();
            }
#if !UNITY_WEBGL || UNITY_EDITOR
            else if (Time.unscaledTime - lastBackPressTime <= ExitConfirmWindow)
            {
                // Иначе выходим из игры полностью — но только по второму нажатию.
                // В WebGL этого делать нельзя: Application.Quit() выгружает инстанс Unity,
                // и игрок, случайно нажавший Esc, получает чёрный экран.
                Debug.Log("Выход из игры по кнопке Назад");
                Application.Quit();
            }
            else
            {
                lastBackPressTime = Time.unscaledTime;
                ShowExitHint();
            }
#endif
        }
    }

    private static void ShowExitHint()
    {
        AndroidToast.Show("Press Back again to exit", "Нажмите «Назад» ещё раз, чтобы выйти");
    }

    public void StartGame()
    {
        Debug.Log("Game started... Loading level 1.");
        SceneLoadGuard.Load(SceneManager.GetActiveScene().buildIndex + 1);
    }

    public void OpenSettings()
    {
        Debug.Log("Opening settings.");
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            if (mainPanel != null) mainPanel.SetActive(false);
        }
    }

    public void CloseSettings()
    {
        Debug.Log("Closing settings.");
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
            if (mainPanel != null) mainPanel.SetActive(true);
        }
    }
}
