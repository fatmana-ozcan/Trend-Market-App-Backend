namespace TrendMarketServer.Models;

// Favoriler CartEntry ile aynı prensiple cihaza özel oturum kimliğine (X-Cart-Session) göre
// veritabanında tutulur — önceki bellek-içi FavoriteProducts hem sunucu her yeniden başladığında
// siliniyordu hem de (CartEntry'nin eski hali gibi) tüm ziyaretçiler/hesaplar arasında ortaktı.
public class FavoriteEntry
{
    public int Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public int ProductId { get; set; }
}
