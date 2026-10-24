using System.Collections;
using UnityEngine;

public class DraggableMatch : MonoBehaviour
{
    public LayerMask slotLayer;
    public bool freePlacementMode = false;
    public float tapRotateAngle = -90f;
    
    [Header("Подсказки")]
    [Tooltip("Текст, который исчезнет, когда игрок повернет эту спичку")]
    public GameObject hideOnRotateText;

    private Vector3 offset;
    private Vector3 startPos;
    private bool isPlaced = false;
    private Vector3 tapStartMousePos;
    private Vector3 tapStartObjectPos;
    private float maxDragPx; // самое дальнее смещение пальца за текущий жест

    // «Сок» перетаскивания (VisualTheme, раздел «Игра: перетаскивание»)
    private Quaternion baseRotation;
    private float tilt;
    private Vector3 lastDragPos;
    private bool returning;
    private bool pressStarted; // нажатие началось на поле, а не на UI (M-06)

    public bool IsPlaced => isPlaced;

    // Порог «тап или перетаскивание» в физическом размере (~2.5 мм), а не в пикселях:
    // 10 px на экране 400+ dpi — это полмиллиметра, любое дрожание пальца становилось drag.
    private static float TapThresholdPx => Mathf.Max(10f, (Screen.dpi > 0 ? Screen.dpi : 160f) * 0.1f);

    void Start()
    {
        startPos = transform.position;
    }

    void OnMouseDown()
    {
        if (Time.timeScale == 0f) return; // Игнорируем нажатия на паузе
        pressStarted = false;
        if (isPlaced || returning) return;
        if (FieldInput.PointerOverUI()) return; // касание по кнопке не должно двигать спичку (M-06)
        pressStarted = true;
        tapStartMousePos = Input.mousePosition;
        tapStartObjectPos = transform.position;
        maxDragPx = 0f;
        baseRotation = transform.rotation;
        tilt = 0f;
        lastDragPos = transform.position;
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        offset = transform.position - mousePos;
        offset.z = 0;
    }

    void OnMouseDrag()
    {
        if (Time.timeScale == 0f) return; // Игнорируем нажатия на паузе
        if (isPlaced) return;

        if (returning || !pressStarted) return;

        bool wasTap = maxDragPx < TapThresholdPx;
        maxDragPx = Mathf.Max(maxDragPx, Vector3.Distance(Input.mousePosition, tapStartMousePos));
        if (maxDragPx < TapThresholdPx) return; // пока это может быть тап — спичка не дёргается за пальцем
        if (wasTap) TutorialController.Notify(TutorialController.Kind.Drag);

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        Vector3 target = mousePos + offset;
        VisualTheme theme = VisualTheme.Current;

        // Магнит к слоту и его подсветка (только в уровнях со слотами)
        if (!freePlacementMode && (theme.slotMagnet || theme.slotHighlight))
        {
            Vector3 center = CenterIfAt(target);
            Collider2D slot = Physics2D.OverlapCircle(center, theme.slotMagnetRadius, slotLayer);
            if (slot != null)
            {
                Vector3 slotPos = slot.transform.position;
                float pull = 1f - Mathf.Clamp01(Vector2.Distance(center, slotPos) / theme.slotMagnetRadius);
                if (theme.slotMagnet) target += (slotPos - center) * (theme.slotMagnetStrength * pull);
                GameJuice.ShowSlotHighlight(slotPos, pull);
            }
            else
            {
                GameJuice.HideSlotHighlight();
            }
        }

        // Лёгкий наклон по направлению движения
        if (theme.dragTilt)
        {
            float vx = (target.x - lastDragPos.x) / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            float goal = Mathf.Clamp(-vx * 3f, -theme.dragTiltMaxAngle, theme.dragTiltMaxAngle);
            tilt = Mathf.Lerp(tilt, goal, 1f - Mathf.Exp(-theme.dragTiltResponse * Time.unscaledDeltaTime));
        }
        // Не даём утащить спичку за край экрана (M-07)
        target += FieldInput.PushInside(CenterIfAt(target), Camera.main);

        lastDragPos = target;
        SetPose(target, tilt);
    }

