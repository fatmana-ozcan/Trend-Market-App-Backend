namespace TrendMarketServer.Models;

public static class ShipmentStatus
{
    public const string Preparing = "Hazırlanıyor";
    public const string Shipped = "KargoyaVerildi";
    public const string InTransit = "Yolda";
    public const string Delivered = "TeslimEdildi";
}

public class Shipment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int SellerId { get; set; }
    public string Status { get; set; } = ShipmentStatus.Preparing;
    public string? CarrierCode { get; set; }
    public string? CourierName { get; set; }
    public string? CourierPhone { get; set; }
    public string? CourierEmail { get; set; }
    public DateTime? EstimatedDeliveryDate { get; set; }
    public string? TrackingNumber { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
