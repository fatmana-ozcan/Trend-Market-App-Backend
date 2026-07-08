namespace TrendMarketServer.Models;

public class ProductPriceHistory
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public decimal Price { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
