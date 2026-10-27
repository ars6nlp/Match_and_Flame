using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Превью уровней без графического устройства (batchmode -nographics): выгружает спрайты сцены/уровней в JSON
/// (файл исходной картинки, прямоугольник и пивот в пикселях, PPU, мировая матрица, цвет, порядок),
/// а Tools/preview/compose.py собирает из них PNG в кадре 9:16. Результат — Builds/preview/*.png.
/// </summary>
public static class LevelPreview
{
    public const string OutDir = "Builds/preview";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    [MenuItem("Tools/Match & Flame/Level Lab/Export level previews (JSON)")]
    public static void ExportAll()
    {
        Directory.CreateDirectory(OutDir);
        // Фон и камера GameScene
        Scene game = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        Camera cam = Object.FindFirstObjectByType<Camera>();
        var bg = new StringBuilder();
        bg.Append("{\"camera\":{\"size\":").Append(F(cam.orthographicSize))
          .Append(",\"x\":").Append(F(cam.transform.position.x)).Append(",\"y\":").Append(F(cam.transform.position.y))
          .Append(",\"bg\":").Append(Col(cam.backgroundColor)).Append("},\"sprites\":[");
        AppendRenderers(bg, game.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SpriteRenderer>(false))
            .Where(r => r.GetComponentInParent<Canvas>() == null && r.GetComponentInParent<MatchLogic>() == null));
        bg.Append("]}");
        File.WriteAllText(Path.Combine(OutDir, "scene.json"), bg.ToString());

        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Resources/LevelCatalog.asset");
        for (int i = 0; i < catalog.levels.Length; i++) ExportLevel(catalog.levels[i], $"level_{i:00}");
        LevelLab.Log($"LevelPreview: exported {catalog.levels.Length} levels to {OutDir}");
    }

    /// <summary>Демо механик: кран над спичкой, зонтик над спичкой, вентилятор дует на спички.</summary>
    [MenuItem("Tools/Match & Flame/Level Lab/Export mechanics demo preview")]
    public static void ExportMechanicsDemo()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("MechanicsDemo");
        GameObject P(string path, Vector2 pos, float rot = 0f, float sx = 1f)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), root.transform);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, 0, rot));
            go.transform.localScale = new Vector3(sx, 1f, 1f);
            return go;
        }
        LevelLab.NewMatch(root.transform, new Vector2(0.4f, 0.6f), 0f, false, "M1");
        LevelLab.NewMatch(root.transform, new Vector2(0.4f, -0.4f), 0f, false, "M2").isGolden = true;
        P("Assets/Prefabs/Mechanics/WaterDripper.prefab", new Vector2(0.4f, 2.4f));
        P("Assets/Prefabs/Mechanics/WaterShelter.prefab", new Vector2(-1.2f, -1.2f));
        LevelLab.NewMatch(root.transform, new Vector2(-1.2f, -1.7f), 0f, false, "M3");
        P("Assets/Prefabs/Mechanics/Fan.prefab", new Vector2(-1.5f, -3.2f));
        LevelLab.NewMatch(root.transform, new Vector2(0.9f, -3.1f), 90f, false, "M4");
        P("Assets/Prefabs/Mechanics/Fan.prefab", new Vector2(1.6f, 1.6f), 0f, -1f);
        ExportRoot(root, "mechanics_demo");
        Object.DestroyImmediate(root);
    }

    /// <summary>Выгрузить один корень (уровень, префаб механики) в JSON.</summary>
    public static void ExportLevel(GameObject prefab, string name, System.Action<GameObject> beforeExport = null)
    {
        Directory.CreateDirectory(OutDir);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.position = Vector3.zero;
        beforeExport?.Invoke(go);
        ExportRoot(go, name);
        Object.DestroyImmediate(go);
    }

    public static void ExportRoot(GameObject go, string name)
    {
        var sb = new StringBuilder("{\"sprites\":[");
        AppendRenderers(sb, go.GetComponentsInChildren<SpriteRenderer>(false));
        sb.Append("],\"players\":[");
        bool first = true;
        foreach (DraggableMatch d in go.GetComponentsInChildren<DraggableMatch>(false))
        {
            if (!first) sb.Append(','); first = false;
            sb.Append("{\"x\":").Append(F(d.transform.position.x)).Append(",\"y\":").Append(F(d.transform.position.y)).Append('}');
        }
        sb.Append("]}");
        File.WriteAllText(Path.Combine(OutDir, name + ".json"), sb.ToString());
    }

    private static void AppendRenderers(StringBuilder sb, IEnumerable<SpriteRenderer> renderers)
    {
        bool first = true;
        foreach (SpriteRenderer r in renderers.Where(r => r.sprite != null && r.enabled))
        {
            // Исходный PNG спрайта (в редакторе sprite.texture может указывать на упакованный атлас)
            string path = AssetDatabase.GetAssetPath(r.sprite);
            if (string.IsNullOrEmpty(path)) continue;
            var source = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (source == null) continue;
            Rect rect = r.sprite.rect;
            Vector2 pivot = r.sprite.pivot; // в пикселях от левого нижнего угла прямоугольника
            Matrix4x4 m = r.transform.localToWorldMatrix;
            if (!first) sb.Append(','); first = false;
            sb.Append("{\"file\":\"").Append(path.Replace("\\", "/")).Append('"')
              .Append(",\"tex\":[").Append(source.width).Append(',').Append(source.height).Append(']')
              .Append(",\"rect\":[").Append(F(rect.x)).Append(',').Append(F(rect.y)).Append(',').Append(F(rect.width)).Append(',').Append(F(rect.height)).Append(']')
              .Append(",\"pivot\":[").Append(F(pivot.x)).Append(',').Append(F(pivot.y)).Append(']')
              .Append(",\"ppu\":").Append(F(r.sprite.pixelsPerUnit))
              .Append(",\"m\":[").Append(F(m.m00)).Append(',').Append(F(m.m01)).Append(',').Append(F(m.m03)).Append(',')
              .Append(F(m.m10)).Append(',').Append(F(m.m11)).Append(',').Append(F(m.m13)).Append(']')
              .Append(",\"flip\":[").Append(r.flipX ? 1 : 0).Append(',').Append(r.flipY ? 1 : 0).Append(']')
              .Append(",\"color\":").Append(Col(r.color))
              .Append(",\"order\":").Append(SortingLayer.GetLayerValueFromID(r.sortingLayerID) * 100000 + r.sortingOrder)
              .Append(",\"z\":").Append(F(r.transform.position.z))
              .Append(",\"golden\":").Append(r.GetComponent<MatchLogic>() != null && r.GetComponent<MatchLogic>().isGolden ? 1 : 0)
              .Append(",\"additive\":").Append(r.sharedMaterial != null && r.sharedMaterial.name.Contains("Additive") ? 1 : 0)
              .Append(",\"shadow\":").Append(r.GetComponent<DropShadow2D>() != null ? ShadowJson(r.GetComponent<DropShadow2D>()) : "null")
              .Append('}');
        }
    }

    private static string ShadowJson(DropShadow2D s) =>
        "{\"dx\":" + F(s.shadowOffset.x) + ",\"dy\":" + F(s.shadowOffset.y) + ",\"color\":" + Col(s.shadowColor) + "}";

    private static string Col(Color c) => "[" + F(c.r) + "," + F(c.g) + "," + F(c.b) + "," + F(c.a) + "]";
    private static string F(float v) => v.ToString("0.#####", Inv);
}
