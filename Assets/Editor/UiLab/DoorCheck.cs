using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Проверка дверей-переходов: прямоугольники створок в конце анимации на всех разрешениях UiAudit (Canvas Expand,
/// 1080×1920): покрытие экрана и перекрытие в центре; открытое состояние — створки целиком за краем.
/// Report → Builds/door-check.md; Shots (нужен GPU) → Builds/ui-shots/doors/*.png (панель победы на закрытых дверях
/// и закрытые двери перехода поверх всего).
/// </summary>
public static class DoorCheck
{
    [MenuItem("Tools/Match & Flame/UI Lab/Door check")]
    public static void Report()
    {
        float overlap = VisualTheme.Current.doorOverlap;
        var sb = new StringBuilder("# Двери: покрытие экрана в закрытом состоянии\n\n");
        sb.AppendLine($"Створка — {DoorLayout.LeafWidth(overlap):P0} ширины Canvas, перекрытие задано {overlap:P0}, по высоте створки выступают на 2% за край.\n");
        sb.AppendLine("| Экран | Canvas (ед.) | Левая створка x | Правая створка x | По высоте | Покрытие | Перекрытие в центре | Открыто: за краем |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");
        bool allOk = true;
        foreach (UiAudit.Res r in UiAudit.Resolutions)
        {
            float s = Mathf.Min(r.w / 1080f, r.h / 1920f); // Expand
            var size = new Vector2(r.w / s, r.h / s);
            var root = new GameObject("DoorCheckRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            root.sizeDelta = size;
            var left = new GameObject("L", typeof(RectTransform)).GetComponent<RectTransform>();
            var right = new GameObject("R", typeof(RectTransform)).GetComponent<RectTransform>();
            left.SetParent(root, false); right.SetParent(root, false);
            right.localScale = new Vector3(-1f, 1f, 1f);

            DoorLayout.Apply(left, right, 1f, overlap);
            Rect l = WorldRect(left), rr = WorldRect(right);
            float W = size.x, H = size.y;
            float covLeft = Mathf.Max(-W / 2, l.xMin), covRight = Mathf.Min(W / 2, rr.xMax);
            bool joined = l.xMax >= rr.xMin;
            float coveredX = joined ? (covRight - covLeft) / W : ((l.xMax - covLeft) + (covRight - rr.xMin)) / W;
            float coveredY = (Mathf.Min(H / 2, Mathf.Min(l.yMax, rr.yMax)) - Mathf.Max(-H / 2, Mathf.Max(l.yMin, rr.yMin))) / H;
            float coverage = Mathf.Clamp01(coveredX) * Mathf.Clamp01(coveredY);
            float centre = (l.xMax - rr.xMin) / W;

            DoorLayout.Apply(left, right, 0f, overlap);
            Rect lo = WorldRect(left), ro = WorldRect(right);
            bool offscreen = lo.xMax <= -W / 2 && ro.xMin >= W / 2;
            Object.DestroyImmediate(root.gameObject);

            bool ok = coverage >= 0.9999f && centre >= 0.03f - 1e-4f && offscreen;
            allOk &= ok;
            sb.AppendLine($"| {r.name} | {W:0}×{H:0} | {l.xMin + W / 2:0}…{l.xMax + W / 2:0} | {rr.xMin + W / 2:0}…{rr.xMax + W / 2:0} | " +
                          $"{l.yMin + H / 2:0}…{l.yMax + H / 2:0} | {coverage:P1} | {centre:P1} ({centre * W:0} ед.) | {(offscreen ? "да" : "НЕТ")} |");
        }
        sb.AppendLine($"\n**{(allOk ? "Все разрешения: покрытие 100%, перекрытие ≥ 3%, в открытом состоянии створки за краем." : "ЕСТЬ НАРУШЕНИЯ")}**");
        sb.AppendLine("\nРаскладка — якорями в долях Canvas (DoorLayout), поэтому не зависит от пикселей и масштаба; Canvas дверей перехода — " +
                      "Screen Space Overlay без SafeArea (вырез и жестовая панель тоже закрыты), sorting order 32760.");
        Directory.CreateDirectory("Builds");
        File.WriteAllText("Builds/door-check.md", sb.ToString());
        Debug.Log("[Polish] DoorCheck\n" + sb);
    }

    private static Rect WorldRect(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return Rect.MinMaxRect(c.Min(v => v.x), c.Min(v => v.y), c.Max(v => v.x), c.Max(v => v.y));
    }

    [MenuItem("Tools/Match & Flame/UI Lab/Door screenshots (needs GPU)")]
    public static void Shots()
    {
        string only = System.Environment.GetEnvironmentVariable("UI_RES") ?? "1080x1920,1080x2400,1200x1600";
        string tag = System.Environment.GetEnvironmentVariable("UI_TAG") ?? "after";
        var win = UiAudit.States().First(st => st.name == "win");
        int n = 0;
        foreach (UiAudit.Res r in UiAudit.Resolutions.Where(x => only.Split(',').Contains(x.name)))
            foreach (string mode in new[] { "win", "transition" })
            {
                var s = EditorSceneManager.OpenScene(UiAudit.Game, OpenSceneMode.Single);
                win.setup(s);
                UiAudit.EmulateRuntime(s, "ru");
                GameObject doors = UiAudit.Find(s, "DoorsContainer");
                if (doors != null)
                {
                    doors.SetActive(true);
                    var da = doors.GetComponent<DoorAnimator>();
                    if (tag == "after") DoorLayout.Apply(da.leftDoor, da.rightDoor, 1f, VisualTheme.Current.doorOverlap); // как в конце анимации
                }
                if (mode == "transition" && tag == "after")
                {
                    var go = new GameObject("SceneTransitionPreview", typeof(RectTransform));
                    SceneTransition.Build(go, out RectTransform l, out RectTransform rr, out Image _);
                    go.GetComponent<Canvas>().sortingOrder = 30000;
                    DoorLayout.Apply(l, rr, 1f, VisualTheme.Current.doorOverlap);
                }
                if (mode == "transition" && tag != "after") continue;
                UiAudit.Layout lay = UiAudit.Apply(s, r);
                if (System.Environment.GetEnvironmentVariable("DIAG") == "1")
                    foreach (Canvas c in lay.roots) Debug.Log($"[Polish] root {c.name} order {c.sortingOrder} override {c.overrideSorting} layer {c.gameObject.layer} scene {c.gameObject.scene.name} " +
                        string.Join(" ", c.GetComponentsInChildren<Image>(true).Take(4).Select(i => $"{i.name}:{i.rectTransform.rect.size}:{i.gameObject.activeInHierarchy}")));
                if (UiAudit.Render(s, lay, $"Builds/ui-shots/doors/{tag}_{mode}_{r.name}.png")) n++;
            }
        Debug.Log($"[Polish] DoorCheck.Shots {tag}: {n} PNG");
    }
}
