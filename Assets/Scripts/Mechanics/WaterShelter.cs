using UnityEngine;

/// <summary>
/// Укрытие от капель («зонтик» над спичкой или полочка): капля, попавшая в коллайдер укрытия, разбивается
/// и дальше не летит. Огонь укрытие не останавливает (слой Default, спички его не видят).
/// Часть механики «Капля воды»; логика попадания — в WaterDripper.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class WaterShelter : MonoBehaviour
{
    private void Reset()
    {
        gameObject.layer = 0; // Default: капли видят, огонь — нет
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnDrawGizmos()
    {
        if (!MechanicsSettings.Current.debugGizmos) return;
        var col = GetComponent<Collider2D>();
        if (col == null) return;
        Gizmos.color = new Color(0.45f, 0.75f, 1f, 0.6f);
        Bounds b = col.bounds;
        Gizmos.DrawWireCube(b.center, b.size);
    }
}
