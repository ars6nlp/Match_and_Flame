using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonClickSound : MonoBehaviour
{
    [Tooltip("Уникальный звук для этой кнопки. Если оставить пустым, будет играть стандартный из SoundManager.")]
    public AudioClip customSound;

    void Start()
    {
        // Автоматически находим компонент Button на этом объекте
        Button btn = GetComponent<Button>();
        
        if (btn != null)
        {
            // Добавляем вызов звука через код, чтобы не делать это вручную в Инспекторе!
            btn.onClick.AddListener(PlaySound);
        }
    }

    private void PlaySound()
    {
        if (SoundManager.Instance != null)
        {
            if (customSound != null)
            {
                // Если у кнопки есть свой звук, играем его
                SoundManager.Instance.PlaySound(customSound);
            }
            else
            {
                // Иначе играем стандартный звук клика
                SoundManager.Instance.PlayClick();
            }
        }
    }
}
