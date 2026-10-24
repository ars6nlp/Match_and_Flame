using UnityEngine;

/// <summary>
/// Мягкая тёмная подложка под фигурой уровня: собирает спички в одно поле и отделяет их от рисунка доски.
/// Строится один раз при загрузке уровня (GameManager) по границам коллайдеров спичек и слотов, без спички игрока.
/// Параметры и флаг — VisualTheme («Игра: подложка поля»).
/// </summary>
public static class PlayfieldBacking
{
    public static GameObject Build(GameObject level)
    {
        VisualTheme theme = VisualTheme.Current;
        if (!theme.playfieldBacking || level == null) return null;
        bool any = false;
        Bounds b = default;
        foreach (Collider2D c in level.GetComponentsInChildren<Collider2D>())
        {
            if (!c.enabled || c.GetComponent<DraggableMatch>() != null) continue;
            if (c.GetComponent<MatchLogic>() == null && c.gameObject.layer != LayerMask.NameToLayer("Slot")) continue;
            if (any) b.Encapsulate(c.bounds); else { b = c.bounds; any = true; }
        }
        if (!any) return null;
        var go = new GameObject("PlayfieldBacking");
        go.transform.SetParent(level.transform, false);
        go.transform.position = new Vector3(b.center.x, b.center.y, 0.5f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = theme.playfieldBackingSprite != null ? theme.playfieldBackingSprite : ProceduralTextures.Glow;
        sr.drawMode = theme.playfieldBackingSprite != null ? SpriteDrawMode.Sliced : SpriteDrawMode.Simple;
        Vector2 size = (Vector2)b.size + Vector2.one * (2f * theme.playfieldBackingPadding);
        if (sr.drawMode == SpriteDrawMode.Sliced) sr.size = size;
        else go.transform.localScale = new Vector3(size.x / sr.sprite.bounds.size.x, size.y / sr.sprite.bounds.size.y, 1f);
        Color c0 = theme.charColor;
        c0.a = theme.playfieldBackingAlpha;
        sr.color = c0;
        sr.sortingOrder = -5; // над фоном (−10), под спичками (0)
        return go;
    }
}
