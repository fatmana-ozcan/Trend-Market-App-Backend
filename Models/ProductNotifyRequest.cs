namespace TrendMarketServer.Models;

public class ProductNotifyRequest
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int? VariantId { get; set; }
    public int CustomerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
