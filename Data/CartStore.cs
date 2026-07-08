using TrendMarketServer.Models;

namespace TrendMarketServer.Data;

public class CartItem
{
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
}

// Basit, tek kullanıcılık (anonim) bellek içi sepet — müşteri hesapları eklendi ama
// gezinme/sepete ekleme hâlâ girişsiz; sadece "Siparişi Onayla" adımı müşteri girişi ister.
public static class CartStore
{
    public static readonly Dictionary<int, CartItem> Items = new();
}
