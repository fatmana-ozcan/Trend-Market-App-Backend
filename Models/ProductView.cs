namespace TrendMarketServer.Models;

public class ProductView
{
    public int Id { get; set; }
    // "Önceden gezdiklerim" artık misafirken de kaydedilir: CustomerId = 0 iken satır cihazın
    // SessionId'sine bağlıdır, giriş yapıldığında CartEntry/FavoriteEntry ile aynı şekilde
    // hesaba devredilir (bkz. ProductsController.AdoptSessionData).
    public int CustomerId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
}
