using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Главы на экране выбора уровня: сетка карточек превращается в список глав (заголовок с мотивом, названием и звёздами +
/// своя сетка). Индексы уровней и их кнопки не меняются — это только группировка. В конце — «Глава N — скоро».
/// При первом входе в новую главу — короткая сценка с огоньком (раз на главу).
/// </summary>
public static class ChapterUI
{
    private static readonly string[] Motifs = { "s_star", "out_watch", "out_bolt", "out_drop", "s_chest", "fl_core" };

    public static bool Enabled
    {
        get { RetentionSettings c = RetentionSettings.Current; return c.systemEnabled && c.chapters && c.chapterList.Count > 0; }
    }

    /// <summary>Перестроить контейнер; вернуть родителя для кнопки каждого уровня.</summary>
    public static Transform[] Build(Transform container, int count)
    {
        RetentionSettings cfg = RetentionSettings.Current;
        var grid = container.GetComponent<GridLayoutGroup>();
        var parents = new Transform[count];
        if (grid == null) { for (int i = 0; i < count; i++) parents[i] = container; return parents; }
        Vector2 cell = grid.cellSize, spacing = grid.spacing;
        GridLayoutGroup.Constraint constraint = grid.constraint;
        int cc = grid.constraintCount;
        TextAnchor align = grid.childAlignment;
        var pad = new RectOffset(grid.padding.left, grid.padding.right, grid.padding.top, grid.padding.bottom);
        Object.DestroyImmediate(grid);
        var v = container.gameObject.AddComponent<VerticalLayoutGroup>();
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        v.spacing = 26f;
        v.padding = pad;

        List<RetChapter> list = cfg.chapterList;
        Transform last = container;
        for (int k = 0; k < list.Count; k++)
        {
            RetChapter ch = list[k];
            if (ch.first >= count) break;
            int stars = 0, max = 0;
            for (int i = ch.first; i <= Mathf.Min(ch.last, count - 1); i++) { stars += PlayerPrefs.GetInt("Level_" + i + "_Stars", 0); max += 3; }
            Header(container, k, ch, stars, max, false);
            var block = new GameObject("ChapterGrid" + k, typeof(RectTransform));
            block.layer = 5;
            block.transform.SetParent(container, false);
            var g = block.AddComponent<GridLayoutGroup>();
            g.cellSize = cell; g.spacing = spacing; g.constraint = constraint; g.constraintCount = cc; g.childAlignment = align;
            for (int i = ch.first; i <= Mathf.Min(ch.last, count - 1); i++) parents[i] = block.transform;
            last = block.transform;
        }
        for (int i = 0; i < count; i++) if (parents[i] == null) parents[i] = last;
        if (!string.IsNullOrEmpty(RetentionSettings.Current.nextChapterRU)) Header(container, list.Count, null, 0, 0, true);   // пустой текст — без обещания следующей главы
        return parents;
    }

    private static void Header(Transform container, int k, RetChapter ch, int stars, int max, bool soon)
    {
        RetentionSettings cfg = RetentionSettings.Current;
        var go = new GameObject(soon ? "ChapterSoon" : "Chapter" + k, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(container, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = soon ? 130f : 170f; le.minHeight = le.preferredHeight;
        var img = go.AddComponent<Image>();
        img.sprite = cfg.card; img.type = Image.Type.Sliced; img.raycastTarget = false;
        Color col = soon ? new Color(0.5f, 0.42f, 0.38f) : ch.color;
        img.color = Color.Lerp(Color.white, col, 0.35f);
        RectTransform rt = (RectTransform)go.transform;
        Image band = RetUi.Img(rt, "Band", cfg.pill, new Color(col.r, col.g, col.b, 0.35f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, soon ? 80f : 110f), sliced: true);
        band.preserveAspect = false;
        RetUi.Img(rt, "Motif", RetUi.Art(soon ? "s_secret" : Motifs[ch.motif % Motifs.Length]), soon ? new Color(0f, 0f, 0f, 0.6f) : Color.white,
            new Vector2(0f, 0.5f), new Vector2(90f, 0f), new Vector2(soon ? 80f : 110f, soon ? 80f : 110f));
        if (soon)
        {
            int n = cfg.chapterList.Count + 1;
            RetUi.Text(rt, "Title", string.Format(cfg.nextChapterRU, n), string.Format(cfg.nextChapterEN, n), 40f, cfg.textLight, new Vector2(0.5f, 0.5f), new Vector2(40f, 0f), new Vector2(640f, 70f));
            return;
        }
        // «Замок» — только картинка: до главы ещё не дошли, но уровни не блокируются
        bool ahead = ch.first > 0 && PlayerPrefs.GetInt("Level_" + (ch.first - 1) + "_Stars", 0) == 0 && stars == 0;
        if (ahead) RetUi.Img(rt, "Lock", RetUi.Art("s_lock"), new Color(1f, 1f, 1f, 0.9f), new Vector2(1f, 0.5f), new Vector2(-80f, 0f), new Vector2(64f, 64f));
        RetUi.Text(rt, "Title", $"Глава {k + 1}. {ch.nameRU}", $"Chapter {k + 1}. {ch.nameEN}", 46f, cfg.title, new Vector2(0.5f, 0.5f), new Vector2(20f, 22f), new Vector2(640f, 64f), bold: true);
        RetUi.Text(rt, "Stars", $"Звёзды: {stars} из {max} · уровни {ch.first + 1}–{ch.last + 1}", $"Stars: {stars} of {max} · levels {ch.first + 1}–{ch.last + 1}",
            30f, cfg.textLight, new Vector2(0.5f, 0.5f), new Vector2(20f, -32f), new Vector2(640f, 44f));
    }

    /// <summary>Сценка «новая глава» — один раз, когда текущий уровень впервые оказывается в главе дальше первой.</summary>
    public static void MaybeIntro(int currentLevel)
    {
        if (!Enabled || !Retention.Active || currentLevel < 0) return;
        List<RetChapter> list = RetentionSettings.Current.chapterList;
        int k = list.FindIndex(c => currentLevel >= c.first && currentLevel <= c.last);
        if (k < 0) return;
        RetState s = Retention.Instance.S;
        if (s.chapterSeen >= k) return;
        bool first = s.chapterSeen < 0 && k == 0;
        s.chapterSeen = k;
        Retention.Instance.Save();
        if (first) return;
        var go = new GameObject("ChapterIntro");
        go.AddComponent<ChapterIntro>().Show(k, list[k]);
    }
}

public class ChapterIntro : MonoBehaviour
{
    private float closeAt;
    private RetFlame flame;

    public void Show(int k, RetChapter ch)
    {
        RetentionSettings cfg = RetentionSettings.Current;
        Canvas c = RetUi.Overlay("ChapterIntroCanvas", 70);
        c.transform.SetParent(transform, false);
        RectTransform root = RetUi.Fill(c.transform, "Root");
        var dim = root.gameObject.AddComponent<Image>();
        dim.color = new Color(0.08f, 0.03f, 0.02f, 0.9f);
        root.gameObject.AddComponent<CanvasGroup>();
        root.gameObject.AddComponent<UIPanelAnimator>();
        RetUi.HitArea(root.gameObject, () => Destroy(gameObject));
        RetUi.Img(root, "Glow", cfg.glow, new Color(ch.color.r, ch.color.g, ch.color.b, 0.5f), new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(900f, 900f));
        flame = RetFlame.Create(root, new Vector2(0f, 120f), 260f);
        ((RectTransform)flame.transform).anchorMin = ((RectTransform)flame.transform).anchorMax = new Vector2(0.5f, 0.5f);
        RetState s = Retention.Instance.S;
        flame.Setup(Mathf.Max(1, s.stage), Retention.Instance.core.LineOf(s.stage), s.equippedOutfit, 0.4f);
        flame.Hop();
        RetUi.Text(root, "Chapter", $"Глава {k + 1}", $"Chapter {k + 1}", 60f, cfg.textLight, new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(900f, 80f));
        RetUi.Text(root, "Name", ch.nameRU, ch.nameEN, 84f, cfg.title, new Vector2(0.5f, 0.5f), new Vector2(0f, -260f), new Vector2(960f, 110f), bold: true);
        RetUi.Text(root, "Hint", "Нажми, чтобы продолжить", "Tap to continue", 34f, cfg.textLight, new Vector2(0.5f, 0.5f), new Vector2(0f, -380f), new Vector2(900f, 60f));
        closeAt = Time.unscaledTime + 3.5f;
    }

    private void Update() { if (Time.unscaledTime >= closeAt) Destroy(gameObject); }

    private static ChapterIntro open;
    private void OnEnable() { open = this; }
    private void OnDestroy() { if (open == this) open = null; }

    /// <summary>Системная «Назад» на экране уровней: сначала закрыть вступление главы.</summary>
    public static bool CloseOpen()
    {
        if (open == null) return false;
        Destroy(open.gameObject);
        open = null;
        return true;
    }
}
