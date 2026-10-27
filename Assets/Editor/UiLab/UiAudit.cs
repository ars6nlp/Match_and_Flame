using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Аудитор раскладки UI (без запуска игры). Для каждой сцены/панели и разрешения раскладывает Canvas так же,
/// как CanvasScaler (Scale With Screen Size), эмулирует безопасную зону (вырез сверху 100 px, жестовая панель снизу 60 px)
/// и проверяет: перекрытия кнопок между собой и с текстом, выход за экран и в небезопасную зону, размер касания
/// меньше 48 dp, кнопки ближе 24 px к краю, зону баннера (меню и выбор уровня), кнопки под невидимыми
/// перехватчиками нажатий, переполнение текста на RU и EN.
/// Запуск: -executeMethod UiAudit.Audit (UI_TAG=before|after) → Builds/ui-audit-{tag}.md.
/// Скриншоты: -executeMethod UiAudit.Shots БЕЗ -nographics → Builds/ui-shots/{tag}/.
/// Сцены не сохраняются: каждое состояние открывается заново.
/// </summary>
public static class UiAudit
{
    public struct Res
    {
        public string name; public int w, h; public float density;
        public Res(string n, int w, int h, float d) { name = n; this.w = w; this.h = h; density = d; }
        public float Dp(float dp) => dp * density;
    }

    public static readonly Res[] Resolutions =
    {
        new Res("1080x1920", 1080, 1920, 2.625f),
        new Res("1080x2400", 1080, 2400, 2.625f),
        new Res("1080x2520", 1080, 2520, 2.625f),
        new Res("1440x3040", 1440, 3040, 3.5f),
        new Res("1200x1600", 1200, 1600, 1.5f),
        new Res("800x1280", 800, 1280, 1.333f),
    };

    public const float NotchPx = 100f, GesturePx = 60f, MinTouchDp = 48f, EdgePx = 24f, BannerDp = 100f;

    public class State
    {
        public string scene, name, modal;
        public bool banner;
        public Action<Scene> setup;
    }

    public static readonly string Main = "Assets/Scenes/MainScene.unity", Levels = "Assets/Scenes/LevelSelectionScene.unity", Game = "Assets/Scenes/GameScene.unity";

    public static List<State> States() => new List<State>
    {
        new State { scene = Main, name = "menu", banner = true, setup = s => { Off(s, "ShopPanel"); Off(s, "SettingsPanel"); RetentionWidgets(s); } },
        new State { scene = Main, name = "shop", banner = true, modal = "ShopPanel", setup = s => { On(s, "ShopPanel"); Off(s, "SettingsPanel"); Off(s, "MenuPanel"); Off(s, "ChangeSkinButton"); } }, // как OpenShop + кнопка магазина
        new State { scene = Main, name = "settings", banner = true, modal = "SettingsPanel", setup = s => { Off(s, "ShopPanel"); On(s, "SettingsPanel"); Off(s, "MenuPanel"); } }, // как OpenSettings
        new State { scene = Levels, name = "levels", banner = true, setup = SpawnLevelCards },
        new State { scene = Game, name = "hud", setup = s => GameState(s, null) },
        new State { scene = Game, name = "pause", modal = "PausePanel", setup = s => GameState(s, "PausePanel") },
        new State { scene = Game, name = "win", modal = "WinPanel", setup = s => GameState(s, "WinPanel") },
        new State { scene = Game, name = "lose", modal = "AlmostPanel", setup = s => GameState(s, "AlmostPanel") },
        new State { scene = Game, name = "tutorial", setup = s => GameState(s, "Tutorial") },
    };

    // ---------------------------------------------------------------- Состояния

