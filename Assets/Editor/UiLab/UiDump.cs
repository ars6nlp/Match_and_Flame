using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Выгрузка дерева UI трёх сцен (якоря, размеры, компоненты) — для диагностики раскладки. Файл Builds/ui-dump.txt.</summary>
public static class UiDump
{
    public static readonly string[] Scenes = { "Assets/Scenes/MainScene.unity", "Assets/Scenes/LevelSelectionScene.unity", "Assets/Scenes/GameScene.unity" };

    public static void Run()
    {
        var sb = new StringBuilder();
        foreach (string path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            sb.AppendLine("##### " + path);
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Canvas c in root.GetComponentsInChildren<Canvas>(true).Where(c => c.isRootCanvas))
                    Dump(c.transform, 0, sb);
            foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine($"  CAMERA {cam.name} ortho {cam.orthographic} size {cam.orthographicSize} pos {cam.transform.position}");
        }
        Directory.CreateDirectory("Builds");
        File.WriteAllText("Builds/ui-dump.txt", sb.ToString());
        Debug.Log("[Polish] UiDump written " + sb.Length);
    }

    private static void Dump(Transform t, int depth, StringBuilder sb)
    {
        var rt = t as RectTransform;
        string ind = new string(' ', depth * 2);
        var comps = t.GetComponents<Component>().Where(c => c != null && !(c is Transform) && !(c is CanvasRenderer))
            .Select(c => c.GetType().Name).ToList();
        string extra = "";
        var g = t.GetComponent<Graphic>();
        if (g != null) extra += $" rc={(g.raycastTarget ? 1 : 0)} col={ColorUtility.ToHtmlStringRGBA(g.color)}";
        var img = t.GetComponent<Image>();
        if (img != null && img.sprite != null) extra += $" spr={img.sprite.name}";
        var tmp = t.GetComponent<TMP_Text>();
        if (tmp != null) extra += $" text=\"{tmp.text.Replace("\n", "\\n")}\" fs={tmp.fontSize}{(tmp.enableAutoSizing ? " auto" : "")} wrap={tmp.textWrappingMode}";
        var c = t.GetComponent<Canvas>();
        if (c != null) extra += $" canvas mode={c.renderMode} order={c.sortingOrder} override={c.overrideSorting}";
        if (rt != null)
            sb.AppendLine($"{ind}{(t.gameObject.activeSelf ? "" : "[off] ")}{t.name} a=({rt.anchorMin.x:0.##},{rt.anchorMin.y:0.##})-({rt.anchorMax.x:0.##},{rt.anchorMax.y:0.##}) piv=({rt.pivot.x:0.##},{rt.pivot.y:0.##}) pos=({rt.anchoredPosition.x:0},{rt.anchoredPosition.y:0}) size=({rt.sizeDelta.x:0},{rt.sizeDelta.y:0}) sc={rt.localScale.x:0.##} [{string.Join(",", comps)}]{extra}");
        else
            sb.AppendLine($"{ind}{(t.gameObject.activeSelf ? "" : "[off] ")}{t.name} (Transform) [{string.Join(",", comps)}]");
        foreach (Transform ch in t) Dump(ch, depth + 1, sb);
    }
}
