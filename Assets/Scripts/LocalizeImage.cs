using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Подменяет спрайт в зависимости от языка платформы.
/// Язык берётся из <see cref="LanguageInit"/> — тот же источник, что и у LocalizeText,
/// то есть ysdk.environment.i18n.lang через плагин.
///
/// Вешать только на те Image, спрайт которых НЕ меняется в рантайме из кода.
/// Кнопки магазина сюда не годятся — там спрайт переключает ShopManager,
/// и у него для этого есть свои русские поля.
/// </summary>
[RequireComponent(typeof(Image))]
public class LocalizeImage : MonoBehaviour
{
    [Tooltip("Основной спрайт — английский")]
    public Sprite spriteEN;

    [Tooltip("Русский вариант. Оставьте пустым, чтобы показывать английский")]
    public Sprite spriteRU;

    private Image image;

    void Awake()
    {
        image = GetComponent<Image>();

        // Если английский вариант не заполнили вручную — берём тот, что уже стоит на Image
        if (spriteEN == null && image != null) spriteEN = image.sprite;
    }

    void OnEnable()
    {
        LanguageInit.LanguageChanged += UpdateSprite;
        UpdateSprite();
    }

    void OnDisable()
    {
        LanguageInit.LanguageChanged -= UpdateSprite;
    }

    public void UpdateSprite()
    {
        if (image == null) return;

        Sprite target = spriteEN;

        if (LanguageInit.CurrentLang == "ru" && spriteRU != null)
        {
            target = spriteRU;
        }

        // Если английский вариант забыли назначить — лучше показать русский, чем пустоту
        if (target == null) target = spriteRU;

        if (target != null) image.sprite = target;
    }
}
