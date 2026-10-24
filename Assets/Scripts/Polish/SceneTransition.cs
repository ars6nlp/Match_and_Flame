using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Переход между сценами «дверями»: створки закрываются, сцена грузится в фоне (LoadSceneAsync) и включается только
/// после полного закрытия, затем створки открываются. Свой Canvas поверх всего (sorting order 32760), живёт между сценами.
/// Пока двери не открыты полностью, прозрачный блокер ловит касания. Анимация на unscaled time — не зависит от timeScale.
/// Параметры — VisualTheme, «Переходы между сценами: двери»; sceneDoors = выкл — загрузка сразу, как раньше.
/// </summary>
public class SceneTransition : MonoBehaviour
{
    public static bool Enabled => VisualTheme.Current != null && VisualTheme.Current.sceneDoors && VisualTheme.Current.doorSprite != null;
    /// <summary>Двери не полностью открыты (движутся или закрыты).</summary>
    public static bool Busy => instance != null && instance.busy;
    /// <summary>Текущее положение створок: 0 — открыты, 1 — закрыты (для проверок).</summary>
    public static float Closed => instance != null ? instance.closed : 0f;

    private static SceneTransition instance;
    private RectTransform left, right;
    private Image blocker;
    private bool busy;
    private float closed;

    public static void Load(string sceneName, int buildIndex, System.Action onDone)
    {
        Ensure().StartCoroutine(instance.Run(sceneName, buildIndex, onDone));
    }

    /// <summary>Закрыть двери и оставить закрытыми (проверка в редакторе).</summary>
    public static void CloseOnly() => Ensure().StartCoroutine(instance.Move(instance.closed, 1f, VisualTheme.Current.doorCloseSeconds));
    /// <summary>Открыть двери (проверка в редакторе).</summary>
    public static void OpenOnly() => Ensure().StartCoroutine(instance.Move(instance.closed, 0f, VisualTheme.Current.doorOpenSeconds));

    private static SceneTransition Ensure()
    {
        if (instance != null) return instance;
        var go = new GameObject("SceneTransition", typeof(RectTransform));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<SceneTransition>();
        Build(go, out instance.left, out instance.right, out instance.blocker);
        instance.SetClosed(0f);
        instance.SetBlocking(false);
        return instance;
    }

    /// <summary>Собрать Canvas дверей на объекте (игра и превью в редакторе — DoorCheck).</summary>
    public static void Build(GameObject gameObject, out RectTransform left, out RectTransform right, out Image blocker)
    {
        VisualTheme theme = VisualTheme.Current;
        var canvas = gameObject.AddComponent<Canvas>();
        Transform transform = gameObject.transform; // после AddComponent<Canvas>: Transform заменён на RectTransform
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32760; // поверх HUD, панелей, онбординга и FX
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        gameObject.AddComponent<GraphicRaycaster>();

        // Блокер касаний — на весь экран, без SafeArea (вырез и жестовая панель тоже)
        blocker = NewImage("Blocker", transform, null, new Color(0f, 0f, 0f, 0f));
        Stretch(blocker.rectTransform);
        blocker.raycastTarget = true;

        // Правая створка снизу, левая сверху: на левой накладка шва и тень на правую
        right = NewImage("RightDoor", transform, theme.doorSprite, Color.white).rectTransform;
        right.localScale = new Vector3(-1f, 1f, 1f);
        left = NewImage("LeftDoor", transform, theme.doorSprite, Color.white).rectTransform;

        float leaf = DoorLayout.LeafWidth(theme.doorOverlap);
        if (theme.doorShadowAlpha > 0f)
        {
            Image shadow = NewImage("SeamShadow", left, ShadowSprite(), new Color(0f, 0f, 0f, theme.doorShadowAlpha));
            shadow.rectTransform.anchorMin = new Vector2(1f, 0f);
            shadow.rectTransform.anchorMax = new Vector2(1f + 0.05f / leaf, 1f);
            ZeroOffsets(shadow.rectTransform);
        }
        if (theme.doorSeamWidth > 0f)
        {
            float s = theme.doorSeamWidth / leaf;
            Image seam = NewImage("Seam", left, null, theme.doorSeamColor);
            seam.rectTransform.anchorMin = new Vector2(1f - s, 0f);
            seam.rectTransform.anchorMax = new Vector2(1f, 1f);
            ZeroOffsets(seam.rectTransform);
            Image edge = NewImage("SeamEdge", seam.rectTransform, null, theme.doorSeamEdgeColor);
            edge.rectTransform.anchorMin = new Vector2(0f, 0f);
            edge.rectTransform.anchorMax = new Vector2(0.18f, 1f);
            ZeroOffsets(edge.rectTransform);
        }
    }

