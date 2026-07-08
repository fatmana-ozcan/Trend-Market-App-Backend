namespace TrendMarketServer.Models;

public class ProductView
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int ProductId { get; set; }
    public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
}
