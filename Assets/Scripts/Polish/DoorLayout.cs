using UnityEngine;

/// <summary>
/// Раскладка двух створок дверей якорями — в долях родителя, а не в пикселях, поэтому закрытые створки перекрывают
/// экран на любом соотношении сторон и при любом масштабе Canvas (Expand). closed: 0 — открыто, 1 — закрыто.
/// Закрытые створки заходят друг за друга в центре на overlap ширины экрана и выступают за верх/низ на bleed.
/// </summary>
public static class DoorLayout
{
    /// <summary>Доля ширины экрана, на которую створка уезжает за край в открытом состоянии (чтобы шов и тень тоже ушли).</summary>
    private const float OffscreenExtra = 0.06f;

    public static float LeafWidth(float overlap) => 0.5f + overlap * 0.5f;

    public static void Apply(RectTransform left, RectTransform right, float closed, float overlap, float bleed = 0.02f)
    {
        float w = LeafWidth(overlap);
        float shift = (1f - Mathf.Clamp01(closed)) * (w + OffscreenExtra);
        if (left != null) Set(left, -shift, w - shift, bleed);
        if (right != null) Set(right, 1f - w + shift, 1f + shift, bleed);
    }

    private static void Set(RectTransform rt, float xMin, float xMax, float bleed)
    {
        rt.anchorMin = new Vector2(xMin, -bleed);
        rt.anchorMax = new Vector2(xMax, 1f + bleed);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f); // правая створка зеркалится scale.x = −1 вокруг центра — прямоугольник тот же
    }

    /// <summary>Плавное начало и конец с лёгким «доводом» в конце (без перелёта за 1 — перелёт открыл бы щель).</summary>
    public static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
    }
}
