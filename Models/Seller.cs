namespace TrendMarketServer.Models;

public class Seller
{
    public int Id { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // Yeni kayıt olan satıcılar admin onayı verilene kadar giriş yapıp satıcı olarak
    // işlem yapamaz. DB'ye eklenen sütunun varsayılanı true olduğundan (bkz. migration),
    // bu alanı ekleme migration'ından ÖNCE var olan satıcılar otomatik onaylı sayılır;
    // C# tarafındaki bu varsayılan (false) ise SADECE kod üzerinden (yeni kayıt) oluşturulan
    // satırlara uygulanır.
    public bool IsApproved { get; set; } = false;
    // Admin panelinden "zorla çıkış yap" tetiklendiğinde artırılır; token'daki "sv" claim'i
    // bununla eşleşmiyorsa (bkz. Program.cs OnTokenValidated) o token artık geçersiz sayılır.
    public int SessionVersion { get; set; } = 0;
}
