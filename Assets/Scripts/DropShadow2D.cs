using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class DropShadow2D : MonoBehaviour
{
    [Header("Настройки тени")]
    public Vector2 shadowOffset = new Vector2(0.15f, -0.15f); // Смещение вправо-вниз
    public Color shadowColor = new Color(0f, 0f, 0f, 0.4f); // Полупрозрачный черный
    
    [Tooltip("Насколько тень будет меньше/больше самой спички")]
    public Vector3 shadowScale = new Vector3(1f, 1f, 1f);

    private SpriteRenderer myRenderer;
    private SpriteRenderer shadowRenderer;
    private GameObject shadowObj;

    void Start()
    {
        myRenderer = GetComponent<SpriteRenderer>();

        // Создаем новый пустой объект для тени
        shadowObj = new GameObject("DropShadow");
        shadowObj.transform.SetParent(transform);
        
        // Позиция, поворот и размер
        shadowObj.transform.localPosition = shadowOffset;
        shadowObj.transform.localRotation = Quaternion.identity;
        shadowObj.transform.localScale = shadowScale;

        // Добавляем ему рисовальщик спрайтов
        shadowRenderer = shadowObj.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = myRenderer.sprite; // Копируем картинку спички
        shadowRenderer.color = shadowColor; // Красим в черный
        
        // Делаем так, чтобы тень рисовалась ЗА спичкой
        shadowRenderer.sortingLayerID = myRenderer.sortingLayerID;
        shadowRenderer.sortingOrder = myRenderer.sortingOrder - 1;
    }

    void Update()
    {
        // Если у спички во время горения меняется картинка (сгорает), тень тоже должна меняться!
        if (shadowRenderer.sprite != myRenderer.sprite)
        {
            shadowRenderer.sprite = myRenderer.sprite;
        }
    }
}