    public static GameObject Find(Scene s, string name) =>
        s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name && t.GetComponentInParent<Canvas>(true) != null)?.gameObject;

    private static void On(Scene s, string name) { var g = Find(s, name); if (g != null) g.SetActive(true); }
    private static void Off(Scene s, string name) { var g = Find(s, name); if (g != null) g.SetActive(false); }

    private static void GameState(Scene s, string panel)
    {
        foreach (string p in new[] { "PausePanel", "WinPanel", "AlmostPanel", "Tutorial" }) Off(s, p);
        Off(s, "StartLevelDoors"); // двери уровня — анимация открытия, к раскладке не относятся
        if (panel != null) On(s, panel);
        if (panel == "WinPanel")
        {
            On(s, "NextButton");
            var doors = Find(s, "DoorsContainer"); if (doors != null) doors.SetActive(false);
        }
    }

    /// <summary>Карточки уровней, как их строит LevelSelectionManager (22 шт., текущий — 6-й).</summary>
    /// <summary>Карточки «Очага» в главном меню (как RetentionMenuUI.Attach в игре, без окон).</summary>
    public static void RetentionWidgets(Scene s)
    {
        if (!RetentionSettings.Current.systemEnabled) return;
        Transform menu = s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "MenuPanel");
        if (menu == null) return;
        RetentionMenuUI ui = RetentionMenuUI.BuildForAudit(menu);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ui.gameObject, s);
    }

    public static void SpawnLevelCards(Scene s)
    {
        var mgr = s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LevelSelectionManager>(true)).FirstOrDefault();
        if (mgr == null) return;
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Resources/LevelCatalog.asset");
        int count = catalog != null ? catalog.Count : mgr.totalLevels;
        Transform[] parents = ChapterUI.Enabled ? ChapterUI.Build(mgr.gridContainer, count) : null;   // главы «Очага», как в игре
        for (int i = 0; i < count; i++)
        {
            GameObject card = Object.Instantiate(mgr.levelButtonPrefab, parents != null ? parents[i] : mgr.gridContainer);
            card.SetActive(true);
            card.name = "Level" + (i + 1);
            var lb = card.GetComponent<LevelButton>();
            if (lb != null) lb.Setup(i, i == 5, false, catalog != null ? catalog.GetDifficulty(i) : 1, -1f);
        }
    }

    /// <summary>
    /// То, что в игре делают скрипты при запуске: кадр покадровой анимации вместо пустого Image,
    /// тексты и картинки выбранного языка (UI_LANG=ru|en). Только для проверки, сцена не сохраняется.
    /// </summary>
    /// <summary>Состояние кнопки магазина в проверках (ShopCheck перебирает все).</summary>
    public static ShopActionButton.State ShopState = ShopActionButton.State.Equipped;
    public static bool ShopAffordable = true;

    public static void EmulateRuntime(Scene s, string lang)
    {
        var all = s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
        foreach (var a in all.Select(t => t.GetComponent<UISkinSequenceAnimator>()).Where(a => a != null))
        {
            var img = a.GetComponent<Image>();
            var frames = a.skins != null && a.skins.Length > 0 ? a.skins[0].frames : null;
            if (img != null && frames != null && frames.Length > 0) img.sprite = frames[frames.Length / 2];
        }
        foreach (var a in all.Select(t => t.GetComponent<UISequenceAnimator>()).Where(a => a != null))
        {
            var img = a.GetComponent<Image>();
            if (img != null && a.frames != null && a.frames.Length > 0) img.sprite = a.frames[a.frames.Length - 1];
        }
        bool ru = lang == "ru";
        foreach (var shop in all.Select(t => t.GetComponent<ShopManager>()).Where(m => m != null))
        {
            Sprite sp = ru && shop.equippedSpriteRU != null ? shop.equippedSpriteRU : shop.equippedSprite; // текущий скин уже надет
            if (shop.ActionButton != null) { shop.ActionButton.Show(ShopState, 15, ShopAffordable); continue; } // надпись и цена — отдельными элементами
            if (shop.equipButtonImage != null && sp != null) shop.equipButtonImage.sprite = sp;
            if (shop.priceText != null) shop.priceText.gameObject.SetActive(false);
        }
        foreach (var l in all.Select(t => t.GetComponent<LocalizeText>()).Where(l => l != null))
        {
            var tmp = l.GetComponent<TMP_Text>();
            string text = ru ? l.textRU : l.textEN;
            if (tmp != null && !string.IsNullOrEmpty(text)) tmp.text = text;
        }
        foreach (var l in all.Select(t => t.GetComponent<LocalizeImage>()).Where(l => l != null))
        {
            var img = l.GetComponent<Image>();
            Sprite sp = ru ? l.spriteRU : l.spriteEN;
            if (img != null && sp != null) img.sprite = sp;
        }
    }

    // ---------------------------------------------------------------- Раскладка под разрешение

    public class Layout
    {
        public Res res;
        public float scale;          // пикселей на единицу канваса
        public Vector2 canvasSize;   // в единицах канваса
        public List<Canvas> roots = new List<Canvas>();
    }

    /// <summary>Перевести корневые Canvas в WorldSpace с размером, который выставил бы CanvasScaler, и эмулировать безопасную зону.</summary>
    public static Layout Apply(Scene s, Res r)
    {
        var lay = new Layout { res = r };
        foreach (Canvas c in s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true)).Where(c => c.isRootCanvas && c.renderMode != RenderMode.WorldSpace))
        {
            var scaler = c.GetComponent<CanvasScaler>();
            float scale = 1f;
            if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                Vector2 refRes = scaler.referenceResolution;
                switch (scaler.screenMatchMode)
                {
                    case CanvasScaler.ScreenMatchMode.Expand: scale = Mathf.Min(r.w / refRes.x, r.h / refRes.y); break;
                    case CanvasScaler.ScreenMatchMode.Shrink: scale = Mathf.Max(r.w / refRes.x, r.h / refRes.y); break;
                    default:
                        float lw = Mathf.Log(r.w / refRes.x, 2), lh = Mathf.Log(r.h / refRes.y, 2);
                        scale = Mathf.Pow(2, Mathf.Lerp(lw, lh, scaler.matchWidthOrHeight)); break;
                }
                scaler.enabled = false;
            }
            lay.scale = scale;
            lay.canvasSize = new Vector2(r.w / scale, r.h / scale);
            c.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)c.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = lay.canvasSize;
            rt.position = Vector3.zero;
            rt.rotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            lay.roots.Add(c);
        }
        // SafeArea: как SafeArea.Apply, но с эмулированной зоной
        foreach (SafeArea sa in s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SafeArea>(true)))
        {
            var rt = (RectTransform)sa.transform;
            rt.anchorMin = new Vector2(0f, GesturePx / r.h);
            rt.anchorMax = new Vector2(1f, 1f - NotchPx / r.h);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        // BannerReserve: (bannerReserveDp + bannerGapDp) dp → пиксели этого экрана → единицы Canvas
        VisualTheme theme = VisualTheme.Current;
        foreach (BannerReserve br in s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BannerReserve>(true)))
        {
            var rt = (RectTransform)br.transform;
            rt.offsetMin = new Vector2(rt.offsetMin.x, (theme.bannerReserveDp + theme.bannerGapDp) * r.density / lay.scale);
        }
        Rebuild(s);
        // Огонёк меню раскладывается по размеру Canvas (в игре — в OnEnable/LateUpdate, здесь кадров нет)
        foreach (MenuFlameLayout m in s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MenuFlameLayout>(true))) m.Apply();
        Rebuild(s);
        foreach (UIFollowRect f in s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UIFollowRect>(true))) f.Sync();
        return lay;
    }

    public static void Rebuild(Scene s)
    {
        Canvas.ForceUpdateCanvases();
        foreach (var lg in s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LayoutGroup>(false)).Reverse())
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)lg.transform);
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>Прямоугольник в пикселях экрана (0,0 — левый нижний угол).</summary>
    public static Rect PixelRect(RectTransform rt, Layout lay)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        float minX = c.Min(v => v.x), maxX = c.Max(v => v.x), minY = c.Min(v => v.y), maxY = c.Max(v => v.y);
        return Rect.MinMaxRect((minX + lay.canvasSize.x / 2f) * lay.scale, (minY + lay.canvasSize.y / 2f) * lay.scale,
                               (maxX + lay.canvasSize.x / 2f) * lay.scale, (maxY + lay.canvasSize.y / 2f) * lay.scale);
    }

    // ---------------------------------------------------------------- Проверки

    public class Issue { public string state, element, res, kind, detail; public int severity; }

    private class Elem
    {
        public RectTransform rt;
        public Rect rect, full; // видимая часть и полный прямоугольник (для размера касания)
        public string path;
        public bool interactive, text, raycast;
        public int order;
        public Selectable selectable;
    }

    private static string Path(Transform t)
    {
        var parts = new List<string>();
        for (Transform p = t; p != null; p = p.parent)
        {
            var c = p.GetComponent<Canvas>();
            if (c != null && c.isRootCanvas) break;
            parts.Add(p.name);
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static bool Visible(Transform t)
    {
        for (Transform p = t; p != null; p = p.parent)
        {
            var cg = p.GetComponent<CanvasGroup>();
            if (cg != null && cg.alpha <= 0.01f && !cg.ignoreParentGroups) return false;
        }
        return true;
    }

    // Сколько элементов проверено за прогон (чтобы «0 проблем» не означало «ничего не проверили»)
    private static int CheckedInteractive, CheckedTexts;

    public static List<Issue> Check(Scene s, State st, Layout lay)
    {
        var issues = new List<Issue>();
        Res r = lay.res;
        var elems = new List<Elem>();
        int order = 0;
        foreach (Canvas root in lay.roots)
            foreach (RectTransform rt in root.GetComponentsInChildren<RectTransform>(false))
            {
                order++;
                var g = rt.GetComponent<Graphic>();
                var sel = rt.GetComponent<Selectable>();
                var tmp = rt.GetComponent<TMP_Text>();
                var legacy = rt.GetComponent<Text>();
                bool isText = (tmp != null && tmp.enabled && !string.IsNullOrWhiteSpace(tmp.text) && tmp.color.a > 0.05f) ||
                              (legacy != null && legacy.enabled && !string.IsNullOrWhiteSpace(legacy.text) && legacy.color.a > 0.05f);
                bool interactive = sel != null && sel.enabled && sel.interactable && Visible(rt);
                if (g == null && sel == null) continue;
                Rect rect = PixelRect(rt, lay);
                Rect fullRect = rect;
                // Содержимое прокрутки: видимая часть — пересечение с маской (RectMask2D); полностью скрытое не проверяем
                RectMask2D mask = rt.GetComponentInParent<RectMask2D>();
                if (mask != null && mask.transform != rt)
                {
                    Rect mr = PixelRect((RectTransform)mask.transform, lay);
                    rect = Rect.MinMaxRect(Mathf.Max(rect.xMin, mr.xMin), Mathf.Max(rect.yMin, mr.yMin), Mathf.Min(rect.xMax, mr.xMax), Mathf.Min(rect.yMax, mr.yMax));
                    if (rect.width <= 0f || rect.height <= 0f) continue;
                }
                if (sel != null && sel.targetGraphic != null && sel.targetGraphic.transform != rt)
                {
                    Rect tg = PixelRect((RectTransform)sel.targetGraphic.transform, lay);
                    rect = Rect.MinMaxRect(Mathf.Min(rect.xMin, tg.xMin), Mathf.Min(rect.yMin, tg.yMin), Mathf.Max(rect.xMax, tg.xMax), Mathf.Max(rect.yMax, tg.yMax));
                }
                elems.Add(new Elem
                {
                    rt = rt, rect = rect, full = fullRect, path = Path(rt), interactive = interactive, text = isText && Visible(rt),
                    raycast = g != null && g.enabled && g.raycastTarget, order = order, selectable = sel,
                });
            }

        void Add(string el, string kind, string detail, int sev) =>
            issues.Add(new Issue { state = st.name, element = el, res = r.name, kind = kind, detail = detail, severity = sev });

        bool Related(Elem a, Elem b) => a.rt.IsChildOf(b.rt) || b.rt.IsChildOf(a.rt) ||
                                        (a.selectable != null && b.rt.IsChildOf(a.selectable.transform)) || (b.selectable != null && a.rt.IsChildOf(b.selectable.transform));
        float Area(Rect x) => Mathf.Max(0f, x.width) * Mathf.Max(0f, x.height);
        float Inter(Rect a, Rect b) => Mathf.Max(0f, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin)) * Mathf.Max(0f, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin));

        var inter = elems.Where(e => e.interactive).ToList();
        CheckedInteractive += inter.Count;
        CheckedTexts += elems.Count(e => e.text);
        var texts = elems.Where(e => e.text).ToList();
        Rect screen = new Rect(0, 0, r.w, r.h);
        Rect safe = Rect.MinMaxRect(0, GesturePx, r.w, r.h - NotchPx);
        Rect banner = Rect.MinMaxRect(0, GesturePx, r.w, GesturePx + r.Dp(BannerDp));
        float minTouch = r.Dp(MinTouchDp);

        for (int i = 0; i < inter.Count; i++)
            for (int j = i + 1; j < inter.Count; j++)
            {
                Elem a = inter[i], b = inter[j];
                if (Related(a, b)) continue;
                float k = Inter(a.rect, b.rect) / Mathf.Max(1f, Mathf.Min(Area(a.rect), Area(b.rect)));
                if (k >= 0.05f) Add($"{a.path} × {b.path}", "OVERLAP", $"{k:P0} площади меньшей", 3);
            }
        foreach (Elem a in inter)
            foreach (Elem t in texts)
            {
                if (Related(a, t)) continue;
                float k = Inter(a.rect, t.rect) / Mathf.Max(1f, Area(t.rect));
                if (k >= 0.10f) Add($"{a.path} × {t.path}", "TEXT_OVER", $"кнопка закрывает {k:P0} текста", 2);
            }
        foreach (Elem e in elems.Where(e => e.interactive || e.text))
        {
            Rect x = e.rect;
            if (Area(x) >= 0.85f * r.w * r.h) continue; // фон на весь экран
            float outside = Area(x) - Inter(x, screen);
            if (outside > 4f * Mathf.Max(x.width, x.height))
                Add(e.path, "OFFSCREEN", $"вне экрана {outside / Mathf.Max(1f, Area(x)):P0} площади, rect {Fmt(x)}", e.interactive ? 3 : 2);
            else if (Inter(x, screen) > 0f && (x.yMax > safe.yMax + 1f || x.yMin < safe.yMin - 1f))
                Add(e.path, "UNSAFE", $"заходит под вырез/жестовую панель, rect {Fmt(x)}", e.interactive ? 3 : 1);
        }
        foreach (Elem e in inter)
        {
            Rect x = e.rect;
            if (Mathf.Min(e.full.width, e.full.height) < minTouch - 0.5f) Add(e.path, "SMALL", $"{e.full.width:0}×{e.full.height:0} px < {minTouch:0} (48 dp)", 2);
            float edge = Mathf.Min(x.xMin, r.w - x.xMax);
            if (Inter(x, screen) > 0f && edge < EdgePx && edge > -x.width) Add(e.path, "EDGE", $"до бокового края {edge:0} px", 1);
            if (st.banner && Inter(x, banner) > 0f) Add(e.path, "BANNER", $"в зоне баннера (низ {GesturePx:0}…{banner.yMax:0} px)", 3);
            // Кто перехватит нажатие в центре кнопки: последний по порядку отрисовки raycast-элемент не из её иерархии
            Vector2 c = x.center;
            Elem top = elems.Where(o => o.raycast && o.order > e.order && o.rect.Contains(c) && !Related(o, e) && Visible(o.rt)).OrderByDescending(o => o.order).FirstOrDefault();
            Transform modal = st.modal != null ? Find(s, st.modal)?.transform : null;
            bool intended = top != null && modal != null && top.rt.IsChildOf(modal) && !e.rt.IsChildOf(modal); // открытая модальная панель закрывает всё под собой
            if (top != null && !intended) Add($"{e.path} ← {top.path}", "BLOCKED", "нажатие перехватывает элемент поверх", 3);
            // Кнопка вне открытой модальной панели нарисована поверх неё
            if (modal != null && !e.rt.IsChildOf(modal))
            {
                var m = elems.FirstOrDefault(o => o.rt == modal);
                if (m != null && e.order > m.order && Inter(x, m.rect) > 0f) Add(e.path, "ABOVE_MODAL", $"кнопка поверх открытой панели {st.modal}", 3);
            }
        }
        return issues;
    }

    /// <summary>Переполнение текста (RU и EN): предпочтительный размер больше рамки, без автоподбора.</summary>
    public static List<Issue> CheckTexts(Scene s, State st, Layout lay)
    {
        var issues = new List<Issue>();
        foreach (Canvas root in lay.roots)
            foreach (TMP_Text tmp in root.GetComponentsInChildren<TMP_Text>(false))
            {
                var loc = tmp.GetComponent<LocalizeText>();
                var variants = loc != null ? new[] { ("RU", loc.textRU), ("EN", loc.textEN) } : new[] { ("", tmp.text) };
                Rect box = ((RectTransform)tmp.transform).rect;
                if (box.width <= 1f || box.height <= 1f) continue;
                foreach (var (lang, text) in variants)
                {
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    float size = tmp.enableAutoSizing ? tmp.fontSizeMin : tmp.fontSize;
                    float old = tmp.fontSize;
                    tmp.fontSize = size;
                    Vector2 pref = tmp.GetPreferredValues(text, tmp.textWrappingMode == TextWrappingModes.NoWrap ? 0f : box.width, 0f);
                    tmp.fontSize = old;
                    bool over = tmp.textWrappingMode == TextWrappingModes.NoWrap ? pref.x > box.width * 1.05f : pref.y > box.height * 1.15f;
                    if (over) issues.Add(new Issue { state = st.name, element = Path(tmp.transform), res = lay.res.name, kind = "TEXT_OVERFLOW",
                        detail = $"{lang} «{text.Replace("\n", " ")}» нужно {pref.x:0}×{pref.y:0}, рамка {box.width:0}×{box.height:0}", severity = 2 });
                }
            }
        return issues;
    }

    // ---------------------------------------------------------------- Читаемость (окна «Очага»)

    public const float MinFontSp = 14f, MinContrast = 4.5f;

    /// <summary>Точка Canvas (мир при раскладке Apply) → пиксели экрана.</summary>
    private static Vector2 ToPixel(Vector3 w, Layout lay) => new Vector2((w.x + lay.canvasSize.x / 2f) * lay.scale, (w.y + lay.canvasSize.y / 2f) * lay.scale);

    /// <summary>Прямоугольник реально нарисованных букв (textBounds), в пикселях экрана; обрезан маской прокрутки.</summary>
    public static Rect GlyphRect(TMP_Text tmp, Layout lay)
    {
        Bounds b = tmp.textBounds;
        if (b.size.x <= 0f || b.size.y <= 0f) return Rect.zero;
        Vector2 a = ToPixel(tmp.transform.TransformPoint(new Vector3(b.min.x, b.min.y, 0f)), lay);
        Vector2 c = ToPixel(tmp.transform.TransformPoint(new Vector3(b.max.x, b.max.y, 0f)), lay);
        Rect r = Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y), Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y));
        RectMask2D mask = tmp.GetComponentInParent<RectMask2D>();
        if (mask != null)
        {
            Rect mr = PixelRect((RectTransform)mask.transform, lay);
            r = Rect.MinMaxRect(Mathf.Max(r.xMin, mr.xMin), Mathf.Max(r.yMin, mr.yMin), Mathf.Min(r.xMax, mr.xMax), Mathf.Min(r.yMax, mr.yMax));
            if (r.width <= 0f || r.height <= 0f) return Rect.zero;
        }
        return r;
    }

    /// <summary>Видимые тексты: TMP включён, не пуст, не прозрачен, не спрятан CanvasGroup.</summary>
    public static List<TMP_Text> VisibleTexts(Layout lay)
    {
        var list = new List<TMP_Text>();
        foreach (Canvas root in lay.roots)
            foreach (TMP_Text tmp in root.GetComponentsInChildren<TMP_Text>(false))
                if (tmp.enabled && !string.IsNullOrWhiteSpace(tmp.text) && tmp.color.a > 0.05f && Visible(tmp.transform)) list.Add(tmp);
        return list;
    }

    /// <summary>Наименьший кегль среди нарисованных букв, в пикселях экрана (с учётом масштаба карточек).</summary>
    public static float FontPx(TMP_Text tmp, Layout lay)
    {
        tmp.ForceMeshUpdate();
        TMP_TextInfo info = tmp.textInfo;
        float pt = float.MaxValue;
        for (int i = 0; i < info.characterCount; i++) if (info.characterInfo[i].isVisible) pt = Mathf.Min(pt, info.characterInfo[i].pointSize);
        if (pt == float.MaxValue) return -1f;
        Canvas root = tmp.canvas != null ? tmp.canvas.rootCanvas : null;
        float k = tmp.transform.lossyScale.y / Mathf.Max(1e-6f, root != null ? root.transform.lossyScale.y : 1f);
        return pt * k * lay.scale;
    }

    private static string Snip(string t) { t = t.Replace("\n", " "); return t.Length > 40 ? t.Substring(0, 40) + "…" : t; }

    /// <summary>
    /// Читаемость: кегль меньше 14 sp (SMALL_FONT), буквы на буквах (TEXT_OVERLAP, по нарисованным глифам),
    /// кнопка или текст вылезает из своей карточки (OUT_OF_CARD; карточка — Image со спрайтом card).
    /// </summary>
    public static List<Issue> CheckReadability(Scene s, State st, Layout lay, Sprite cardSprite, string langTag = "")
    {
        var issues = new List<Issue>();
        Res r = lay.res;
        string res = r.name + langTag;
        float minPx = MinFontSp * r.density - 0.75f;
        void Add(string el, string kind, string detail, int sev) => issues.Add(new Issue { state = st.name, element = el, res = res, kind = kind, detail = detail, severity = sev });
        float Area(Rect x) => Mathf.Max(0f, x.width) * Mathf.Max(0f, x.height);
        float Inter(Rect a, Rect b) => Mathf.Max(0f, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin)) * Mathf.Max(0f, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin));

        var glyphs = new List<(TMP_Text t, Rect g)>();
        foreach (TMP_Text tmp in VisibleTexts(lay))
        {
            float px = FontPx(tmp, lay);
            Rect g = GlyphRect(tmp, lay);
            if (px < 0f || Area(g) <= 0f) continue;
            if (px < minPx) Add(Path(tmp.transform), "SMALL_FONT", $"{px:0} px < {minPx + 0.75f:0} (14 sp): «{Snip(tmp.text)}»", 2);
            glyphs.Add((tmp, g));
        }
        for (int i = 0; i < glyphs.Count; i++)
            for (int j = i + 1; j < glyphs.Count; j++)
            {
                var (a, ga) = glyphs[i]; var (b, gb) = glyphs[j];
                if (a.transform.IsChildOf(b.transform) || b.transform.IsChildOf(a.transform)) continue;
                float k = Inter(ga, gb) / Mathf.Max(1f, Mathf.Min(Area(ga), Area(gb)));
                if (k >= 0.06f) Add($"{Path(a.transform)} × {Path(b.transform)}", "TEXT_OVERLAP", $"буквы перекрываются на {k:P0}: «{Snip(a.text)}» / «{Snip(b.text)}»", 3);
            }
        if (cardSprite != null)
        {
            var cards = new List<Image>();
            foreach (Canvas root in lay.roots)
                foreach (Image im in root.GetComponentsInChildren<Image>(false))
                    if (im.enabled && im.sprite == cardSprite && im.transform.childCount > 0 && Visible(im.transform)) cards.Add(im);
            Image CardOf(Transform t)
            {
                for (Transform p = t.parent; p != null; p = p.parent) { var im = p.GetComponent<Image>(); if (im != null && cards.Contains(im)) return im; }
                return null;
            }
            void Inside(Transform t, Rect x, string what)
            {
                Image card = CardOf(t);
                if (card == null) return;
                Rect c = PixelRect(card.rectTransform, lay);
                float over = Mathf.Max(c.xMin - x.xMin, x.xMax - c.xMax, c.yMin - x.yMin, x.yMax - c.yMax);
                if (over > 3f) Add($"{Path(t)} ⊄ {Path(card.transform)}", "OUT_OF_CARD", $"{what} выходит за карточку на {over:0} px", 2);
            }
            foreach (var (t, g) in glyphs) Inside(t.transform, g, "текст");
            foreach (Canvas root in lay.roots)
                foreach (Selectable sel in root.GetComponentsInChildren<Selectable>(false))
                    if (sel.enabled && sel.interactable && Visible(sel.transform)) Inside(sel.transform, PixelRect((RectTransform)sel.transform, lay), "кнопка");
        }
        return issues;
    }

    /// <summary>Что нужно для проверки контраста: прямоугольник букв и итоговый цвет текста (до отрисовки — потом Canvas уезжает к камере).</summary>
    public class TextSample { public string path, text; public Rect rect; public Color color, outline; public float outlineWidth; }

    public static List<TextSample> TextSamples(Layout lay)
    {
        var list = new List<TextSample>();
        foreach (TMP_Text tmp in VisibleTexts(lay))
        {
            tmp.ForceMeshUpdate();
            Rect g = GlyphRect(tmp, lay);
            if (g.width < 2f || g.height < 2f) continue;
            Color c = tmp.color;
            float alpha = c.a;
            for (Transform p = tmp.transform; p != null; p = p.parent) { var cg = p.GetComponent<CanvasGroup>(); if (cg != null) alpha *= cg.alpha; }
            Material m = tmp.fontSharedMaterial;
            if (m != null && m.HasProperty(ShaderUtilities.ID_FaceColor)) { Color f = m.GetColor(ShaderUtilities.ID_FaceColor); c *= f; alpha *= f.a; }
            c.a = alpha;
            var smp = new TextSample { path = Path(tmp.transform), text = tmp.text, rect = g, color = c };
            if (tmp.outlineWidth > 0.05f) { smp.outline = tmp.outlineColor; smp.outlineWidth = tmp.outlineWidth; }
            list.Add(smp);
        }
        return list;
    }

    private static float Lum(Color c)
    {
        float L(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
        return 0.2126f * L(c.r) + 0.7152f * L(c.g) + 0.0722f * L(c.b);
    }

    public static float Contrast(Color a, Color b) { float x = Lum(a), y = Lum(b); return (Mathf.Max(x, y) + 0.05f) / (Mathf.Min(x, y) + 0.05f); }

    /// <summary>
    /// Контраст текста с фоном (CONTRAST): фон берётся из кадра, отрисованного без текста, под прямоугольником букв;
    /// считается для каждого пикселя фона, в зачёт идёт 25-й перцентиль (пёстрый фон не прячется за средним).
    /// Текст с обводкой (≥0.1) сравнивается и с обводкой — берётся лучшее.
    /// </summary>
    public static List<Issue> CheckContrast(State st, Layout lay, List<TextSample> samples, Texture2D noText, string langTag = "")
    {
        var issues = new List<Issue>();
        var vals = new List<float>(4096);
        foreach (TextSample t in samples)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(t.rect.xMin), 0, noText.width - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(t.rect.yMin), 0, noText.height - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(t.rect.xMax), x0 + 1, noText.width), y1 = Mathf.Clamp(Mathf.CeilToInt(t.rect.yMax), y0 + 1, noText.height);
            Color[] px = noText.GetPixels(x0, y0, x1 - x0, y1 - y0);
            vals.Clear();
            Color sum = Color.clear;
            int step = Mathf.Max(1, px.Length / 4000);
            for (int i = 0; i < px.Length; i += step)
            {
                Color bg = px[i];
                Color fg = Color.Lerp(bg, new Color(t.color.r, t.color.g, t.color.b, 1f), t.color.a);
                vals.Add(Contrast(fg, bg));
                sum += bg;
            }
            if (vals.Count == 0) continue;
            vals.Sort();
            float p25 = vals[vals.Count / 4];
            if (t.outlineWidth >= 0.1f) p25 = Mathf.Max(p25, Contrast(t.color, t.outline));
            if (p25 < MinContrast)
            {
                Color avg = sum / vals.Count;
                issues.Add(new Issue { state = st.name, element = t.path, res = lay.res.name + langTag, kind = "CONTRAST", severity = 2,
                    detail = $"{p25:0.0}:1 — текст #{ColorUtility.ToHtmlStringRGB(t.color)} на ~#{ColorUtility.ToHtmlStringRGB(avg)}: «{Snip(t.text)}»" });
            }
        }
        return issues;
    }

    // ---------------------------------------------------------------- Скриншоты (нужен GPU: batchmode без -nographics)

    /// <summary>Отрисовать сцену с UI в PNG: Canvas временно в WorldSpace перед камерой (сцена не сохраняется).</summary>
    public static bool Render(Scene s, Layout lay, string path, GameObject level = null)
    {
        Texture2D png = RenderToTexture(s, lay);
        if (png == null) return false;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        File.WriteAllBytes(path, png.EncodeToPNG());
        Object.DestroyImmediate(png);
        return true;
    }

    /// <summary>Кадр в текстуру (её нужно уничтожить вызывающему).</summary>
    public static Texture2D RenderToTexture(Scene s, Layout lay)
    {
        Camera cam = s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c => c.CompareTag("MainCamera"))
                     ?? s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
        if (cam == null) return null;
        Res r = lay.res;
        cam.aspect = r.w / (float)r.h;
        cam.cullingMask = ~0;
        float k = 2f * cam.orthographicSize / lay.canvasSize.y;
        foreach (Canvas c in lay.roots)
        {
            c.worldCamera = cam;
            c.sortingOrder += 1000;
            var rt = (RectTransform)c.transform;
            rt.position = cam.transform.position + cam.transform.forward * 5f;
            rt.rotation = cam.transform.rotation;
            rt.localScale = Vector3.one * k;
        }
        Canvas.ForceUpdateCanvases();
        var tex = new RenderTexture(r.w, r.h, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = tex;
        cam.Render();
        RenderTexture.active = tex;
        var png = new Texture2D(r.w, r.h, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, r.w, r.h), 0, 0);
        png.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        tex.Release();
        return png;
    }

    [MenuItem("Tools/Match & Flame/UI Lab/Screenshots (needs GPU)")]
    public static void Shots()
    {
        string tag = (Environment.GetEnvironmentVariable("UI_TAG") ?? "current") + ((Environment.GetEnvironmentVariable("UI_LANG") ?? "en") == "ru" ? "_ru" : "");
        string only = Environment.GetEnvironmentVariable("UI_RES") ?? "1080x1920,1080x2400,1200x1600";
        string level = Environment.GetEnvironmentVariable("UI_LEVEL") ?? "5";
        int ok = 0;
        foreach (State st in States())
            foreach (Res r in Resolutions.Where(x => only.Split(',').Contains(x.name)))
            {
                Scene s = EditorSceneManager.OpenScene(st.scene, OpenSceneMode.Single);
                st.setup(s);
                GameObject lvl = null;
                if (st.scene == Game)
                {
                    var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Resources/LevelCatalog.asset");
                    lvl = (GameObject)PrefabUtility.InstantiatePrefab(catalog.levels[int.Parse(level)], s);
                    lvl.transform.position = Vector3.zero;
                }
                EmulateRuntime(s, Environment.GetEnvironmentVariable("UI_LANG") ?? "en");
                if (lvl != null) LevelFraming.FrameLikeGame(lvl, r);
                Layout lay = Apply(s, r);
                if (Render(s, lay, $"Builds/ui-shots/{tag}/{System.IO.Path.GetFileNameWithoutExtension(st.scene)}_{st.name}_{r.name}.png", lvl)) ok++;
            }
        Debug.Log($"[Polish] UiAudit.Shots {tag}: {ok} PNG, graphics device {SystemInfo.graphicsDeviceType}");
    }

    private static string Fmt(Rect x) => $"({x.xMin:0},{x.yMin:0})-({x.xMax:0},{x.yMax:0})";

    // ---------------------------------------------------------------- Точки входа

    [MenuItem("Tools/Match & Flame/UI Lab/Audit layout")]
    public static void Audit()
    {
        string tag = Environment.GetEnvironmentVariable("UI_TAG") ?? "current";
        var all = new List<Issue>();
        CheckedInteractive = CheckedTexts = 0;
        foreach (State st in States())
            foreach (Res r in Resolutions)
            {
                Scene s = EditorSceneManager.OpenScene(st.scene, OpenSceneMode.Single);
                st.setup(s);
                Layout lay = Apply(s, r);
                all.AddRange(Check(s, st, lay));
                if (r.name == "1080x1920" || r.name == "800x1280") all.AddRange(CheckTexts(s, st, lay));
            }
        // Уникальные проблемы: одна строка на (состояние, элемент, вид) со списком разрешений
        var grouped = all.GroupBy(i => (i.state, i.element, i.kind))
            .Select(g => new { g.Key.state, g.Key.element, g.Key.kind, sev = g.Max(i => i.severity), res = string.Join(", ", g.Select(i => i.res).Distinct()), detail = g.First().detail })
            .OrderByDescending(x => x.sev).ThenBy(x => x.state).ThenBy(x => x.kind).ToList();
        var sb = new StringBuilder($"# UI audit ({tag})\n\nПроверено: {States().Count} состояний × {Resolutions.Length} разрешений, {CheckedInteractive} интерактивных элементов и {CheckedTexts} текстов (суммарно по прогонам).\n\nВсего записей {all.Count}, уникальных {grouped.Count}.\n\n");
        sb.AppendLine("| Вид | Кол-во записей |\n|---|---|");
        foreach (var k in all.GroupBy(i => i.kind).OrderBy(g => g.Key)) sb.AppendLine($"| {k.Key} | {k.Count()} |");
        sb.AppendLine("\n| Крит. | Экран | Проблема | Элемент | Разрешения | Пример |\n|---|---|---|---|---|---|");
        foreach (var g in grouped) sb.AppendLine($"| {g.sev} | {g.state} | {g.kind} | {g.element} | {g.res} | {g.detail} |");
        Directory.CreateDirectory("Builds");
        File.WriteAllText($"Builds/ui-audit-{tag}.md", sb.ToString());
        Debug.Log($"[Polish] UiAudit {tag}: checked {CheckedInteractive} interactive, {CheckedTexts} texts; {all.Count} records, {grouped.Count} unique; " + string.Join(", ", all.GroupBy(i => i.kind).Select(k => $"{k.Key} {k.Count()}")));
    }
}
