namespace TrendMarketServer.Models;

public class ProductVariant
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    // Bir varyant satırı renk YA DA beden seçeneğini temsil eder (ikisi birden değil) —
    // renk ve beden bu uygulamada bağımsız eksenler olarak sunulur, tam SKU matrisi (ör.
    // "Kırmızı - L") gerekmiyor.
    public string? Color { get; set; }
    public string? Size { get; set; }
    // null ise, üründeki ana görsel kullanılır (her renk için ayrı fotoğraf zorunlu değil).
    public string? Image { get; set; }
    public int Stock { get; set; }
    // null ise, üründeki ana (Product.Price) fiyat kullanılır.
    public decimal? Price { get; set; }
}