    // Центр коллайдера, если поставить корень спички в rootPosition без наклона
    private Vector3 CenterIfAt(Vector3 rootPosition)
    {
        Collider2D col = GetComponent<Collider2D>();
        Vector3 local = col != null ? (Vector3)col.offset : Vector3.zero;
        return rootPosition + baseRotation * Vector3.Scale(local, transform.lossyScale);
    }

    // Корень в rootPosition, наклон вокруг центра коллайдера (не вокруг смещённой точки опоры префаба)
    private void SetPose(Vector3 rootPosition, float tiltDegrees)
    {
        transform.rotation = baseRotation;
        transform.position = rootPosition;
        if (Mathf.Abs(tiltDegrees) > 0.01f)
            transform.RotateAround(CenterIfAt(rootPosition), Vector3.forward, tiltDegrees);
    }

    // Убрать наклон, сохранив центр спички на месте: дальше работает прежняя логика отпускания
    private void RemoveTilt()
    {
        if (Mathf.Abs(tilt) <= 0.01f && transform.rotation == baseRotation) return;
        Collider2D col = GetComponent<Collider2D>();
        Vector3 centerBefore = col != null ? transform.TransformPoint(col.offset) : transform.position;
        transform.rotation = baseRotation;
        Vector3 centerAfter = col != null ? transform.TransformPoint(col.offset) : transform.position;
        transform.position += centerBefore - centerAfter;
        tilt = 0f;
    }

    // Мягкий возврат на место вместо мгновенного телепорта
    private void ReturnToStart()
    {
        float duration = VisualTheme.Current.returnDuration;
        if (duration <= 0f || !isActiveAndEnabled) { transform.position = startPos; return; }
        StartCoroutine(ReturnRoutine(duration));
    }

