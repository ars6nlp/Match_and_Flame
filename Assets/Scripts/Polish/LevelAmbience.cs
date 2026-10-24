using UnityEngine;

/// <summary>
/// Фон уровня без постобработки: тёплое свечение по центру, виньетка по краям и редкие угольки.
/// Висит на дочернем объекте камеры и подгоняет размер под её видимую область
/// (камера может отдаляться, см. GameManager.FitCameraToLevel). Параметры — VisualTheme («фон уровня»).
/// </summary>
public class LevelAmbience : MonoBehaviour
{
    [SerializeField] private SpriteRenderer vignette;
    [SerializeField] private SpriteRenderer warmGlow;
    [SerializeField] private ParticleSystem embers;

    private Camera cam;
    private float lastSize = -1f, lastAspect = -1f;

    private void Start()
    {
        cam = GetComponentInParent<Camera>();
        VisualTheme theme = VisualTheme.Current;
        bool on = theme.levelAmbience;
        if (vignette != null)
        {
            vignette.enabled = on;
            vignette.color = WithAlpha(theme.charColor, theme.vignetteAlpha);
        }
        if (warmGlow != null)
        {
            warmGlow.enabled = on;
            warmGlow.color = WithAlpha(theme.flame, theme.warmGlowAlpha);
        }
        if (embers != null)
        {
            if (on && theme.ambientEmbers) embers.Play(); else embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        Fit();
    }

    private void LateUpdate()
    {
        if (cam != null && (cam.orthographicSize != lastSize || cam.aspect != lastAspect)) Fit();
    }

    private void Fit()
    {
        if (cam == null) return;
        lastSize = cam.orthographicSize;
        lastAspect = cam.aspect;
        float h = 2f * lastSize, w = h * lastAspect;
        Cover(vignette, w * 1.04f, h * 1.04f);
        float g = Mathf.Min(w, h) * 1.3f;
        Cover(warmGlow, g, g);
        if (embers != null)
        {
            ParticleSystem.ShapeModule shape = embers.shape;
            shape.scale = new Vector3(w, 0.5f, 1f);
            embers.transform.localPosition = new Vector3(0f, -lastSize, embers.transform.localPosition.z);
        }
    }

    private static void Cover(SpriteRenderer sr, float width, float height)
    {
        if (sr == null || sr.sprite == null) return;
        Vector2 s = sr.sprite.bounds.size;
        sr.transform.localScale = new Vector3(width / s.x, height / s.y, 1f);
    }

    private static Color WithAlpha(Color c, float a) { c.a = a; return c; }
}
