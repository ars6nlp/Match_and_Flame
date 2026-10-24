using UnityEngine;

/// <summary>
/// Короткая тряска камеры (последняя спичка, победа). Амплитуда и длительность — VisualTheme
/// («тряска камеры»); выключается флагом cameraShake. Смещение затухает и камера возвращается точно на место.
/// </summary>
public class CameraShake : MonoBehaviour
{
    private Vector3 basePosition;
    private float time = -1f, duration, amplitude;

    public void Shake(float amp, float dur)
    {
        if (!VisualTheme.Current.cameraShake || amp <= 0f || dur <= 0f) return;
        if (time < 0f) basePosition = transform.localPosition;
        amplitude = Mathf.Max(amplitude * (time < 0f ? 0f : 1f), amp);
        duration = dur;
        time = 0f;
    }

    private void LateUpdate()
    {
        if (time < 0f) return;
        time += Time.unscaledDeltaTime;
        if (time >= duration)
        {
            transform.localPosition = basePosition;
            time = -1f;
            amplitude = 0f;
            return;
        }
        float fade = 1f - time / duration;
        float t = time * 30f;
        var offset = new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f) * (2f * amplitude * fade * fade);
        transform.localPosition = basePosition + offset;
    }
}
