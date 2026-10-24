using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Ввод на игровом поле: касание не должно «проваливаться» сквозь UI (M-06),
/// спичку нельзя утащить за край экрана (M-07).
///
/// Компромисс M-06: ввод спичек остаётся на OnMouse* (эмуляция мыши из касаний), а перед началом
/// перетаскивания проверяется, не над UI ли палец (по fingerId касания). Полный переход на
/// IPointer*-обработчики с Physics2DRaycaster надёжнее, но меняет весь ввод и проверяется только на устройстве.
/// </summary>
public static class FieldInput
{
    private static readonly List<RaycastResult> hits = new List<RaycastResult>(8);
    private static PointerEventData pointerData;
    private static EventSystem pointerOwner;

    /// <summary>Палец (или мышь) сейчас над интерактивным UI.</summary>
    public static bool PointerOverUI()
    {
        EventSystem es = EventSystem.current;
        if (es == null) return false;
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
                if (es.IsPointerOverGameObject(Input.GetTouch(i).fingerId)) return true;
            return false;
        }
        return es.IsPointerOverGameObject();
    }

    /// <summary>Есть ли UI под точкой экрана (надёжно и в момент отпускания пальца).</summary>
    public static bool ScreenPointOverUI(Vector2 screenPosition)
    {
        EventSystem es = EventSystem.current;
        if (es == null) return false;
        if (pointerData == null || pointerOwner != es) { pointerData = new PointerEventData(es); pointerOwner = es; }
        pointerData.position = screenPosition;
        hits.Clear();
        es.RaycastAll(pointerData, hits);
        return hits.Count > 0;
    }

    /// <summary>Видимая область камеры минус отступ VisualTheme.fieldMargin.</summary>
    public static Rect FieldRect(Camera cam)
    {
        float margin = VisualTheme.Current.fieldMargin;
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector3 c = cam.transform.position;
        return Rect.MinMaxRect(c.x - w + margin, c.y - h + margin, c.x + w - margin, c.y + h - margin);
    }

    /// <summary>Сдвиг, который вернёт точку внутрь поля (ноль, если она уже внутри).</summary>
    public static Vector3 PushInside(Vector3 point, Camera cam)
    {
        Rect r = FieldRect(cam);
        return new Vector3(Mathf.Clamp(point.x, r.xMin, r.xMax) - point.x, Mathf.Clamp(point.y, r.yMin, r.yMax) - point.y, 0f);
    }
}
