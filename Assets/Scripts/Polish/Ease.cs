using UnityEngine;

/// <summary>Функции сглаживания для твинов. t в диапазоне 0..1.</summary>
public static class Ease
{
    public enum Type { Linear, OutQuad, OutCubic, OutBack, InQuad, InOutSine }

    public static float Evaluate(Type type, float t, float overshoot = 1.70158f)
    {
        t = Mathf.Clamp01(t);
        switch (type)
        {
            case Type.OutQuad: return 1f - (1f - t) * (1f - t);
            case Type.OutCubic: { float u = 1f - t; return 1f - u * u * u; }
            case Type.OutBack: { float u = t - 1f; return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u; }
            case Type.InQuad: return t * t;
            case Type.InOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
            default: return t;
        }
    }
}
