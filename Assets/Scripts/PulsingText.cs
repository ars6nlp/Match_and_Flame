using UnityEngine;
using TMPro;

public class PulsingText : MonoBehaviour
{
    [Tooltip("Скорость мигания")]
    public float speed = 2f;
    
    [Tooltip("Минимальная прозрачность (от 0 до 1)")]
    public float minAlpha = 0.3f;
    
    [Tooltip("Максимальная прозрачность (от 0 до 1)")]
    public float maxAlpha = 1f;

    private TextMeshProUGUI textUI;
    private TextMeshPro textWorld;

    void Start()
    {
        // Пытаемся получить любой из двух видов текста (UI или обычный)
        textUI = GetComponent<TextMeshProUGUI>();
        textWorld = GetComponent<TextMeshPro>();
    }

    void Update()
    {
        // Mathf.PingPong плавно бегает от 0 до 1 и обратно
        float pingPong = Mathf.PingPong(Time.time * speed, 1f);
        
        // Переводим это в наш диапазон (от minAlpha до maxAlpha)
        float currentAlpha = Mathf.Lerp(minAlpha, maxAlpha, pingPong);

        // Применяем прозрачность
        if (textUI != null)
        {
            Color c = textUI.color;
            c.a = currentAlpha;
            textUI.color = c;
        }
        else if (textWorld != null)
        {
            Color c = textWorld.color;
            c.a = currentAlpha;
            textWorld.color = c;
        }
    }
}
