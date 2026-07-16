namespace TrendMarketServer.Models;

// Sepet artık cihaza özel kalıcı bir kimlik (X-Cart-Session başlığı) ile veritabanında
// tutulur — önceki bellek-içi CartStore hem sunucu her yeniden başladığında siliniyordu
// hem de tüm ziyaretçiler arasında paylaşılıyordu.
public class CartEntry
{
    public int Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    // Renk ve beden bağımsız eksenler olduğundan (bkz. ProductVariant) her ikisi de
    // ayrı ayrı seçilip aynı sepet satırında birlikte taşınabilir.
    public int? ColorVariantId { get; set; }
    public int? SizeVariantId { get; set; }
}
