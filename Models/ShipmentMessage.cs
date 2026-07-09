namespace TrendMarketServer.Models;

public static class ShipmentMessageSender
{
    public const string Customer = "Customer";
    public const string Seller = "Seller";
}

public class ShipmentMessage
{
    public int Id { get; set; }
    public int ShipmentId { get; set; }
    public string SenderRole { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