    private IEnumerator ReturnRoutine(float duration)
    {
        returning = true;
        Vector3 from = transform.position;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            transform.position = Vector3.LerpUnclamped(from, startPos, Ease.Evaluate(Ease.Type.OutBack, t / duration, 1.2f));
            yield return null;
        }
        transform.position = startPos;
        returning = false;
    }

    void OnMouseUp()
    {
        if (Time.timeScale == 0f)
        {
            // Пауза посреди перетаскивания: отменяем жест, иначе спичка остаётся висеть наклонённой с подсветкой слота
            if (pressStarted && !isPlaced && !returning)
            {
                pressStarted = false;
                GameJuice.HideSlotHighlight();
                RemoveTilt();
                transform.position = tapStartObjectPos;
            }
            return;
        }
        if (isPlaced || returning || !pressStarted) return;
        pressStarted = false;

        GameJuice.HideSlotHighlight();
        RemoveTilt();

        // Если это тап для поворота: палец за весь жест не ушёл дальше порога
        maxDragPx = Mathf.Max(maxDragPx, Vector3.Distance(Input.mousePosition, tapStartMousePos));
        if (maxDragPx < TapThresholdPx)
        {
            transform.position = tapStartObjectPos;

            Collider2D myCol = GetComponent<Collider2D>();
            if (myCol != null)
            {
                Vector3 centerPoint = transform.TransformPoint(myCol.offset);
                transform.RotateAround(centerPoint, Vector3.forward, tapRotateAngle);
            }
            else
            {
                transform.Rotate(0, 0, tapRotateAngle);
            }
            
            // Прячем текст подсказки, если он есть
            if (hideOnRotateText != null)
            {
                Destroy(hideOnRotateText);
            }
            
            startPos = transform.position;
            TutorialController.Notify(TutorialController.Kind.Rotate);
            return;
        }

        // Отпустили над кнопкой/панелью: не ставим и не поджигаем, возвращаем на место (M-07)
        if (FieldInput.ScreenPointOverUI(Input.mousePosition))
        {
            ReturnToStart();
            return;
        }

        Collider2D myCollider = GetComponent<Collider2D>();
        BoxCollider2D myBox = GetComponent<BoxCollider2D>();
        
        // --- МАТЕМАТИЧЕСКАЯ ПРОВЕРКА НА ПЕРЕСЕЧЕНИЕ ---
        // Ищем ВСЕ спички на уровне (даже те, которые уже были расставлены изначально)
        MatchLogic myLogic = GetComponent<MatchLogic>();
        MatchLogic[] allMatches = FindObjectsByType<MatchLogic>(FindObjectsSortMode.None);
        
        foreach (var match in allMatches)
        {
            // Проверяем, что это не наша же спичка и она активна
            if (match != null && match != myLogic && match.gameObject.activeInHierarchy)
            {
                if (CheckOverlap(myLogic, match))
                {
                    // Накладывается на другую спичку - возвращаем на место!
                    ReturnToStart();
                    Haptics.Error();
                    return;
                }
            }
        }
        // ----------------------------------------------------

        Vector3 currentCenter = transform.position;
        if (myCollider != null)
        {
            currentCenter = transform.TransformPoint(myCollider.offset);
        }

        if (freePlacementMode)
        {
            isPlaced = true;
            if (myLogic != null)
            {
                myLogic.Ignite();
            }
            return;
        }

        Collider2D slotCollider = Physics2D.OverlapCircle(currentCenter, 0.5f, slotLayer);

        if (slotCollider != null)
        {
            transform.rotation = slotCollider.transform.rotation;

            if (myCollider != null)
            {
                Vector3 newCenter = transform.TransformPoint(myCollider.offset);
                Vector3 pivotOffset = transform.position - newCenter;
                transform.position = slotCollider.transform.position + pivotOffset;
            }
            else
            {
                transform.position = slotCollider.transform.position;
            }

            isPlaced = true;
            Destroy(slotCollider.gameObject);

            if (myLogic != null)
            {
                myLogic.Ignite();
            }
        }
        else
        {
            ReturnToStart();
        }
    }

    private bool CheckOverlap(MatchLogic logic1, MatchLogic logic2)
    {
        if (logic1 == null || logic1.headPoint == null || logic1.tailPoint == null ||
            logic2 == null || logic2.headPoint == null || logic2.tailPoint == null)
            return false;

        float minAllowedDistance = 0.25f; // Насколько близко можно класть параллельные спички
        
        Vector2 a1 = logic1.headPoint.position;
        Vector2 a2 = logic1.tailPoint.position;
        Vector2 b1 = logic2.headPoint.position;
        Vector2 b2 = logic2.tailPoint.position;

        // Быстрая проверка на пересечение самих отрезков (крест-накрест). 
        // Если они касаются только кончиками - это разрешено!
        if (SegmentsIntersect(a1, a2, b1, b2)) return true;

        // Проверяем ТОЛЬКО середину первой спички (без кончиков), чтобы кончиками можно было касаться!
        Vector2[] pointsA = { Vector2.Lerp(a1, a2, 0.25f), Vector2.Lerp(a1, a2, 0.5f), Vector2.Lerp(a1, a2, 0.75f) };
        foreach (Vector2 p in pointsA)
        {
            if (DistancePointToSegment(p, b1, b2) < minAllowedDistance) return true;
        }

        // Проверяем ТОЛЬКО середину второй спички
        Vector2[] pointsB = { Vector2.Lerp(b1, b2, 0.25f), Vector2.Lerp(b1, b2, 0.5f), Vector2.Lerp(b1, b2, 0.75f) };
        foreach (Vector2 p in pointsB)
        {
            if (DistancePointToSegment(p, a1, a2) < minAllowedDistance) return true;
        }

        return false;
    }

    private bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float denominator = ((b.x - a.x) * (d.y - c.y)) - ((b.y - a.y) * (d.x - c.x));
        if (denominator == 0) return false;

        float numerator1 = ((a.y - c.y) * (d.x - c.x)) - ((a.x - c.x) * (d.y - c.y));
        float numerator2 = ((a.y - c.y) * (b.x - a.x)) - ((a.x - c.x) * (b.y - a.y));

        float r = numerator1 / denominator;
        float s = numerator2 / denominator;

        // Используем 0.05 и 0.95, чтобы разрешить пересечение на самых кончиках (соединение спичек)
        return (r > 0.05f && r < 0.95f) && (s > 0.05f && s < 0.95f);
    }

    private float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSq = ab.sqrMagnitude;
        if (lengthSq == 0) return Vector2.Distance(point, a);

        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq);
        Vector2 projection = a + t * ab;
        return Vector2.Distance(point, projection);
    }
}
