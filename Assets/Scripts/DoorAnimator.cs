using UnityEngine;
using System.Collections;

/// <summary>
/// Двери внутри сцены (фон панели победы). Створки раскладываются якорями от размера родителя (DoorLayout),
/// поэтому закрываются целиком на любом экране; анимация на unscaled time — не замирает при timeScale = 0.
/// Двери начала уровня (openOnStart) при включённом переходе между сценами не нужны: их открывает SceneTransition.
/// </summary>
public class DoorAnimator : MonoBehaviour
{
    public RectTransform leftDoor;
    public RectTransform rightDoor;

    [Tooltip("Секунд на закрытие/открытие")]
    [SerializeField] private float duration = 0.45f;

    [Tooltip("Насколько закрытые створки заходят друг за друга в центре, доля ширины экрана")]
    [Range(0.02f, 0.08f)] [SerializeField] private float overlap = 0.04f;

    [Tooltip("Если включить, двери будут ОТКРЫВАТЬСЯ при старте. Если выключить - ЗАКРЫВАТЬСЯ.")]
    public bool openOnStart = false;

    private void OnEnable()
    {
        if (leftDoor == null || rightDoor == null) return;
        if (openOnStart && SceneTransition.Enabled) { gameObject.SetActive(false); return; }
        if (rightDoor.localScale.x > 0f) rightDoor.localScale = new Vector3(-rightDoor.localScale.x, rightDoor.localScale.y, 1f);
        StopAllCoroutines();
        StartCoroutine(Animate(openOnStart ? 1f : 0f, openOnStart ? 0f : 1f));
    }

    private IEnumerator Animate(float from, float to)
    {
        float t = 0f;
        DoorLayout.Apply(leftDoor, rightDoor, from, overlap);
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.05f, duration);
            DoorLayout.Apply(leftDoor, rightDoor, Mathf.Lerp(from, to, DoorLayout.Ease(t)), overlap);
            yield return null;
        }
        DoorLayout.Apply(leftDoor, rightDoor, to, overlap);
        if (openOnStart) gameObject.SetActive(false); // двери старта уровня не мешают играть
    }
}
