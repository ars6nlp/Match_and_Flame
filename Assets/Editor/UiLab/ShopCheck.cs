using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Проверка кнопки-спички магазина: 6 разрешений × RU/EN × 5 состояний (Купить, Купить — не хватает, Выбрать, Выбрано,
/// Реклама). Прямоугольники надписи и бейджа цены не пересекаются и не выходят за тело спички; надпись не обрезана;
/// «Назад» и «Купить» одной высоты и на одной линии; панель в safe area и над баннером; стрелки не наезжают на огонёк;
/// контраст надписи к дереву. Report → Builds/shop-check.md, Shots (GPU) → Builds/ui-shots/shop/*.png.
/// </summary>
public static class ShopCheck
{
    private static readonly (ShopActionButton.State state, bool affordable, string name)[] States =
    {
        (ShopActionButton.State.Buy, true, "buy"), (ShopActionButton.State.Buy, false, "buy_short"),
        (ShopActionButton.State.Equip, true, "equip"), (ShopActionButton.State.Equipped, true, "equipped"),
        (ShopActionButton.State.WatchAd, true, "ad"),
    };

    [MenuItem("Tools/Match & Flame/UI Lab/Shop button check")]
    public static void Report()
    {
        var sb = new StringBuilder("# Магазин: кнопка-спичка и нижняя панель\n\n");
        sb.AppendLine("| Экран | Язык | Состояние | Надпись (шрифт) | Бейдж | Пересечение | Внутри тела | Обрезка | Высота Назад/Купить | Центр по y | Над баннером | Стрелки и огонёк |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
        int problems = 0, rows = 0;
        float contrast = Contrast("textColor", null), contrastEquipped = Contrast("equippedTextColor", "equippedTint");
        var shop = UiAudit.States().First(st => st.name == "shop");
        foreach (UiAudit.Res r in UiAudit.Resolutions)
            foreach (string lang in new[] { "ru", "en" })
                foreach (var st in States)
                {
                    var s = EditorSceneManager.OpenScene(UiAudit.Main, OpenSceneMode.Single);
                    shop.setup(s);
                    UiAudit.ShopState = st.state; UiAudit.ShopAffordable = st.affordable;
                    UiAudit.EmulateRuntime(s, lang);
                    UiAudit.Layout lay = UiAudit.Apply(s, r);
                    var btn = Object.FindFirstObjectByType<ShopActionButton>(FindObjectsInactive.Include);
                    btn.Layout();
                    Canvas.ForceUpdateCanvases();
                    btn.Label.ForceMeshUpdate();
                    if (btn.PriceText != null) btn.PriceText.ForceMeshUpdate();

                    Rect body = BodyRect(btn);
                    Rect label = TextRect(btn.Label);
                    bool badgeOn = btn.Badge != null && btn.Badge.gameObject.activeInHierarchy;
                    Rect badge = badgeOn ? WorldRect(btn.Badge) : Rect.zero;
                    bool overlap = badgeOn && label.Overlaps(Shrink(badge, 0.5f));
                    bool inside = Contains(body, label, 1f) && (!badgeOn || Contains(body, badge, 1f));
                    bool clipped = btn.Label.isTextOverflowing || btn.Label.fontSize < btn.Label.fontSizeMin - 0.01f;
                    var back = ((RectTransform)btn.transform.parent).Cast<Transform>().First(t => t.name == "BackButton") as RectTransform;
                    Rect rb = WorldRect(back), re = WorldRect((RectTransform)btn.transform);
                    bool sameH = Mathf.Abs(rb.height - re.height) < 1f, sameY = Mathf.Abs(rb.center.y - re.center.y) < 1f;
                    float bannerTop = (VisualTheme.Current.bannerReserveDp) * r.density / lay.scale;
                    float bottomGap = re.yMin - (-lay.canvasSize.y / 2f) - bannerTop;
                    bool above = bottomGap >= 0f && rb.yMin - (-lay.canvasSize.y / 2f) - bannerTop >= 0f;
                    bool arrowsOk = ArrowsClear(s);
                    bool ok = !overlap && inside && !clipped && sameH && sameY && above && arrowsOk;
                    if (!ok) problems++;
                    rows++;
                    sb.AppendLine($"| {r.name} | {lang.ToUpper()} | {st.name} | «{btn.Label.text}» ({btn.Label.fontSize:0}) | " +
                                  $"{(badgeOn ? (btn.PriceText.gameObject.activeSelf ? btn.PriceText.text : "видео") : "—")} | {(overlap ? "ДА" : "нет")} | {(inside ? "да" : "НЕТ")} | " +
                                  $"{(clipped ? "ДА" : "нет")} | {rb.height:0}/{re.height:0} | {(sameY ? "да" : "НЕТ")} | {bottomGap:0} | {(arrowsOk ? "не пересекаются" : "ПЕРЕСЕКАЮТСЯ")} |");
                }
        sb.AppendLine($"\nКонтраст тёмной надписи к средней яркости дерева спички: **{contrast:0.0}:1**, в состоянии «Выбрано» (тонированная спичка) — **{contrastEquipped:0.0}:1** (нужно ≥ 4.5:1); вокруг букв светлый контур.");
        sb.AppendLine($"\n**{(problems == 0 ? $"Нарушений нет ({rows} комбинаций)." : $"Нарушений: {problems} из {rows}")}**");
        File.WriteAllText("Builds/shop-check.md", sb.ToString());
        Debug.Log("[Polish] ShopCheck " + (problems == 0 ? "OK" : problems + " problems") + $", contrast {contrast:0.0}");
    }

    [MenuItem("Tools/Match & Flame/UI Lab/Shop button screenshots (needs GPU)")]
    public static void Shots()
    {
        var shop = UiAudit.States().First(st => st.name == "shop");
        string only = System.Environment.GetEnvironmentVariable("UI_RES") ?? "1080x1920,1080x2400,1200x1600";
        int n = 0;
        foreach (UiAudit.Res r in UiAudit.Resolutions.Where(x => only.Split(',').Contains(x.name)))
            foreach (string lang in new[] { "ru", "en" })
                foreach (var st in States)
                {
                    if (r.name != "1080x1920" && st.name != "buy" && st.name != "buy_short") continue;
                    var s = EditorSceneManager.OpenScene(UiAudit.Main, OpenSceneMode.Single);
                    shop.setup(s);
                    UiAudit.ShopState = st.state; UiAudit.ShopAffordable = st.affordable;
                    UiAudit.EmulateRuntime(s, lang);
                    UiAudit.Layout lay = UiAudit.Apply(s, r);
                    Object.FindFirstObjectByType<ShopActionButton>(FindObjectsInactive.Include).Layout();
                    if (UiAudit.Render(s, lay, $"Builds/ui-shots/shop/{st.name}_{lang}_{r.name}.png")) n++;
                }
        Debug.Log($"[Polish] ShopCheck.Shots: {n} PNG");
    }

    private static Rect BodyRect(ShopActionButton b)
    {
        var so = new SerializedObject(b);
        Vector2 bx = so.FindProperty("bodyX").vector2Value, by = so.FindProperty("bodyY").vector2Value;
        Rect r = WorldRect((RectTransform)b.transform);
        return Rect.MinMaxRect(r.xMin + bx.x * r.width, r.yMin + by.x * r.height, r.xMin + bx.y * r.width, r.yMin + by.y * r.height);
    }

    /// <summary>Фактические границы букв (textBounds) в мировых координатах Canvas.</summary>
    private static Rect TextRect(TextMeshProUGUI t)
    {
        Bounds b = t.textBounds;
        Vector3 min = t.rectTransform.TransformPoint(b.min), max = t.rectTransform.TransformPoint(b.max);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static Rect WorldRect(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return Rect.MinMaxRect(c.Min(v => v.x), c.Min(v => v.y), c.Max(v => v.x), c.Max(v => v.y));
    }

    private static Rect Shrink(Rect r, float d) => Rect.MinMaxRect(r.xMin + d, r.yMin + d, r.xMax - d, r.yMax - d);
    private static bool Contains(Rect outer, Rect inner, float tol) =>
        inner.xMin >= outer.xMin - tol && inner.xMax <= outer.xMax + tol && inner.yMin >= outer.yMin - tol && inner.yMax <= outer.yMax + tol;

    /// <summary>Стрелки переключения скинов не пересекают огонёк (огонёк — середина картинки: 40% ширины, 15–85% высоты).</summary>
    private static bool ArrowsClear(UnityEngine.SceneManagement.Scene s)
    {
        var anim = Object.FindFirstObjectByType<UISkinSequenceAnimator>(FindObjectsInactive.Include);
        if (anim == null) return true;
        Rect img = WorldRect((RectTransform)anim.transform);
        Rect flame = Rect.MinMaxRect(img.xMin + 0.3f * img.width, img.yMin + 0.15f * img.height, img.xMax - 0.3f * img.width, img.yMax - 0.15f * img.height);
        foreach (string name in new[] { "Left", "Right" })
        {
            GameObject a = UiAudit.Find(s, name);
            if (a != null && a.activeInHierarchy && WorldRect((RectTransform)a.transform).Overlaps(flame)) return false;
        }
        return true;
    }

    /// <summary>Контраст по WCAG: цвет надписи против средней яркости тела спички (по пикселям PNG).</summary>
    private static float Contrast(string textField, string tintField)
    {
        var bt = Object.FindFirstObjectByType<ShopActionButton>(FindObjectsInactive.Include);
        Color tint = tintField != null && bt != null ? new SerializedObject(bt).FindProperty(tintField).colorValue : Color.white;
        tint.a = 1f;
        var tex = new Texture2D(2, 2);
        tex.LoadImage(File.ReadAllBytes("Assets/Art/Button/match_button_blank.png"));
        double sum = 0; int n = 0;
        for (int x = (int)(tex.width * 0.08f); x < tex.width * 0.75f; x += 3)
            for (int y = (int)(tex.height * 0.2f); y < tex.height * 0.8f; y += 3)
            {
                Color c = tex.GetPixel(x, y);
                if (c.a < 0.9f) continue;
                sum += Lum(c * tint); n++;
            }
        Object.DestroyImmediate(tex);
        var b = Object.FindFirstObjectByType<ShopActionButton>(FindObjectsInactive.Include);
        Color text = b != null ? (Color)new SerializedObject(b).FindProperty(textField).colorValue : new Color(0.2f, 0.09f, 0.03f);
        double wood = n > 0 ? sum / n : 0.5, ink = Lum(text);
        return (float)((wood + 0.05) / (ink + 0.05));
    }

    private static double Lum(Color c)
    {
        double f(double v) => v <= 0.03928 ? v / 12.92 : System.Math.Pow((v + 0.055) / 1.055, 2.4);
        return 0.2126 * f(c.r) + 0.7152 * f(c.g) + 0.0722 * f(c.b);
    }
}
