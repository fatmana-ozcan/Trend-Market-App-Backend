namespace TrendMarketServer.Data;

public class VerificationEntry
{
    public string Code { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public object? Payload { get; set; }
}

// Basit, bellek içi doğrulama kodu deposu. Demo modunda
// gerçek bir SMS servisi bağlı değil; kod burada saklanır ve isteyen uca "demoCode"
// olarak döner. Gerçek SMS entegrasyonunda bu depo aynı kalır, sadece kodun SMS ile
// gönderildiği yer eklenir ve "demoCode" alanı response'tan çıkarılır.
public static class VerificationStore
{
    public static readonly Dictionary<string, VerificationEntry> Entries = new();

    public static string GenerateCode() => Random.Shared.Next(100000, 999999).ToString();
}
