namespace TrendMarketServer.Models;

public class CouponTransaction
{
    public int Id { get; set; }
    public int CustomerId { get; set; }

    // Pozitif = kazanılan kupon, negatif = harcanan kupon.
    public decimal Amount { get; set; }
    public int? OrderId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
