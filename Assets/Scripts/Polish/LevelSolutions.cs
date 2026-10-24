using UnityEngine;

/// <summary>
/// Задуманные решения уровней для подсказки: поза КОРНЯ спички игрока (как у объекта Match_Normal).
/// Для уровней со слотом решение не нужно — подсказка берёт позу из слота.
/// Заполняется Editor-симулятором (LevelLab), ассет: Assets/Resources/LevelSolutions.asset.
/// </summary>
[CreateAssetMenu(fileName = "LevelSolutions", menuName = "Match & Flame/Level Solutions")]
public class LevelSolutions : ScriptableObject
{
    [System.Serializable]
    public struct Solution
    {
        public int level;
        public Vector2 position;
        public float rotationZ;
    }

    public Solution[] solutions;

    private static LevelSolutions current;
    private static bool loaded;

    public static LevelSolutions Current
    {
        get
        {
            if (!loaded) { current = Resources.Load<LevelSolutions>("LevelSolutions"); loaded = true; }
            return current;
        }
    }

    public bool TryGet(int level, out Solution solution)
    {
        if (solutions != null)
            foreach (Solution s in solutions)
                if (s.level == level) { solution = s; return true; }
        solution = default;
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { current = null; loaded = false; }
}
