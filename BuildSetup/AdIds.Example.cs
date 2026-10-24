// ПРИМЕР. Лежит вне Assets, чтобы не компилироваться вместе с настоящим AdIds.cs.
// Сборка с чистого клона: скопируйте этот файл в Assets/Scripts/AdIds.cs (он в .gitignore) — проект соберётся
// с официальными тестовыми ID Google. Для релиза впишите боевые ID из AdMob и хеши своих телефонов в TestDevices.
// Development Build всегда берёт тестовые ID (GoogleAdsManager), релиз — эти константы.
// Перед релизной сборкой Android AdIdsBuildCheck предупредит, если здесь остались тестовые ID или пуст TestDevices.
public static class AdIds
{
    public const string Banner = "ca-app-pub-3940256099942544/6300978111";
    public const string Interstitial = "ca-app-pub-3940256099942544/1033173712";
    public const string Rewarded = "ca-app-pub-3940256099942544/5224354917";

    // Хешированные ID своих телефонов: на них даже релизная сборка покажет тестовую рекламу.
    // ID печатается в logcat при первом запросе рекламы:
    //   adb logcat | findstr setTestDeviceIds
    //   ... setTestDeviceIds(Arrays.asList("33BE2250B43518CCDA7DE426D04EE231"))
    public static readonly string[] TestDevices = { };
}
