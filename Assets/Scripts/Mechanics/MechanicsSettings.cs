using UnityEngine;

/// <summary>
/// Общие настройки механик «капля» и «вентилятор»: вид мокрой/потушенной спички, наклон пламени на ветру,
/// звуки, эффекты. Ассет: Assets/Resources/MechanicsSettings.asset. Параметры конкретного источника
/// и вентилятора — на их компонентах (WaterDripper, Fan) в префабе уровня.
/// </summary>
[CreateAssetMenu(fileName = "MechanicsSettings", menuName = "Match & Flame/Mechanics Settings")]
public class MechanicsSettings : ScriptableObject
{
    private const string ResourcePath = "MechanicsSettings";
    private static MechanicsSettings current;

    public static MechanicsSettings Current
    {
        get
        {
            if (current == null) current = Resources.Load<MechanicsSettings>(ResourcePath);
            if (current == null) current = CreateInstance<MechanicsSettings>(); // значения по умолчанию
            return current;
        }
    }

    [Header("Мокрая спичка")]
    [Tooltip("Цвет спрайта мокрой спички (умножается на исходный)")]
    public Color wetTint = new Color(0.62f, 0.72f, 0.86f, 1f);
    [Tooltip("Сколько секунд цвет возвращается к сухому в конце намокания")]
    public float dryFadeTime = 0.6f;

    [Header("Потушенная спичка")]
    [Tooltip("Цвет спрайта потушенной спички: недогоревшая, тёмная")]
    public Color extinguishedTint = new Color(0.45f, 0.4f, 0.38f, 1f);

    [Header("Пламя на ветру")]
    [Tooltip("Наклон пламени в градусах на единицу силы ветра")]
    public float fireTiltPerStrength = 12f;
    [Tooltip("Наибольший наклон пламени, градусы")]
    public float fireMaxTilt = 55f;
    [Tooltip("Частота мигания пламени перед тем, как ветер его задует, Гц")]
    public float blowOutFlickerHz = 14f;

    [Header("Эффекты (Assets/Prefabs/Mechanics/FX)")]
    public ParticleSystem splashFx;
    public ParticleSystem steamFx;
    public ParticleSystem blowOutSmokeFx;

    [Header("Звуки (пусто — без звука). Нужны файлы: см. MECHANICS.md")]
    [Tooltip("ЗАГЛУШКА: шипение при тушении каплей")]
    public AudioClip extinguishHiss;
    [Tooltip("ЗАГЛУШКА: «пф» — ветер задул пламя")]
    public AudioClip blowOutPuff;
    [Tooltip("ЗАГЛУШКА: капля упала (кап)")]
    public AudioClip dripDrop;
    [Range(0f, 1f)] public float sfxVolume = 0.7f;

    [Header("Вибрация")]
    [Tooltip("Короткая «ошибка» при тушении")]
    public bool vibrateOnExtinguish = true;

    [Header("Отладка")]
    [Tooltip("Gizmos в окне Scene: зоны ветра (сила цветом, красным — где задувает) и траектории капель")]
    public bool debugGizmos = true;
}
