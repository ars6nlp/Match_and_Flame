using UnityEditor;
using UnityEngine;

public class AddMoneyHack
{
    [MenuItem("Tools/Give 100 Golden Matches 💰")]
    public static void AddMoney()
    {
        int current = PlayerPrefs.GetInt("GoldenMatchesCurrency", 0);
        PlayerPrefs.SetInt("GoldenMatchesCurrency", current + 100);
        PlayerPrefs.Save();
        Debug.Log("Вам начислено 100 Золотых Спичек! Ваш баланс: " + (current + 100));
    }

    [MenuItem("Tools/Reset Shop (Delete Saves) 🗑️")]
    public static void ResetShop()
    {
        PlayerPrefs.DeleteKey("GoldenMatchesCurrency");
        PlayerPrefs.DeleteKey("SkinUnlocked_1");
        PlayerPrefs.DeleteKey("SkinUnlocked_2");
        PlayerPrefs.DeleteKey("SkinUnlocked_3");
        PlayerPrefs.DeleteKey("EquippedFireSkin");
        PlayerPrefs.Save();
        Debug.Log("Все покупки и баланс удалены!");
    }
}
