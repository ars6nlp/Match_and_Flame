using UnityEngine;

public class Oscillator : MonoBehaviour
{
    [Header("Настройки движения")]
    [Tooltip("Ось, по которой будет двигаться объект (например, (0, 1, 0) для движения вверх-вниз)")]
    public Vector3 movementAxis = new Vector3(0f, 1f, 0f);
    
    [Tooltip("Амплитуда (насколько далеко будет улетать)")]
    public float distance = 2f;
    
    [Tooltip("Скорость движения")]
    public float speed = 2f;

    [Tooltip("Время от начала уровня (LevelClock), а не от запуска игры: фаза качания совпадает с расписанием капель и вентиляторов. Старые уровни — выключено")]
    public bool useLevelClock = false;

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        // Движение по синусоиде дает плавный эффект туда-обратно (Ping-Pong)
        float offset = Mathf.Sin((useLevelClock ? LevelClock.Now : Time.time) * speed) * distance;
        transform.position = startPos + movementAxis * offset;
    }
}
