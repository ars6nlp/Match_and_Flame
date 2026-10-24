using UnityEngine;

// Тег ExecuteInEditMode позволяет скрипту работать прямо в редакторе, без запуска игры
[ExecuteInEditMode]
public class CircleArranger : MonoBehaviour
{
    [Header("Настройки круга")]
    [Tooltip("Радиус круга")]
    public float radius = 2f;

    [Tooltip("Поставить галочку, чтобы обновить расстановку")]
    public bool updateLayout = false;

    void Update()
    {
        // Обновляем позиции только если нажали галочку в Inspector
        if (updateLayout)
        {
            ArrangeInCircle();
            updateLayout = false; // Сразу снимаем галочку
        }
    }

    void ArrangeInCircle()
    {
        int count = transform.childCount;
        float stepDegrees = 30f; // Жестко задаем шаг в 30 градусов

        for (int i = 0; i < count; i++)
        {
            // Вычисляем текущий угол в градусах (0, 30, 60, 90...)
            float currentAngleDeg = i * stepDegrees;

            // Переводим градусы в радианы, так как Mathf.Cos и Mathf.Sin работают только с радианами
            float currentAngleRad = currentAngleDeg * Mathf.Deg2Rad;

            // Вычисляем позицию на окружности
            Vector3 pos = new Vector3(Mathf.Cos(currentAngleRad), Mathf.Sin(currentAngleRad), 0) * radius;

            Transform child = transform.GetChild(i);
            child.localPosition = pos;

            // Поворачиваем спичку так, чтобы она лежала вдоль линии окружности
            // Тут радианы переводить не нужно, просто берем наши градусы и добавляем 90
            child.localRotation = Quaternion.Euler(0, 0, currentAngleDeg + 180f);
        }
    }
}