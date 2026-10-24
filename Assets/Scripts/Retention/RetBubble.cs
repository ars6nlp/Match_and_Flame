using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Пузырь реплики огонька: светлый, не больше 2 строк, ширина по тексту; появляется (пружинка), держится, гаснет.
/// Не масштабируется вместе с комнатой — кегль не мельче 14 sp. Без выделений в Update.
/// </summary>
public class RetBubble : MonoBehaviour
{
    private CanvasGroup group;
    private TextMeshProUGUI text;
    private RectTransform tail;
    private float until = -1f, shownAt, maxWidth = 620f, lastScale = -1f;

    private static HearthVisualSettings V => HearthVisualSettings.Current;

    /// <summary>anchor/pos — нижний край пузыря по центру (хвостик смотрит вниз); width — наибольшая ширина.</summary>
    public static RetBubble Create(Transform parent, Vector2 anchor, Vector2 pos, float width)
    {
        RectTransform rt = RetUi.Rect(parent, "Bubble", anchor, anchor, new Vector2(0.5f, 0f), pos, new Vector2(width, 120f));
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = RetUi.Art("ui_bubble"); img.type = Image.Type.Sliced; img.color = Color.white; img.raycastTarget = false;
        var b = rt.gameObject.AddComponent<RetBubble>();
        b.tail = RetUi.Img(rt, "Tail", RetUi.Art("ui_bubble_tail"), Color.white, new Vector2(0.5f, 0f), new Vector2(0f, -10f), new Vector2(40f, 28f)).rectTransform;
        b.tail.pivot = new Vector2(0.5f, 1f);
        b.tail.anchoredPosition = new Vector2(0f, 4f);
        b.group = rt.gameObject.AddComponent<CanvasGroup>();
        b.group.blocksRaycasts = false;
        b.maxWidth = width;
        b.text = HearthStyle.Text(rt, "Text", "", "", TextLevel.Body, V.ink, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(width - 56f, 104f));
        b.text.fontSize = b.text.fontSizeMax = 40f;
        b.group.alpha = 0f;
        return b;
    }

    // Смена языка — новая длина строки: пересчитать ширину пузыря
    private void OnEnable() { LanguageInit.LanguageChanged -= OnLanguage; LanguageInit.LanguageChanged += OnLanguage; }
    private void OnDisable() { LanguageInit.LanguageChanged -= OnLanguage; }
    private void OnLanguage()
    {
        if (text == null) return;
        var loc = text.GetComponent<LocalizeText>();
        if (loc != null) loc.UpdateText();   // порядок подписчиков не гарантирован: сначала новый текст, потом ширина
        Fit();
    }

    public void Say(FlameLine line, float seconds)
    {
        if (line == null) return;
        Say(line.ru, line.en, seconds);
    }

    public void Say(string ru, string en, float seconds)
    {
        RetUi.Set(text, ru, en);
        Fit();
        until = Time.unscaledTime + seconds;
        shownAt = Time.unscaledTime;
        gameObject.SetActive(true);
    }

    /// <summary>Ширина по тексту (до maxWidth), высота — 1 или 2 строки.</summary>
    private void Fit()
    {
        float inner = maxWidth - 80f;
        // По более длинному из RU/EN: при смене языка пузырь не обрезает строку
        var loc = text.GetComponent<LocalizeText>();
        string ru = loc != null && !string.IsNullOrEmpty(loc.textRU) ? loc.textRU : text.text;
        string en = loc != null && !string.IsNullOrEmpty(loc.textEN) ? loc.textEN : text.text;
        Vector2 oneRu = text.GetPreferredValues(ru, 100000f, 100000f), oneEn = text.GetPreferredValues(en, 100000f, 100000f);   // ширина без переноса
        float w = Mathf.Min(inner, Mathf.Max(oneRu.x, oneEn.x) + 4f);
        float lineH = Mathf.Max(oneRu.y, oneEn.y);
        float h = Mathf.Min(Mathf.Max(text.GetPreferredValues(ru, w, 100000f).y, text.GetPreferredValues(en, w, 100000f).y), lineH * 2.05f);
        var rt = (RectTransform)transform;
        rt.sizeDelta = new Vector2(Mathf.Max(200f, w + 72f), h + 34f);
        text.rectTransform.sizeDelta = new Vector2(w + 24f, h + 8f);   // запас: перенос TMP чуть шире расчётной строки
    }

    /// <summary>Нижний край по центру — в точку pos (координаты родителя); ширина не больше width.</summary>
    public void Place(Vector2 pos, float width)
    {
        var rt = (RectTransform)transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = pos + new Vector2(0f, 24f);
        if (!Mathf.Approximately(width, maxWidth)) { maxWidth = width; Fit(); }
    }

    /// <summary>Сдвинуть пузырь по x (не выходить за край), хвостик остаётся над огоньком.</summary>
    public void Shift(float dx)
    {
        var rt = (RectTransform)transform;
        rt.anchoredPosition += new Vector2(dx, 0f);
        tail.anchoredPosition = new Vector2(-dx, 4f);
    }

    public float Width => ((RectTransform)transform).sizeDelta.x;
    public float Height => ((RectTransform)transform).sizeDelta.y + 24f;
    public bool Showing => until > Time.unscaledTime;
    public void Hide() { until = Time.unscaledTime; }

    private void Update()
    {
        float t = Time.unscaledTime;
        float target = t < until ? 1f : 0f;
        if (group.alpha != target) group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 5f);
        float pop = Mathf.Clamp01((t - shownAt) * 6f);
        float s = t < until && V.uiAnimations ? 0.85f + 0.15f * Mathf.Sin(pop * Mathf.PI * 0.5f) + (pop < 1f ? 0.06f * Mathf.Sin(pop * Mathf.PI) : 0f) : 1f;
        if (s != lastScale) { lastScale = s; transform.localScale = new Vector3(s, s, 1f); }
    }
}

/// <summary>Звуки Очага из существующих клипов: разная высота, общий переключатель «Звук».</summary>
public static class HearthAudio
{
    private static AudioSource source;

    public static void Play(AudioClip clip, Vector2 pitch, float volume = 1f)
    {
        HearthVisualSettings hv = HearthVisualSettings.Current;
        if (clip == null || !hv.sounds || !GameAudio.SoundEnabled) return;
        if (source == null)
        {
            var go = new GameObject("HearthAudio");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
        }
        source.pitch = Random.Range(pitch.x, pitch.y);
        source.PlayOneShot(clip, hv.volume * volume);
    }

    public static void Tap() { HearthVisualSettings h = HearthVisualSettings.Current; Play(h.tapSound, h.tapPitch, 0.7f); }
    public static void Feed() { HearthVisualSettings h = HearthVisualSettings.Current; Play(h.feedSound, h.feedPitch); }
    public static void Crackle() { HearthVisualSettings h = HearthVisualSettings.Current; Play(h.crackleSound, new Vector2(0.9f, 1.05f)); }
    public static void Reward() { HearthVisualSettings h = HearthVisualSettings.Current; Play(h.rewardSound, new Vector2(1f, 1.15f)); }

    public static void Haptic()
    {
        if (HearthVisualSettings.Current.haptics) Haptics.Success();
    }
}
