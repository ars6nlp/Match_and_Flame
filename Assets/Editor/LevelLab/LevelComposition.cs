using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>Состав уровней каталога: спички, золото, иммунные, перетаскиваемые, маятники, слоты, CircleArranger, механики, подсказки.
/// Builds/level-composition.md</summary>
public static class LevelComposition
{
    [MenuItem("Tools/Match & Flame/Level Lab/Level composition")]
    public static void Run()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Resources/LevelCatalog.asset");
        var sb = new StringBuilder("| № | Префаб | Спичек | Золото | Иммунные | Перетаск. | Маятники | Слот | Вертушка | Вода/ветер/зонтик | Подсказка |\n|---|---|---|---|---|---|---|---|---|---|---|\n");
        for (int i = 0; i < catalog.levels.Length; i++)
        {
            GameObject p = catalog.levels[i];
            var m = p.GetComponentsInChildren<MatchLogic>(true).Where(x => x.GetComponent<DraggableMatch>() == null).ToList();
            int slots = p.GetComponentsInChildren<Transform>(true).Count(t => t.gameObject.layer == LayerMask.NameToLayer("Slot"));
            int circle = p.GetComponentsInChildren<MonoBehaviour>(true).Count(b => b != null && b.GetType().Name == "CircleArranger");
            string hint = string.Join(" / ", p.GetComponentsInChildren<TextMeshPro>(true).Select(t => t.text.Replace("\n", " ")));
            sb.AppendLine($"| {i} | {p.name} | {m.Count} | {m.Count(x => x.isGolden)} | {m.Count(x => x.ignorePlayerMatch)} | {m.Count(x => x.isDraggable)} | {p.GetComponentsInChildren<Oscillator>(true).Length} | {slots} | {circle} | " +
                      $"{p.GetComponentsInChildren<WaterDripper>(true).Length}/{p.GetComponentsInChildren<Fan>(true).Length}/{p.GetComponentsInChildren<WaterShelter>(true).Length} | {hint} |");
        }
        File.WriteAllText("Builds/level-composition.md", sb.ToString());
        LevelLab.Log("LevelComposition\n" + sb);
    }
}
