using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Таблица связей на сетке: спичка A в (0,0) с поворотом ra, спичка B — центр видимой палочки со сдвигом (dx, dy)
/// кратным шагу (LINK_STEP, по умолчанию 0.125), поворот rb. Для каждого сдвига с видимым зазором меньше 0.7: поджигает ли A спичку B и B спичку A,
/// и устойчива ли связь/её отсутствие к сдвигу B на ±0.03 (без ветра). Builds/link-table.txt — справочник для расстановки.
/// </summary>
public static class LinkTable
{
    [MenuItem("Tools/Match & Flame/Level Lab/Link table on grid")]
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Probe");
        MatchLogic p = LevelLab.NewMatch(root.transform, new Vector2(50f, 50f), 0f, player: true, name: "P");
        p.GetComponent<DraggableMatch>().freePlacementMode = true;
        MatchLogic a = LevelLab.NewMatch(root.transform, Vector2.zero, 0f, false, "A");
        MatchLogic b = LevelLab.NewMatch(root.transform, new Vector2(5f, 5f), 0f, false, "B");
        float step = float.Parse(System.Environment.GetEnvironmentVariable("LINK_STEP") ?? "0.125", System.Globalization.CultureInfo.InvariantCulture);
        int n = Mathf.RoundToInt(2.5f / step);
        const float J = 0.03f;
        var sb = new StringBuilder($"Связи на сетке {step} (центры видимых палочек). Формат: dx,dy зазор A→B B→A; '!' — связь/её отсутствие меняется при сдвиге ±{J}\n");
        using (var sim = new LevelSimulator(root, owned: false))
        {
            void Place(MatchLogic m, Vector2 vc, float rot)
            {
                LevelDesigns.Match spec = LevelDesigns.S(vc.x, vc.y, rot);
                var col = (BoxCollider2D)m.GetComponent<Collider2D>();
                Vector2 rootPos = spec.center - (Vector2)(Quaternion.Euler(0, 0, rot) * Vector3.Scale(col.offset, m.transform.lossyScale));
                sim.SetStartPose(m, rootPos, rot);
            }
            bool Lights(MatchLogic from, MatchLogic to) { sim.RunFrom(from, 0f, 6f); return sim.Burned(to); }
            foreach (float ra in new[] { 0f, 90f })
                foreach (float rb in new[] { 0f, 90f, 180f, 270f })
                {
                    sb.AppendLine($"\nA rot {ra}, B rot {rb}:");
                    for (int iy = n; iy >= -n; iy--)
                        for (int ix = -n; ix <= n; ix++)
                        {
                            var d = new Vector2(ix * step, iy * step);
                            Place(a, Vector2.zero, ra);
                            Place(b, d, rb);
                            sim.RunFrom(a, 0f, 0.01f); // расставить
                            Physics2D.SyncTransforms();
                            float gap = LayoutCheck.Gap(LayoutCheck.StickOf(a), LayoutCheck.StickOf(b));
                            if (gap >= 0.7f || gap < 0f) continue;
                            bool ab = Lights(a, b), ba = Lights(b, a);
                            bool stable = true;
                            foreach (Vector2 j in new[] { new Vector2(J, 0), new Vector2(-J, 0), new Vector2(0, J), new Vector2(0, -J) })
                            {
                                Place(b, d + j, rb);
                                if (Lights(a, b) != ab || Lights(b, a) != ba) { stable = false; break; }
                            }
                            sb.AppendLine($"  {d.x,6:0.000},{d.y,6:0.000} gap {gap:0.00} {(ab ? "A>B" : "   ")} {(ba ? "B>A" : "   ")} {(stable ? "" : "!")}");
                        }
                }
        }
        Object.DestroyImmediate(root);
        Directory.CreateDirectory("Builds");
        File.WriteAllText("Builds/link-table.txt", sb.ToString());
        LevelLab.Log("LinkTable written");
    }
}
