using UnityEngine;

/// <summary>
/// Блеск золотой спички до поджога: пульсация цвета к белому и искорки (GameJuice).
/// Добавляется MatchLogic автоматически, если VisualTheme.goldenShimmer включён.
/// </summary>
public class GoldenShimmer : MonoBehaviour
{
    private MatchLogic match;
    private SpriteRenderer sprite;
    private Color baseColor;
    private PooledEffect sparkle;
    private float phase;

    public void Init(MatchLogic owner, SpriteRenderer renderer)
    {
        match = owner;
        sprite = renderer;
        baseColor = renderer.color;
        phase = Random.value * 10f;
        sparkle = GameJuice.AttachGoldenSparkle(transform, GameJuice.Center(owner));
    }

    private void Update()
    {
        if (match == null || sprite == null) { enabled = false; return; }
        if (match.isBurning || match.isBurnedOut)
        {
            Stop();
            return;
        }
        VisualTheme theme = VisualTheme.Current;
        float k = theme.goldenPulseAmount * (0.5f + 0.5f * Mathf.Sin((Time.time + phase) * theme.goldenPulseSpeed));
        sprite.color = Color.Lerp(baseColor, Color.white, k);
    }

    private void Stop()
    {
        if (sprite != null && !match.isBurnedOut) sprite.color = baseColor;
        GameJuice.Release(sparkle);
        sparkle = null;
        enabled = false;
    }

    private void OnDestroy()
    {
        GameJuice.Release(sparkle);
    }
}
