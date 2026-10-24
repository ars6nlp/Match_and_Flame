using UnityEngine;

public class FireOffset : MonoBehaviour
{
    [Tooltip("Смещение огня по X и Y. Если огонь слишком высоко, сделайте Y отрицательным (например -0.3)")]
    public Vector2 offset = new Vector2(0f, -0.3f);
}
