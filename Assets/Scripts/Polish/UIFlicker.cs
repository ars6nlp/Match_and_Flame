using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Мерцание UI-элемента «как от огня»: прозрачность (и по желанию масштаб) по шуму Перлина.
/// Держите элемент во вложенном Canvas — изменение цвета перестраивает его Canvas.
/// </summary>
[RequireComponent(typeof(Graphic))]
public class UIFlicker : MonoBehaviour
{
    [Range(0f, 1f)] public float alphaAmount = 0.35f;
    public float speed = 1.3f;
    [Range(0f, 0.3f)] public float scaleAmount = 0f;

    private Graphic graphic;
    private float baseAlpha;
    private Vector3 baseScale;
    private float seed;

    private void Awake()
    {
        graphic = GetComponent<Graphic>();
        baseAlpha = graphic.color.a;
        baseScale = transform.localScale;
        seed = Random.value * 100f;
    }

    private void Update()
    {
        float n = Mathf.PerlinNoise(seed, Time.unscaledTime * speed);
        Color c = graphic.color;
        c.a = baseAlpha * (1f - alphaAmount + alphaAmount * n);
        graphic.color = c;
        if (scaleAmount > 0f) transform.localScale = baseScale * (1f + scaleAmount * (n - 0.5f));
    }
}
