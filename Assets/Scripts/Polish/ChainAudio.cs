using UnityEngine;

/// <summary>
/// «Мелодия» цепной реакции: каждая следующая подожжённая спичка звучит чуть выше
/// (VisualTheme.chainPitchStep, потолок chainPitchMax). Счётчик сбрасывается в начале уровня.
/// </summary>
public static class ChainAudio
{
    private static int chain;

    public static void Reset() => chain = 0;

    public static void Advance() => chain++;

    public static float Pitch
    {
        get
        {
            VisualTheme theme = VisualTheme.Current;
            if (!theme.chainPitch) return 1f;
            return Mathf.Min(1f + Mathf.Max(0, chain - 1) * theme.chainPitchStep, theme.chainPitchMax);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => chain = 0;
}
