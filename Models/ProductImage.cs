namespace TrendMarketServer.Models;

// Ürünün kapak görseli (Product.Image) hariç, ek galeri görselleri — toplamda en fazla 4 görsel
// olacak şekilde (1 kapak + en fazla 3 ek) sınırlandırılır (bkz. SellerProductsController).
public class ProductImage
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Url { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
