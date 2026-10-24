using UnityEngine;

/// <summary>
/// Фон сцены уровня рассчитан на камеру исходного размера. Когда камера отдаляется (вытянутые экраны,
/// высокие уровни), фон растягивается так, чтобы закрыть кадр без тёмных полос; при исходном размере не меняется.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundCover : MonoBehaviour
{
    private Camera cam;
    private Vector3 baseScale;
    private Vector2 baseSize;
    private float lastSize = -1f, lastAspect = -1f;

    private void Awake()
    {
        baseScale = transform.localScale;
        baseSize = GetComponent<SpriteRenderer>().bounds.size;
    }

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || (cam.orthographicSize == lastSize && cam.aspect == lastAspect)) return;
        lastSize = cam.orthographicSize;
        lastAspect = cam.aspect;
        Fit(cam);
    }

    public void Fit(Camera camera)
    {
        if (baseSize == Vector2.zero) Awake();
        Vector3 offset = transform.position - camera.transform.position;
        float h = 2f * (camera.orthographicSize + Mathf.Abs(offset.y));
        float w = 2f * (camera.orthographicSize * camera.aspect + Mathf.Abs(offset.x));
        float k = Mathf.Max(1f, h / baseSize.y, w / baseSize.x);
        transform.localScale = new Vector3(baseScale.x * k, baseScale.y * k, baseScale.z);
    }
}