    private IEnumerator Run(string sceneName, int buildIndex, System.Action onDone)
    {
        VisualTheme theme = VisualTheme.Current;
        busy = true;
        SetBlocking(true);
        AsyncOperation op = sceneName != null ? SceneManager.LoadSceneAsync(sceneName) : SceneManager.LoadSceneAsync(buildIndex);
        if (op == null) { SceneLoadGuard.Cancel(); yield return Move(closed, 0f, theme.doorOpenSeconds); busy = false; SetBlocking(false); onDone?.Invoke(); yield break; }
        op.allowSceneActivation = false; // старая сцена рисуется, пока двери закрываются
        yield return Move(closed, 1f, theme.doorCloseSeconds);
        while (op.progress < 0.9f) yield return null;
        bool loaded = false;
        void OnLoaded(Scene s, LoadSceneMode m) { loaded = true; }
        SceneManager.sceneLoaded += OnLoaded;
        op.allowSceneActivation = true; // только когда двери закрыты полностью
        while (!loaded || !op.isDone) yield return null;
        SceneManager.sceneLoaded -= OnLoaded;
        onDone?.Invoke();
        float hold = theme.doorHoldSeconds;
        while (hold > 0f) { hold -= Time.unscaledDeltaTime; yield return null; }
        yield return null; // первый кадр новой сцены (раскладка, Start)
        yield return Move(1f, 0f, theme.doorOpenSeconds);
        busy = false;
        SetBlocking(false);
    }

    private IEnumerator Move(float from, float to, float seconds)
    {
        busy = true;
        SetBlocking(true);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.05f, seconds);
            SetClosed(Mathf.Lerp(from, to, DoorLayout.Ease(t)));
            yield return null;
        }
        SetClosed(to);
        if (to <= 0f) { busy = false; SetBlocking(false); }
    }

    private void SetClosed(float value)
    {
        closed = value;
        DoorLayout.Apply(left, right, value, VisualTheme.Current.doorOverlap);
        bool visible = value > 0.001f;
        left.gameObject.SetActive(visible);
        right.gameObject.SetActive(visible);
    }

    private void SetBlocking(bool on) => blocker.raycastTarget = on;

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; ZeroOffsets(rt); }
    private static void ZeroOffsets(RectTransform rt) { rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }

    private static Sprite shadowSprite;
    /// <summary>Горизонтальный градиент тени 64×4 (слева тёмный, справа прозрачный) — создаётся один раз.</summary>
    private static Sprite ShadowSprite()
    {
        if (shadowSprite != null) return shadowSprite;
        var tex = new Texture2D(64, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "DoorSeamShadow" };
        var px = new Color32[64 * 4];
        for (int x = 0; x < 64; x++)
        {
            float a = Mathf.Pow(1f - x / 63f, 2f);
            for (int y = 0; y < 4; y++) px[y * 64 + x] = new Color32(255, 255, 255, (byte)(a * 255));
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        shadowSprite = Sprite.Create(tex, new Rect(0, 0, 64, 4), new Vector2(0.5f, 0.5f));
        return shadowSprite;
    }
}
