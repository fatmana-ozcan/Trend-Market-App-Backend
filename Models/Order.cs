namespace TrendMarketServer.Models;

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public decimal TotalAmount { get; set; }
    public decimal CouponUsed { get; set; }

    // Sipariş anındaki adres kopyası — adres defteri sonradan değişse de
    // geçmiş siparişin teslimat bilgisi bozulmasın.
    public string ShippingFullName { get; set; } = string.Empty;
    public string ShippingPhone { get; set; } = string.Empty;
    public string ShippingCity { get; set; } = string.Empty;
    public string ShippingDistrict { get; set; } = string.Empty;
    public string ShippingAddressText { get; set; } = string.Empty;
}
