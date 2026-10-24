using UnityEngine;

/// <summary>
/// Единый источник списка уровней (L-03): GameScene берёт префабы отсюда,
/// выбор уровня — их число. Ассет: Assets/Resources/LevelCatalog.asset.
/// Новые уровни добавляются только в конец: индекс = ключ сохранения Level_{i}_Stars.
/// </summary>
[CreateAssetMenu(fileName = "LevelCatalog", menuName = "Match & Flame/Level Catalog")]
public class LevelCatalog : ScriptableObject
{
    private const string ResourcePath = "LevelCatalog";
    private static LevelCatalog current;
    private static bool loaded;

    public static LevelCatalog Current
    {
        get
        {
            if (!loaded) { current = Resources.Load<LevelCatalog>(ResourcePath); loaded = true; }
            return current;
        }
    }

    [Tooltip("Префабы уровней по порядку. Не переставлять: индекс привязан к сохранённым звёздам")]
    public GameObject[] levels;

    [Tooltip("Сложность для карточки уровня: 1–3 огонька (пересчитывается Editor-скриптом)")]
    public int[] difficulty;

    public int Count => levels != null ? levels.Length : 0;

    public int GetDifficulty(int index) =>
        difficulty != null && index >= 0 && index < difficulty.Length ? Mathf.Clamp(difficulty[index], 1, 3) : 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { current = null; loaded = false; }
}
