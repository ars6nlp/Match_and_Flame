using UnityEngine;

/// <summary>
/// Мягкие круглые текстуры для частиц и свечений, генерируются в коде (без файлов).
/// Используются как запасной вариант, если в компоненте не назначен спрайт
/// (в проекте есть те же картинки в Assets/Art/Generated, чтобы их было видно в редакторе).
/// </summary>
public static class ProceduralTextures
{
    private static Sprite softDot, glow;

    /// <summary>64×64, плотный центр — искры, угольки, пепел.</summary>
    public static Sprite SoftDot => softDot != null ? softDot : (softDot = Make("SoftDot", 64, 2.2f));

    /// <summary>128×128, очень мягкий край — свечения, подсветки.</summary>
    public static Sprite Glow => glow != null ? glow : (glow = Make("Glow", 128, 1.4f));

    /// <summary>Пиксели радиального градиента: белый цвет, альфа спадает к краю.</summary>
    public static Color32[] RadialPixels(int size, float falloff)
    {
        var pixels = new Color32[size * size];
        float r = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - r) / r, dy = (y - r) / r;
                float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), falloff);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        return pixels;
    }

    private static Sprite Make(string name, int size, float falloff)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
        };
        tex.SetPixels32(RadialPixels(size, falloff));
        tex.Apply(false, true); // без CPU-копии
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
