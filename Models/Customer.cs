namespace TrendMarketServer.Models;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // Admin panelinden "zorla çıkış yap" tetiklendiğinde artırılır; token'daki "sv" claim'i
    // bununla eşleşmiyorsa (bkz. Program.cs OnTokenValidated) o token artık geçersiz sayılır.
    public int SessionVersion { get; set; } = 0;
}
