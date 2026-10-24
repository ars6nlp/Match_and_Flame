using TMPro;
using UnityEngine;

/// <summary>
/// Мягкая подложка под текстом подсказки на поле: размер подгоняется под текст (RU/EN разной длины)
/// только когда текст меняется. Границы текста с подложкой учитываются в кадре камеры (CameraFit.LevelContent).
/// Цвет и запас — VisualTheme («Игра: кадр уровня»).
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class WorldHintBacking : MonoBehaviour
{
    [SerializeField] private SpriteRenderer backing;
    [Tooltip("Иконка механики слева от текста внутри подложки (необязательно)")]
    [SerializeField] private Transform icon;
    // Место под иконку слева от текста, мировые единицы
    private const float IconSpace = 0.5f;
    // Подложка рисуется в половинном масштабе, чтобы скругления 9-slice были тоньше
    private const float BackingScale = 0.5f;
    private static readonly Vector3[] Corners = new Vector3[4];

    private TMP_Text text;
    private string lastText;

    /// <summary>Мировые границы текста с подложкой (до раскладки текста — рамка RectTransform).</summary>
    public Bounds WorldRect
    {
        get
        {
            if (text == null) text = GetComponent<TMP_Text>();
            text.ForceMeshUpdate();
            Bounds local = Content();
            if (local.size.x > 0f)
            {
                Vector2 pad = VisualTheme.Current.hintBackingPadding;
                local.Expand(new Vector3(2f * pad.x, 2f * pad.y, 0f));
                Corners[0] = transform.TransformPoint(local.min);
                Corners[1] = transform.TransformPoint(local.max);
                Corners[2] = Corners[0];
                Corners[3] = Corners[1];
            }
            else ((RectTransform)transform).GetWorldCorners(Corners);
            var b = new Bounds(Corners[0], Vector3.zero);
            for (int i = 1; i < 4; i++) b.Encapsulate(Corners[i]);
            return b;
        }
    }

    private void Start()
    {
        text = GetComponent<TMP_Text>();
        if (backing != null) backing.color = VisualTheme.Current.hintBackingColor;
    }

    private void LateUpdate()
    {
        if (backing == null || text == null || ReferenceEquals(text.text, lastText)) return;
        lastText = text.text;
        Refit();
    }

    // Текст и место под иконку слева от него (локальные координаты)
    private Bounds Content()
    {
        Bounds b = text.textBounds;
        if (icon != null && b.size.x > 0f) b.SetMinMax(b.min - new Vector3(IconSpace, 0f, 0f), b.max);
        return b;
    }

    public void Refit()
    {
        if (text == null) text = GetComponent<TMP_Text>();
        text.ForceMeshUpdate();
        Bounds b = Content();
        backing.enabled = b.size.x > 0f;
        if (!backing.enabled) return;
        if (icon != null) icon.localPosition = new Vector3(b.min.x + IconSpace / 2f, b.center.y, 0f);
        Vector2 pad = VisualTheme.Current.hintBackingPadding;
        backing.transform.localPosition = new Vector3(b.center.x, b.center.y, 0.01f);
        backing.transform.localScale = new Vector3(BackingScale, BackingScale, 1f);
        backing.size = new Vector2(b.size.x + 2f * pad.x, b.size.y + 2f * pad.y) / BackingScale;
    }
}
