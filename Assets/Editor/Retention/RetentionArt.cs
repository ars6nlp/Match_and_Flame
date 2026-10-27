using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;

/// <summary>
/// Импорт процедурной графики «Очага» (рисует Tools/retention_art/draw_atlas.py): нарезка листов по cells.json на спрайты,
/// Android — до 512 px со сжатием, все листы — в атлас Assets/Art/Retention/Retention.spriteatlasv2.
/// </summary>
public static class RetentionArt
{
    public const string Folder = "Assets/Art/Retention";
    public const string AtlasPath = Folder + "/Retention.spriteatlasv2";

    [System.Serializable] private class CellJ { public string name; public int x, y, w, h, border; }

    [MenuItem("Tools/Match & Flame/Retention/Import hearth art")]
    public static void Import()
    {
        string json = File.ReadAllText(Folder + "/cells.json");
        var sheets = ParseSheets(json);
        var textures = new List<Object>();
        foreach (string file in Directory.GetFiles(Folder, "*.png"))
        {
            string path = file.Replace('\\', '/');
            string key = Path.GetFileNameWithoutExtension(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp == null) { AssetDatabase.ImportAsset(path); imp = (TextureImporter)AssetImporter.GetAtPath(path); }
            imp.textureType = TextureImporterType.Sprite;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.spritePixelsPerUnit = 100;
            imp.wrapMode = TextureWrapMode.Clamp;
            int height = 512;
            var tex0 = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex0 != null) height = tex0.height;
            if (sheets.TryGetValue(key, out List<CellJ> cells))
            {
                imp.spriteImportMode = SpriteImportMode.Multiple;
                var metas = cells.Select(c => new SpriteMetaData
                {
                    name = c.name,
                    rect = new Rect(c.x, height - c.y - c.h, c.w, c.h),   // в Unity y снизу
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = new Vector4(c.border, c.border, c.border, c.border),   // 9-slice (пузырь реплики)
                }).ToArray();
#pragma warning disable 618
                imp.spritesheet = metas;
#pragma warning restore 618
            }
            else
            {
                imp.spriteImportMode = SpriteImportMode.Single;
            }
            var android = imp.GetPlatformTextureSettings("Android");
            android.overridden = true; android.maxTextureSize = 512; android.format = TextureImporterFormat.Automatic;
            android.textureCompression = TextureImporterCompression.Compressed;
            imp.SetPlatformTextureSettings(android);
            var webgl = imp.GetPlatformTextureSettings("WebGL");
            webgl.overridden = true; webgl.maxTextureSize = 512; webgl.format = TextureImporterFormat.Automatic;
            webgl.textureCompression = TextureImporterCompression.Compressed;
            imp.SetPlatformTextureSettings(webgl);
            imp.SaveAndReimport();
            textures.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        }

        // Атлас: все листы «Очага» в одной упаковке
        if (!File.Exists(AtlasPath))
        {
            var asset = new SpriteAtlasAsset();
            asset.Add(textures.ToArray());
            SpriteAtlasAsset.Save(asset, AtlasPath);
        }
        else
        {
            var asset = SpriteAtlasAsset.Load(AtlasPath);
            asset.Remove(textures.ToArray());
            asset.Add(textures.ToArray());
            SpriteAtlasAsset.Save(asset, AtlasPath);
        }
        // Без плотной упаковки: UI-картинки рисуются прямоугольником спрайта, соседи не должны в него попадать
        var atlasImp = AssetImporter.GetAtPath(AtlasPath) as SpriteAtlasImporter;
        if (atlasImp != null)
        {
            atlasImp.packingSettings = new SpriteAtlasPackingSettings { enableTightPacking = false, enableRotation = false, padding = 4, blockOffset = 1 };
            atlasImp.SaveAndReimport();
        }
        // Материал аддитивного свечения (свет камина, ореол огонька): шейдер UI/Hearth Additive, грузится из Resources
        const string matPath = "Assets/Resources/HearthAdditive.mat";
        Shader add = Shader.Find("UI/Hearth Additive");
        if (add != null && AssetDatabase.LoadAssetAtPath<Material>(matPath) == null)
            AssetDatabase.CreateAsset(new Material(add) { name = "HearthAdditive" }, matPath);
        AssetDatabase.Refresh();
        int sprites = textures.Sum(t => AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(t)).OfType<Sprite>().Count());
        Debug.Log($"[Polish] Retention art: {textures.Count} sheets, {sprites} sprites, atlas {File.Exists(AtlasPath)}, additive material {AssetDatabase.LoadAssetAtPath<Material>(matPath) != null}");
        RetentionTools.Refresh();
    }

    private static Dictionary<string, List<CellJ>> ParseSheets(string json)
    {
        // cells.json: { "sheet": [ {name,x,y,w,h}, ... ], ... } — JsonUtility не читает словари, разбираем вручную
        var result = new Dictionary<string, List<CellJ>>();
        int i = 0;
        while ((i = json.IndexOf('"', i)) >= 0)
        {
            int j = json.IndexOf('"', i + 1);
            string key = json.Substring(i + 1, j - i - 1);
            int colon = json.IndexOf(':', j);
            int open = json.IndexOf('[', colon);
            if (open < 0) break;
            int depth = 0, k = open;
            for (; k < json.Length; k++) { if (json[k] == '[') depth++; else if (json[k] == ']' && --depth == 0) break; }
            string arr = json.Substring(open, k - open + 1);
            var list = new List<CellJ>();
            int p = 0;
            while ((p = arr.IndexOf('{', p)) >= 0)
            {
                int q = arr.IndexOf('}', p);
                list.Add(JsonUtility.FromJson<CellJ>(arr.Substring(p, q - p + 1)));
                p = q + 1;
            }
            result[key] = list;
            i = k + 1;
        }
        return result;
    }
}
