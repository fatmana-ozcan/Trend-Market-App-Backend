namespace TrendMarketServer.Models;

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ShipmentId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductImage { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public int SellerId { get; set; }
    // Sipariş anında seçilmiş olan renk/beden — sadece görüntüleme amaçlı (bkz. OrdersController.GetMyOrders).
    public string? ColorVariantLabel { get; set; }
    public string? SizeVariantLabel { get; set; }
}
