using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using UnityEngine.UI;

/// <summary>
/// Оценка нагрузки экранов без запуска игры (в batchmode UnityStats не работает): сколько видимых элементов UI,
/// сколько разных материалов/текстур (спрайты из атласа считаются одной текстурой) и сколько смен материала/текстуры
/// в порядке отрисовки — нижняя и верхняя граница числа батчей Canvas. Точное число — Frame Debugger на устройстве.
/// UiBudget.Report → Builds/ui-budget.md
/// </summary>
public static class UiBudget
{
    [MenuItem("Tools/Match & Flame/UI Lab/Draw call estimate")]
    public static void Report()
    {
        SpriteAtlas[] atlases = AssetDatabase.FindAssets("t:SpriteAtlas").Select(g => AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        var sb = new StringBuilder("# Оценка батчей по экранам (1080x1920)\n\n| Экран | Видимых UI-элементов | Батчи UI (от…до) | Спрайтов в мире | Текстур в мире |\n|---|---|---|---|---|\n");
        UiAudit.Res r = UiAudit.Resolutions[0];
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Resources/LevelCatalog.asset");
        foreach (UiAudit.State st in UiAudit.States())
        {
            Scene s = EditorSceneManager.OpenScene(st.scene, OpenSceneMode.Single);
            st.setup(s);
            if (st.scene == UiAudit.Game)
            {
                var lvl = (GameObject)PrefabUtility.InstantiatePrefab(catalog.levels[5], s);
                lvl.transform.position = Vector3.zero;
            }
            UiAudit.EmulateRuntime(s, "en");
            UiAudit.Apply(s, r);
            int visible = 0, distinct = 0, transitions = 0;
            foreach (Canvas c in s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>()).Where(c => c.isRootCanvas))
            {
                var keys = new HashSet<string>();
                string last = null;
                foreach (Graphic g in c.GetComponentsInChildren<Graphic>())
                {
                    if (!g.enabled || g.color.a < 0.01f || Hidden(g)) continue;
                    visible++;
                    string key = g.materialForRendering.name + "|" + Texture(g, atlases);
                    keys.Add(key);
                    if (key != last) transitions++;
                    last = key;
                }
                distinct += keys.Count;
            }
            var sprites = s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SpriteRenderer>()).Where(x => x.enabled && x.sprite != null).ToList();
            int worldTex = sprites.Select(x => Atlas(x.sprite, atlases) ?? x.sprite.texture.name).Distinct().Count();
            sb.AppendLine($"| {st.name} | {visible} | {distinct}…{transitions} | {sprites.Count} | {worldTex} |");
        }
        // Вес нового арта
        sb.AppendLine("\n## Новые текстуры\n\n| Файл | Размер, px | На диске, КБ |\n|---|---|---|");
        foreach (string dir in new[] { "Assets/Art/UI", "Assets/Art/Mechanics" })
            if (Directory.Exists(dir))
                foreach (string f in Directory.GetFiles(dir, "*.png"))
                {
                    var t = AssetDatabase.LoadAssetAtPath<Texture2D>(f.Replace('\\', '/'));
                    sb.AppendLine($"| {f.Replace('\\', '/')} | {t.width}×{t.height} | {new FileInfo(f).Length / 1024f:0} |");
                }
        Directory.CreateDirectory("Builds");
        File.WriteAllText("Builds/ui-budget.md", sb.ToString());
        Debug.Log("[Polish] UiBudget\n" + sb);
    }

    private static bool Hidden(Graphic g)
    {
        foreach (CanvasGroup cg in g.GetComponentsInParent<CanvasGroup>())
            if (cg.alpha < 0.01f) return true;
        return false;
    }

    private static string Texture(Graphic g, SpriteAtlas[] atlases)
    {
        if (g is Image img && img.sprite != null) return Atlas(img.sprite, atlases) ?? img.sprite.texture.name;
        return g.mainTexture != null ? g.mainTexture.name : "-";
    }

    private static string Atlas(Sprite sprite, SpriteAtlas[] atlases)
    {
        foreach (SpriteAtlas a in atlases) if (a.CanBindTo(sprite)) return "atlas:" + a.name;
        return null;
    }
}
