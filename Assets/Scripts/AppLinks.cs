/// <summary>Внешние ссылки приложения — в одном месте.</summary>
public static class AppLinks
{
    /// <summary>
    /// Политика конфиденциальности. Google Play требует ссылку и в Play Console, и внутри приложения.
    /// ЗАГЛУШКА: впишите адрес своей политики (тот же, что в Play Console). С заглушкой ReleaseBuild не соберёт релиз.
    /// </summary>
    public const string PrivacyPolicyUrl = "https://example.com/privacy";

    public static bool PrivacyPolicyIsPlaceholder => PrivacyPolicyUrl.Contains("example.com");
}
