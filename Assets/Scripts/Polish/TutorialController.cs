using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Онбординг (M-10): анимированная «рука» показывает действие один раз.
/// Level_0 (слот): «перетащи спичку в слот»; первый уровень со свободной расстановкой: «тапни, чтобы повернуть».
/// Флаги показа — новые ключи PlayerPrefs Tutorial_Drag_Done / Tutorial_Rotate_Done (старые ключи не трогаются).
/// Исчезает при первом соответствующем действии игрока или по кнопке «Пропустить».
/// </summary>
public class TutorialController : MonoBehaviour
{
    public const string DragKey = "Tutorial_Drag_Done";
    public const string RotateKey = "Tutorial_Rotate_Done";

    public enum Kind { None, Drag, Rotate }

    public static TutorialController Instance { get; private set; }

    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform hand;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Button skipButton;
    [SerializeField] private TMP_Text skipLabel;

    private Kind kind = Kind.None;
    private Transform fromTarget;
    private Vector3 fromOffset, toWorld;
    private float time;

    private void Awake()
    {
        Instance = this;
        if (skipButton != null) skipButton.onClick.AddListener(Finish);
        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Вызывается GameManager после создания уровня.</summary>
    public void TryStart(int levelIndex, GameObject level)
    {
        if (!VisualTheme.Current.tutorials || level == null) return;
        DraggableMatch player = level.GetComponentInChildren<DraggableMatch>(true);
        if (player == null) return;
        MatchLogic logic = player.GetComponent<MatchLogic>();
        bool ru = LanguageInit.CurrentLang == "ru";

        if (!player.freePlacementMode && levelIndex == 0 && PlayerPrefs.GetInt(DragKey, 0) == 0)
        {
            Collider2D slot = FindSlot(level);
            if (slot == null) return;
            kind = Kind.Drag;
            toWorld = slot.transform.position;
            if (label != null) label.text = ru ? "Перетащи спичку в слот" : "Drag the match into the slot";
        }
        else if (player.freePlacementMode && PlayerPrefs.GetInt(RotateKey, 0) == 0)
        {
            kind = Kind.Rotate;
            if (label != null) label.text = ru ? "Тапни по спичке, чтобы повернуть" : "Tap the match to rotate it";
        }
        else return;

        fromTarget = player.transform;
        fromOffset = (logic != null ? GameJuice.Center(logic) : player.transform.position) - player.transform.position;
        if (skipLabel != null) skipLabel.text = ru ? "Пропустить" : "Skip";
        time = 0f;
        gameObject.SetActive(true);
        if (group != null) { group.alpha = 1f; group.blocksRaycasts = true; }
    }

    /// <summary>Игрок сделал действие: если это то, чему учим, — урок пройден.</summary>
    public static void Notify(Kind action)
    {
        if (Instance != null && Instance.kind == action) Instance.Finish();
    }

    private void Finish()
    {
        if (kind == Kind.Drag) PlayerPrefs.SetInt(DragKey, 1);
        if (kind == Kind.Rotate) PlayerPrefs.SetInt(RotateKey, 1);
        PlayerPrefs.Save();
        kind = Kind.None;
        Hide();
    }

    private void Hide()
    {
        if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; }
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (kind == Kind.None || fromTarget == null || hand == null || Camera.main == null) return;
        time += Time.unscaledDeltaTime;
        float loop = Mathf.Max(0.5f, VisualTheme.Current.tutorialLoopDuration);
        float p = (time % loop) / loop;
        Vector3 from = fromTarget.position + fromOffset;
        var handImage = hand.GetComponent<Graphic>();

        if (kind == Kind.Drag)
        {
            // 0–0.15 нажатие, 0.15–0.75 тянем к слоту, 0.75–0.9 отпускаем, 0.9–1 исчезаем
            float move = Ease.Evaluate(Ease.Type.InOutSine, Mathf.InverseLerp(0.15f, 0.75f, p));
            Place(Vector3.Lerp(from, toWorld, move));
            float press = p < 0.15f ? Mathf.InverseLerp(0f, 0.15f, p) : p > 0.75f ? 1f - Mathf.InverseLerp(0.75f, 0.9f, p) : 1f;
            hand.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.9f, press);
            if (handImage != null) SetAlpha(handImage, p > 0.9f ? 1f - Mathf.InverseLerp(0.9f, 1f, p) : Mathf.Min(1f, p / 0.08f));
        }
        else
        {
            // Два коротких тапа по спичке
            Place(from);
            float tap = Mathf.Abs(Mathf.Sin(p * Mathf.PI * 2f));
            hand.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.15f, tap);
            if (handImage != null) SetAlpha(handImage, 0.6f + 0.4f * tap);
        }
    }

    private void Place(Vector3 world)
    {
        var parent = (RectTransform)hand.parent;
        Vector2 screen = Camera.main.WorldToScreenPoint(world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out Vector2 local);
        hand.anchoredPosition = local + new Vector2(30f, -40f); // «палец» чуть ниже и правее точки
    }

    private static void SetAlpha(Graphic g, float a)
    {
        Color c = g.color;
        c.a = a;
        g.color = c;
    }

    private static Collider2D FindSlot(GameObject level)
    {
        int slotLayer = LayerMask.NameToLayer("Slot");
        foreach (Collider2D col in level.GetComponentsInChildren<Collider2D>(false))
            if (col.gameObject.layer == slotLayer) return col;
        return null;
    }
}
