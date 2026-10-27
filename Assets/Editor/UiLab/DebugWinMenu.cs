using UnityEditor;
using UnityEngine;

/// <summary>Отладка в Play Mode (GameScene): итог уровня с заданным числом звёзд — проверить звук, звёзды и вибрацию. Прогресс не сохраняется.</summary>
public static class DebugWinMenu
{
    private static void Win(int stars)
    {
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (gm != null) gm.DebugWin(stars);
        else Debug.LogWarning("DebugWin: откройте GameScene в Play Mode");
    }

    [MenuItem("Tools/Match & Flame/Debug/Итог 0★ («Почти получилось»)")] private static void W0() => Win(0);
    [MenuItem("Tools/Match & Flame/Debug/Победа 1★")] private static void W1() => Win(1);
    [MenuItem("Tools/Match & Flame/Debug/Победа 2★")] private static void W2() => Win(2);
    [MenuItem("Tools/Match & Flame/Debug/Победа 3★")] private static void W3() => Win(3);
    [MenuItem("Tools/Match & Flame/Debug/Итог 0★ («Почти получилось»)", true)]
    [MenuItem("Tools/Match & Flame/Debug/Победа 1★", true)]
    [MenuItem("Tools/Match & Flame/Debug/Победа 2★", true)]
    [MenuItem("Tools/Match & Flame/Debug/Победа 3★", true)]
    private static bool Valid() => EditorApplication.isPlaying;
}
