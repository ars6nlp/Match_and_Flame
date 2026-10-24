using UnityEngine;

/// <summary>
/// Подсказка за rewarded-рекламу: на несколько секунд показывает полупрозрачный силуэт
/// спички игрока в задуманной позиции. Уровни со слотом: поза из слота; free-уровни: из LevelSolutions.
/// Реклама — существующий AdsManager.ShowRewardedAd (награда только после досмотра).
/// Если подсказку купили на панели «Почти получилось», она покажется в начале следующей попытки.
/// </summary>
public class HintController : MonoBehaviour
{
    public static HintController Instance { get; private set; }

    private const int NoPending = int.MinValue;   // -1 — индекс уровня дня, его нельзя брать как «нет»
    private static int pendingLevel = NoPending;

    private int levelIndex = -1;
    private GameObject level;
    private SpriteRenderer ghost;
    private float shownFor = -1f;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Setup(int index, GameObject levelRoot)
    {
        levelIndex = index;
        level = levelRoot;
        if (pendingLevel == index)
        {
            pendingLevel = NoPending;
            Show();
        }
    }

    // Подсказка нужна, пока спичка игрока не поставлена (или уровень уже закончен — тогда на следующую попытку)
    public bool Available =>
        VisualTheme.Current.hints && level != null && TryGetPose(out _, out _, out DraggableMatch player)
        && (!player.IsPlaced || (GameManager.Instance != null && GameManager.Instance.isLevelEnded));

    /// <summary>Кнопка «Подсказка»: реклама, после досмотра — силуэт (или на следующей попытке).</summary>
    public void RequestHint()
    {
        if (!Available) return;
        AdsManager.ShowRewardedAd(() =>
        {
            if (GameManager.Instance != null && GameManager.Instance.isLevelEnded) pendingLevel = levelIndex;
            else
            {
                // Подсказку купили из меню паузы: снимаем паузу, иначе силуэт отыграет 3 с за панелью (M-01)
                if (GameManager.Instance != null && GameManager.Instance.IsPaused) GameManager.Instance.TogglePause();
                Show();
            }
        });
    }

    private void Show()
    {
        if (!TryGetPose(out Vector3 position, out Quaternion rotation, out DraggableMatch player)) return;
        var playerSprite = player.GetComponent<SpriteRenderer>();
        if (playerSprite == null) return;

        if (ghost == null)
        {
            ghost = new GameObject("HintGhost").AddComponent<SpriteRenderer>();
            ghost.sortingOrder = playerSprite.sortingOrder + 2;
        }
        ghost.sprite = playerSprite.sprite;
        ghost.transform.SetPositionAndRotation(position, rotation);
        ghost.transform.localScale = player.transform.lossyScale;
        ghost.enabled = true;
        shownFor = 0f;
    }

    private void Update()
    {
        if (shownFor < 0f || ghost == null) return;
        shownFor += Time.unscaledDeltaTime;
        VisualTheme theme = VisualTheme.Current;
        if (shownFor >= theme.hintDuration)
        {
            ghost.enabled = false;
            shownFor = -1f;
            return;
        }
        Color c = theme.cream;
        c.a = theme.hintAlpha * (0.6f + 0.4f * Mathf.Sin(shownFor * 6f));
        ghost.color = c;
    }

    // Поза корня спички игрока в задуманном решении
    private bool TryGetPose(out Vector3 position, out Quaternion rotation, out DraggableMatch player)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        player = level != null ? level.GetComponentInChildren<DraggableMatch>(true) : null;
        if (player == null) return false;

        if (player.freePlacementMode)
        {
            if (LevelSolutions.Current == null || !LevelSolutions.Current.TryGet(levelIndex, out LevelSolutions.Solution s)) return false;
            position = s.position;
            rotation = Quaternion.Euler(0f, 0f, s.rotationZ);
            return true;
        }

        // Слот: та же поза, что выставляет DraggableMatch при установке в слот
        int slotLayer = LayerMask.NameToLayer("Slot");
        foreach (Collider2D col in level.GetComponentsInChildren<Collider2D>(false))
        {
            if (col.gameObject.layer != slotLayer) continue;
            rotation = col.transform.rotation;
            Collider2D own = player.GetComponent<Collider2D>();
            Vector3 centerOffset = own != null ? rotation * Vector3.Scale(own.offset, player.transform.lossyScale) : Vector3.zero;
            position = col.transform.position - centerOffset;
            return true;
        }
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => pendingLevel = NoPending;
}
