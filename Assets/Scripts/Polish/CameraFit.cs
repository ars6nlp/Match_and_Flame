using UnityEngine;

/// <summary>
/// Размер ортокамеры, при котором содержимое уровня влезает по ширине и по высоте между HUD (сверху)
/// и жестовой панелью (снизу). Камера только отдаляется, не дальше maxZoom от исходного размера; позиция не меняется.
/// Общий расчёт для GameManager.FitCameraToLevel и редакторской проверки кадра (LevelFraming).
/// </summary>
public static class CameraFit
{
    // Опорное разрешение Canvas HUD (CanvasScaler: Scale With Screen Size, Expand)
    private const float RefWidth = 1080f, RefHeight = 1920f;

    /// <param name="topFraction">Доля высоты экрана, занятая сверху (вырез + HUD)</param>
    /// <param name="bottomFraction">Доля высоты экрана, занятая снизу (жестовая панель)</param>
    public static float Size(Bounds content, Vector2 cameraPosition, float aspect, float baseSize, float sideMargin,
                             float verticalMargin, float topFraction, float bottomFraction, float maxZoom)
    {
        float halfWidth = Mathf.Max(Mathf.Abs(content.min.x - cameraPosition.x), Mathf.Abs(content.max.x - cameraPosition.x));
        float size = (halfWidth + sideMargin) / aspect;
        float up = content.max.y - cameraPosition.y + verticalMargin;
        float down = cameraPosition.y - content.min.y + verticalMargin;
        // Видимая часть над центром: size·(1 − 2·topFraction), под центром: size·(1 − 2·bottomFraction)
        size = Mathf.Max(size, up / Mathf.Max(0.1f, 1f - 2f * topFraction), down / Mathf.Max(0.1f, 1f - 2f * bottomFraction));
        return Mathf.Clamp(size, baseSize, baseSize * maxZoom);
    }

    /// <summary>
    /// Что должно быть в кадре: включённые коллайдеры (спички, слоты, механики), спрайты кранов и вентиляторов,
    /// рамки подсказок. Размах Oscillator не учитывается: качающиеся препятствия уходят за край и на эталонных 9:16, так задумано.
    /// Спрайты спичек не берём: у них огромные прозрачные поля (кадры 1280×720).
    /// </summary>
    public static bool LevelContent(GameObject level, out Bounds content)
    {
        bool any = false;
        content = default;
        foreach (Collider2D col in level.GetComponentsInChildren<Collider2D>())
            if (col.enabled) Add(ref content, ref any, col.bounds);
        foreach (WaterDripper d in level.GetComponentsInChildren<WaterDripper>())
            if (d.TryGetComponent(out SpriteRenderer sr)) Add(ref content, ref any, sr.bounds);
        foreach (Fan f in level.GetComponentsInChildren<Fan>())
            if (f.TryGetComponent(out SpriteRenderer sr)) Add(ref content, ref any, sr.bounds);
        foreach (WorldHintBacking hint in level.GetComponentsInChildren<WorldHintBacking>())
            Add(ref content, ref any, hint.WorldRect);
        return any;
    }

    private static void Add(ref Bounds content, ref bool any, Bounds b)
    {
        if (any) content.Encapsulate(b); else { content = b; any = true; }
    }

    /// <summary>Доли экрана сверху и снизу, занятые вырезом/HUD и жестовой панелью, на текущем устройстве.</summary>
    public static void ScreenReserves(VisualTheme theme, out float topFraction, out float bottomFraction)
    {
        float h = Mathf.Max(1, Screen.height);
        Rect safe = Screen.safeArea;
        float canvasScale = Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight);
        float dpi = Screen.dpi > 0f ? Screen.dpi : 160f * Screen.width / 411f;
        topFraction = (h - safe.yMax + theme.levelHudReserve * canvasScale) / h;
        bottomFraction = Mathf.Max(safe.yMin, theme.levelBottomReserveDp * dpi / 160f) / h;
    }

    /// <summary>То же для эмуляции экрана в редакторе.</summary>
    public static void Reserves(VisualTheme theme, int width, int height, float notchPx, float bottomPx, float density,
                                out float topFraction, out float bottomFraction)
    {
        float canvasScale = Mathf.Min(width / RefWidth, height / RefHeight);
        topFraction = (notchPx + theme.levelHudReserve * canvasScale) / height;
        bottomFraction = Mathf.Max(bottomPx, theme.levelBottomReserveDp * density) / height;
    }
}
