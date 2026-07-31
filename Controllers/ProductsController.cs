using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrendMarketServer.Data;
using TrendMarketServer.Models;
using TrendMarketServer.Services;

namespace TrendMarketServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ImageStorageService _imageStorage;

        public ProductsController(AppDbContext db, ImageStorageService imageStorage)
        {
            _db = db;
            _imageStorage = imageStorage;
        }

        public class ReviewDto
        {
            public string Comment { get; set; } = string.Empty;
            public IFormFile? Image { get; set; }
        }

        private int CurrentCustomerId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Cihazda kalıcı üretilip her istekte gönderilen anonim oturum kimliği; misafir
        // sepetini/favorilerini ayırt etmek için kullanılır.
        private string CartSessionId =>
            Request.Headers.TryGetValue("X-Cart-Session", out var values) && !string.IsNullOrWhiteSpace(values.ToString())
                ? values.ToString()
                : "anonymous";

        // Sepet/favori/gezinti uçları giriş zorunlu değildir ama token gönderildiyse kimlik yine
        // de çözülür. Giriş yapılmışsa satırlar hesaba, yapılmamışsa misafir cihaz oturumuna
        // aittir. Bu ikili (OwnerCustomerId, OwnerSessionId) tüm sorgularda birlikte kullanılır;
        // hesaba devredilen satırlarda SessionId "" olduğundan sepet cihazdan bağımsız hale gelir.
        private int OwnerCustomerId =>
            User.Identity?.IsAuthenticated == true && User.IsInRole("Customer")
                ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
                : 0;

        private string OwnerSessionId => OwnerCustomerId == 0 ? CartSessionId : string.Empty;

        private async Task<bool> HasPurchasedAsync(int customerId, int productId)
        {
            return await _db.Orders
                .Where(o => o.CustomerId == customerId)
                .Join(_db.OrderItems, o => o.Id, oi => oi.OrderId, (o, oi) => oi)
                .AnyAsync(oi => oi.ProductId == productId);
        }

        // Ürün listelerini (vitrin, gezinti geçmişi) ortak şekilde JSON'a dönüştürür; son 30
        // günün fiyat geçmişini tek sorguda toplu çekerek N+1 sorgudan kaçınır.
        private async Task<List<object>> ShapeProductsAsync(List<Product> products)
        {
            var productIds = products.Select(p => p.Id).ToList();
            var cutoff = DateTime.UtcNow.AddDays(-30);
            var recentHistory = await _db.ProductPriceHistories
                .Where(h => productIds.Contains(h.ProductId) && h.RecordedAt >= cutoff)
                .ToListAsync();
            var historyByProduct = recentHistory.GroupBy(h => h.ProductId).ToDictionary(g => g.Key, g => g.ToList());

            var ownerId = OwnerCustomerId;
            var ownerSession = OwnerSessionId;
            // Bir ürünün birden fazla varyant kombinasyonuyla (farklı renk/beden) ayrı sepet
            // satırları olabileceğinden, ürün başına adet toplamı GroupBy ile alınır — aksi halde
            // ToDictionaryAsync tekrarlı ProductId anahtarında hata fırlatırdı.
            var cartQuantities = await _db.CartEntries
                .Where(c => c.CustomerId == ownerId && c.SessionId == ownerSession && productIds.Contains(c.ProductId))
                .GroupBy(c => c.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(c => c.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Quantity);

            // FavoriteEntry.Id, favorilere eklendiği sırayı yansıtır (bkz. GetCartDetails'teki
            // aynı mantık) — Favoriler ekranında en son favorilenen en üstte gösterilebilsin diye
            // sadece var/yok bilgisi değil, sıralama için kullanılabilecek bu değer de dönülür.
            var favoriteEntryIdByProduct = await _db.FavoriteEntries
                .Where(f => f.CustomerId == ownerId && f.SessionId == ownerSession && productIds.Contains(f.ProductId))
                .ToDictionaryAsync(f => f.ProductId, f => f.Id);

            var variants = await _db.ProductVariants
                .Where(v => productIds.Contains(v.ProductId))
                .ToListAsync();
            var variantsByProduct = variants.GroupBy(v => v.ProductId).ToDictionary(g => g.Key, g => g.ToList());

            // Kapak görseli (Product.Image) hariç ek galeri görselleri — bkz. ProductImage.
            var galleryImages = await _db.ProductImages
                .Where(i => productIds.Contains(i.ProductId))
                .OrderBy(i => i.SortOrder)
                .ToListAsync();
            var imagesByProduct = galleryImages.GroupBy(i => i.ProductId).ToDictionary(g => g.Key, g => g.ToList());

            return products.Select(p =>
            {
                decimal? last30DayLowestPrice = null;
                decimal? oldPrice = null;
                bool hasPriceDrop = false;
                if (historyByProduct.TryGetValue(p.Id, out var rows))
                {
                    last30DayLowestPrice = rows.Min(r => r.Price);
                    hasPriceDrop = rows.Any(r => r.Price > p.Price);
                    // "Eski fiyat" olarak son 30 gündeki en yüksek kaydı gösteriyoruz — üstü çizili
                    // fiyat, ürünün yakın zamanda gerçekten bu tutara satıldığını temsil eder.
                    if (hasPriceDrop) oldPrice = rows.Max(r => r.Price);
                }

                var productVariants = variantsByProduct.TryGetValue(p.Id, out var v)
                    ? v.Select(x => (object)new { x.Id, x.Color, x.Size, x.Image, x.Stock, x.Price }).ToList()
                    : new List<object>();

                var productImages = imagesByProduct.TryGetValue(p.Id, out var imgs)
                    ? imgs.Select(x => (object)new { x.Id, x.Url }).ToList()
                    : new List<object>();

                return (object)new
                {
                    p.Id,
                    p.SellerId,
                    p.Name,
                    p.NameEn,
                    p.NameDe,
                    p.Category,
                    p.Brand,
                    p.Color,
                    p.Size,
                    p.Image,
                    p.Price,
                    p.CostPrice,
                    p.Stock,
                    p.SoldCount,
                    p.RatingSum,
                    p.RatingCount,
                    p.Rating,
                    IsFavorite = favoriteEntryIdByProduct.ContainsKey(p.Id),
                    FavoriteOrder = favoriteEntryIdByProduct.TryGetValue(p.Id, out var favId) ? favId : (int?)null,
                    CartQuantity = cartQuantities.TryGetValue(p.Id, out var qty) ? qty : 0,
                    Last30DayLowestPrice = last30DayLowestPrice,
                    HasPriceDrop = hasPriceDrop,
                    OldPrice = oldPrice,
                    Variants = productVariants,
                    Images = productImages,
                };
            }).ToList();
        }

        // 9. Ürün Yorumlarını Listele (herkese açık)
        [HttpGet("{productId}/reviews")]
        public async Task<IActionResult> GetReviews(int productId)
        {
            var reviews = await _db.ProductReviews
                .Where(r => r.ProductId == productId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.CustomerName,
                    r.Comment,
                    r.ImageUrl,
                    r.CreatedAt,
                })
                .ToListAsync();

            return Ok(reviews);
        }

        // 10. Bu Müşteri Bu Ürüne Yorum Yapabilir mi? (sadece satın alanlar yorum yapabilir)
        [HttpGet("{productId}/can-review")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> CanReview(int productId)
        {
            var canReview = await HasPurchasedAsync(CurrentCustomerId, productId);
            return Ok(new { canReview });
        }

        // 11. Ürüne Yorum Yap (sadece bu ürünü satın alan müşteriler)
        [HttpPost("{productId}/reviews")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> AddReview(int productId, [FromForm] ReviewDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Comment))
                return BadRequest(new { success = false, message = "Yorum metni zorunludur." });

            var product = await _db.Products.FindAsync(productId);
            if (product == null)
                return NotFound(new { success = false, message = "Ürün bulunamadı." });

            var customerId = CurrentCustomerId;
            if (!await HasPurchasedAsync(customerId, productId))
                return BadRequest(new { success = false, message = "Bu ürünü satın alan müşteriler yorum yapabilir." });

            var customer = await _db.Customers.FindAsync(customerId);
            if (customer == null)
                return Unauthorized(new { success = false, message = "Hesap bulunamadı." });

            string? imageUrl = null;
            if (dto.Image != null)
            {
                imageUrl = await _imageStorage.SaveAsync(dto.Image);
                if (imageUrl == null)
                    return BadRequest(new { success = false, message = "Desteklenmeyen veya bozuk görsel dosyası." });
            }

            var review = new ProductReview
            {
                ProductId = productId,
                CustomerId = customerId,
                CustomerName = customer.Name,
                Comment = dto.Comment.Trim(),
                ImageUrl = imageUrl,
            };

            _db.ProductReviews.Add(review);
            await _db.SaveChangesAsync();

            return Ok(new { success = true, review });
        }

        // Mesaj Model Tanımı
        public class MessageItem
        {
            public int ProductId { get; set; }
            public string Sender { get; set; } = string.Empty; // "Müşteri" veya "Satıcı"
            public string Text { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
        }

        // Parametre taşımak için DTO Sınıfı
        public class MessageDto
        {
            public string Sender { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
        }

        // --- BELLEKTE TUTULAN MESAJ VERİLERİ ---
        // (Favoriler artık cihaz oturumuna göre veritabanında tutuluyor — bkz. FavoriteEntry.)

        // Bir ürün silindiğinde favori listesinde hayalet (var olmayan ürüne ait) bir kayıt
        // kalmasın diye SellerProductsController.DeleteProduct tarafından çağrılır.
        internal static async Task RemoveFromFavoritesAsync(AppDbContext db, int productId) =>
            await db.FavoriteEntries.Where(f => f.ProductId == productId).ExecuteDeleteAsync();

        private static readonly List<MessageItem> GlobalMessages = new List<MessageItem>
        {
            new MessageItem { ProductId = 1, Sender = "Satıcı", Text = "Merhaba! Bu ürün hakkında merak ettiğiniz bir şey var mı?", Timestamp = DateTime.Now.AddMinutes(-5) }
        };

        // --- ENDPOINTS (API METOTLARI) ---

        // 1. Ürünleri Listeleme ve Filtreleme
        [HttpGet]
        public async Task<IActionResult> GetProducts([FromQuery] string category = "HEPSİ", [FromQuery] string search = "")
        {
            var query = _db.Products.AsQueryable();

            if (!string.IsNullOrEmpty(category) && category.ToUpper() != "HEPSİ")
            {
                query = query.Where(p => p.Category.ToUpper() == category.ToUpper());
            }

            if (!string.IsNullOrEmpty(search))
            {
                // SQLite'ta string.Contains() (instr()'e çevrilir) büyük/küçük harf duyarlıdır,
                // bu yüzden "powerbank" araması "Powerbank" ürününü bulamıyordu. EF.Functions.Like
                // ise SQLite'ın varsayılan LIKE davranışını kullanır (ASCII harflerde harf duyarsız).
                query = query.Where(p => EF.Functions.Like(p.Name, $"%{search}%"));
            }

            var products = await query.ToListAsync();
            var shaped = await ShapeProductsAsync(products);
            return Ok(shaped);
        }

        // 1c. Sistemde şu an kullanılan tüm kategori adları — satıcı ürün formunda hem
        // önceden tanımlı hem de başka satıcıların eklediği özel kategorileri göstermek için.
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _db.Products
                .Select(p => p.Category)
                .Where(c => c != null && c != "")
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
            return Ok(categories);
        }

        // 1b. Alt menüdeki sepet/favori rozetleri için toplam adet
        [HttpGet("counts")]
        public async Task<IActionResult> GetCounts()
        {
            var ownerId = OwnerCustomerId;
            var ownerSession = OwnerSessionId;
            var cartCount = await _db.CartEntries
                .Where(c => c.CustomerId == ownerId && c.SessionId == ownerSession)
                .SumAsync(c => c.Quantity);
            var favoriteCount = await _db.FavoriteEntries
                .CountAsync(f => f.CustomerId == ownerId && f.SessionId == ownerSession);
            return Ok(new { cartCount, favoriteCount });
        }

        // 12. Önceden Gezdiğim Ürünler (misafirken de tutulur; girişte hesaba devredilir)
        [HttpGet("recently-viewed")]
        public async Task<IActionResult> GetRecentlyViewed()
        {
            var ownerId = OwnerCustomerId;
            var ownerSession = OwnerSessionId;
            var viewedProductIds = await _db.ProductViews
                .Where(v => v.CustomerId == ownerId && v.SessionId == ownerSession)
                .OrderByDescending(v => v.ViewedAt)
                .Select(v => v.ProductId)
                .Take(10)
                .ToListAsync();

            var products = await _db.Products.Where(p => viewedProductIds.Contains(p.Id)).ToListAsync();
            var orderedProducts = viewedProductIds
                .Select(id => products.FirstOrDefault(p => p.Id == id))
                .Where(p => p != null)
                .Select(p => p!)
                .ToList();

            var shaped = await ShapeProductsAsync(orderedProducts);
            return Ok(shaped);
        }

        // 13. Ürün Görüntülemesini Kaydet (gezinti geçmişi için)
        [HttpPost("{id}/view")]
        public async Task<IActionResult> RecordView(int id)
        {
            var productExists = await _db.Products.AnyAsync(p => p.Id == id);
            if (!productExists) return NotFound(new { success = false, message = "Ürün bulunamadı." });

            var ownerId = OwnerCustomerId;
            var ownerSession = OwnerSessionId;
            var existing = await _db.ProductViews
                .FirstOrDefaultAsync(v => v.CustomerId == ownerId && v.SessionId == ownerSession && v.ProductId == id);

            if (existing != null)
            {
                existing.ViewedAt = DateTime.UtcNow;
            }
            else
            {
                _db.ProductViews.Add(new ProductView { CustomerId = ownerId, SessionId = ownerSession, ProductId = id });
            }

            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // 2. Sepete Ürün Ekleme (renk/beden seçiliyse aynı ürünün farklı varyantları ayrı satır olarak tutulur)
        [HttpPost("cart/{id}")]
        public async Task<IActionResult> AddToCart(int id, [FromQuery] int? colorVariantId = null, [FromQuery] int? sizeVariantId = null)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null) return NotFound(new { success = false, message = "Ürün bulunamadı" });

            if (colorVariantId.HasValue && !await _db.ProductVariants.AnyAsync(v => v.Id == colorVariantId.Value && v.ProductId == id))
                return BadRequest(new { success = false, message = "Geçersiz renk seçeneği." });
            if (sizeVariantId.HasValue && !await _db.ProductVariants.AnyAsync(v => v.Id == sizeVariantId.Value && v.ProductId == id))
                return BadRequest(new { success = false, message = "Geçersiz beden seçeneği." });

            var ownerId = OwnerCustomerId;
            var ownerSession = OwnerSessionId;
            var entry = await _db.CartEntries.FirstOrDefaultAsync(c =>
                c.CustomerId == ownerId && c.SessionId == ownerSession && c.ProductId == id &&
                c.ColorVariantId == colorVariantId && c.SizeVariantId == sizeVariantId);
            if (entry != null)
            {
                entry.Quantity += 1;
            }
            else
            {
                _db.CartEntries.Add(new CartEntry
                {
                    CustomerId = ownerId,
                    SessionId = ownerSession,
                    ProductId = id,
                    Quantity = 1,
                    ColorVariantId = colorVariantId,
                    SizeVariantId = sizeVariantId,
                });
            }
            await _db.SaveChangesAsync();

            int totalItemsCount = await _db.CartEntries
                .Where(c => c.CustomerId == ownerId && c.SessionId == ownerSession)
                .SumAsync(c => c.Quantity);
            return Ok(new { success = true, currentCartCount = totalItemsCount });
        }

        // 3. Sepetten Ürün Silme / Azaltma (aynı ürünün hangi varyant satırından azaltılacağını ayırt eder)
        [HttpDelete("cart/{id}")]
        public async Task<IActionResult> RemoveFromCart(int id, [FromQuery] int? colorVariantId = null, [FromQuery] int? sizeVariantId = null)
        {
            var ownerId = OwnerCustomerId;
            var ownerSession = OwnerSessionId;
            var entry = await _db.CartEntries.FirstOrDefaultAsync(c =>
                c.CustomerId == ownerId && c.SessionId == ownerSession && c.ProductId == id &&
                c.ColorVariantId == colorVariantId && c.SizeVariantId == sizeVariantId);
            if (entry == null)
                return BadRequest(new { success = false, message = "Ürün sepette yok" });

            entry.Quantity -= 1;
            if (entry.Quantity <= 0)
            {
                _db.CartEntries.Remove(entry);
            }
            await _db.SaveChangesAsync();

            int totalItemsCount = await _db.CartEntries
                .Where(c => c.CustomerId == ownerId && c.SessionId == ownerSession)
                .SumAsync(c => c.Quantity);
            return Ok(new { success = true, currentCartCount = totalItemsCount });
        }

        // 3b. Sepeti tamamen boşaltma (kullanıcının kendi isteğiyle). Giriş/çıkışta artık
        // çağrılmaz — hesap değişiminde satırlar silinmek yerine devredilir, bkz. AdoptSessionData.
        [HttpDelete("cart")]
        public async Task<IActionResult> ClearCart()
        {
            var ownerId = OwnerCustomerId;
            var ownerSession = OwnerSessionId;
            var entries = _db.CartEntries.Where(c => c.CustomerId == ownerId && c.SessionId == ownerSession);
            _db.CartEntries.RemoveRange(entries);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // 4. Sepet Detaylarını Getirme (renk/beden varyantı seçiliyse etiketi ve o varyanta özel
        // fiyatı da döner — bkz. ProductVariant.Price, sepetteki tutar ve ödeme bu fiyat üzerinden hesaplanır).
        [HttpGet("cart-details")]
        public async Task<IActionResult> GetCartDetails()
        {
            var ownerId = OwnerCustomerId;
            var ownerSession = OwnerSessionId;
            // En son eklenen ürün en üstte görünsün diye Id'ye göre azalan sırada (Id, satır
            // ilk eklendiğinde otomatik artan bir değer olduğundan ekleme sırasını yansıtır;
            // zaten sepette olan bir üründe miktar artırmak yeni satır oluşturmadığı için sırasını değiştirmez).
            var entries = await _db.CartEntries
                .Where(c => c.CustomerId == ownerId && c.SessionId == ownerSession)
                .OrderByDescending(c => c.Id)
                .ToListAsync();
            var productIds = entries.Select(e => e.ProductId).ToList();
            var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

            var variantIds = entries
                .SelectMany(e => new[] { e.ColorVariantId, e.SizeVariantId })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToList();
            var variants = await _db.ProductVariants.Where(v => variantIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id);

            var result = entries
                .Where(e => products.ContainsKey(e.ProductId))
                .Select(e =>
                {
                    var product = products[e.ProductId];
                    var colorVariant = e.ColorVariantId.HasValue && variants.TryGetValue(e.ColorVariantId.Value, out var cv) ? cv : null;
                    var sizeVariant = e.SizeVariantId.HasValue && variants.TryGetValue(e.SizeVariantId.Value, out var sv) ? sv : null;
                    var effectiveUnitPrice = sizeVariant?.Price ?? colorVariant?.Price ?? product.Price;

                    return new
                    {
                        cartEntryId = e.Id,
                        product,
                        quantity = e.Quantity,
                        colorVariantId = e.ColorVariantId,
                        sizeVariantId = e.SizeVariantId,
                        colorVariantLabel = colorVariant?.Color,
                        sizeVariantLabel = sizeVariant?.Size,
                        effectiveUnitPrice,
                    };
                });

            return Ok(result);
        }

        // 5. Favorilere Ekleme / Kaldırma (giriş yapılmışsa hesaba, değilse cihaz oturumuna)
        [HttpPost("favorites/{id}")]
        public async Task<IActionResult> ToggleFavorite(int id)
        {
            var ownerId = OwnerCustomerId;
            var ownerSession = OwnerSessionId;
            var existing = await _db.FavoriteEntries.FirstOrDefaultAsync(f =>
                f.CustomerId == ownerId && f.SessionId == ownerSession && f.ProductId == id);
            bool isFavorite;
            if (existing != null)
            {
                _db.FavoriteEntries.Remove(existing);
                isFavorite = false;
            }
            else
            {
                _db.FavoriteEntries.Add(new FavoriteEntry { CustomerId = ownerId, SessionId = ownerSession, ProductId = id });
                isFavorite = true;
            }
            await _db.SaveChangesAsync();
            var favoriteCount = await _db.FavoriteEntries
                .CountAsync(f => f.CustomerId == ownerId && f.SessionId == ownerSession);
            return Ok(new { success = true, favoriteCount, isFavorite });
        }

        // Favorileri tamamen temizleme (kullanıcının kendi isteğiyle). Giriş/çıkışta artık
        // çağrılmaz — bkz. AdoptSessionData.
        [HttpDelete("favorites")]
        public async Task<IActionResult> ClearFavorites()
        {
            var ownerId = OwnerCustomerId;
            var ownerSession = OwnerSessionId;
            await _db.FavoriteEntries
                .Where(f => f.CustomerId == ownerId && f.SessionId == ownerSession)
                .ExecuteDeleteAsync();
            return Ok(new { success = true });
        }

        // 5c. Misafirken eklenen sepet/favori/gezinti kayıtlarını giriş yapan hesaba devreder.
        // Önceden bu noktada hepsi siliniyordu (ClearCart + ClearFavorites), bu yüzden çıkış
        // yapıp tekrar giren kullanıcı sepetini ve favorilerini kaybediyordu. Devirden sonra
        // misafir oturumu boş kalır, dolayısıyla aynı cihazda başka bir hesaba girildiğinde
        // önceki hesabın verisi sızmaz. Bkz. App.js handleCustomerAuthenticated.
        [HttpPost("session/adopt")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> AdoptSessionData()
        {
            var customerId = CurrentCustomerId;
            var sessionId = CartSessionId;

            var guestCart = await _db.CartEntries
                .Where(c => c.CustomerId == 0 && c.SessionId == sessionId)
                .ToListAsync();
            if (guestCart.Count > 0)
            {
                var accountCart = await _db.CartEntries
                    .Where(c => c.CustomerId == customerId && c.SessionId == "")
                    .ToListAsync();
                foreach (var guestEntry in guestCart)
                {
                    // Aynı ürün + aynı varyant kombinasyonu hesapta zaten varsa adetler toplanır,
                    // yoksa misafir satırı olduğu gibi hesaba geçirilir.
                    var existing = accountCart.FirstOrDefault(c =>
                        c.ProductId == guestEntry.ProductId &&
                        c.ColorVariantId == guestEntry.ColorVariantId &&
                        c.SizeVariantId == guestEntry.SizeVariantId);
                    if (existing != null)
                    {
                        existing.Quantity += guestEntry.Quantity;
                        _db.CartEntries.Remove(guestEntry);
                    }
                    else
                    {
                        guestEntry.CustomerId = customerId;
                        guestEntry.SessionId = string.Empty;
                        accountCart.Add(guestEntry);
                    }
                }
            }

            var guestFavorites = await _db.FavoriteEntries
                .Where(f => f.CustomerId == 0 && f.SessionId == sessionId)
                .ToListAsync();
            if (guestFavorites.Count > 0)
            {
                var accountFavoriteProductIds = await _db.FavoriteEntries
                    .Where(f => f.CustomerId == customerId && f.SessionId == "")
                    .Select(f => f.ProductId)
                    .ToListAsync();
                foreach (var guestFavorite in guestFavorites)
                {
                    if (accountFavoriteProductIds.Contains(guestFavorite.ProductId))
                    {
                        _db.FavoriteEntries.Remove(guestFavorite);
                    }
                    else
                    {
                        guestFavorite.CustomerId = customerId;
                        guestFavorite.SessionId = string.Empty;
                        accountFavoriteProductIds.Add(guestFavorite.ProductId);
                    }
                }
            }

            var guestViews = await _db.ProductViews
                .Where(v => v.CustomerId == 0 && v.SessionId == sessionId)
                .ToListAsync();
            if (guestViews.Count > 0)
            {
                var accountViews = await _db.ProductViews
                    .Where(v => v.CustomerId == customerId && v.SessionId == "")
                    .ToListAsync();
                foreach (var guestView in guestViews)
                {
                    var existing = accountViews.FirstOrDefault(v => v.ProductId == guestView.ProductId);
                    if (existing != null)
                    {
                        // Ürün her iki tarafta da gezilmişse en son ziyaret zamanı korunur.
                        if (guestView.ViewedAt > existing.ViewedAt) existing.ViewedAt = guestView.ViewedAt;
                        _db.ProductViews.Remove(guestView);
                    }
                    else
                    {
                        guestView.CustomerId = customerId;
                        guestView.SessionId = string.Empty;
                        accountViews.Add(guestView);
                    }
                }
            }

            await _db.SaveChangesAsync();

            var cartCount = await _db.CartEntries
                .Where(c => c.CustomerId == customerId && c.SessionId == "")
                .SumAsync(c => c.Quantity);
            var favoriteCount = await _db.FavoriteEntries
                .CountAsync(f => f.CustomerId == customerId && f.SessionId == "");
            return Ok(new { success = true, cartCount, favoriteCount });
        }

        // 6. Ürüne Puan Verme (sadece bu ürünü satın alan müşteriler)
        [HttpPost("rate/{id}")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> RateProduct(int id, [FromQuery] int score)
        {
            if (score < 1 || score > 5) return BadRequest("Puan 1-5 arasında olmalıdır.");

            var product = await _db.Products.FindAsync(id);
            if (product == null) return NotFound("Ürün bulunamadı");

            if (!await HasPurchasedAsync(CurrentCustomerId, id))
                return BadRequest(new { success = false, message = "Bu ürünü satın alan müşteriler puan verebilir." });

            product.RatingSum += score;
            product.RatingCount += 1;
            await _db.SaveChangesAsync();

            return Ok(new { success = true, newRating = product.Rating });
        }

        // 14. Stoğa Gelince Haber Ver (bir renk/beden varyantı ya da doğrudan ürün için)
        [HttpPost("{id}/notify")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> RequestStockNotification(int id, [FromQuery] int? variantId = null)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null) return NotFound(new { success = false, message = "Ürün bulunamadı." });

            var customerId = CurrentCustomerId;
            var existing = await _db.ProductNotifyRequests.FirstOrDefaultAsync(n =>
                n.ProductId == id && n.CustomerId == customerId && n.VariantId == variantId);

            if (existing != null)
            {
                return Ok(new { success = true, alreadyRequested = true });
            }

            _db.ProductNotifyRequests.Add(new ProductNotifyRequest
            {
                ProductId = id,
                VariantId = variantId,
                CustomerId = customerId,
            });
            await _db.SaveChangesAsync();

            return Ok(new { success = true, alreadyRequested = false });
        }

        // 7. SOHBET: Belirli Bir Ürüne Ait Mesaj Geçmişini Getir
        [HttpGet("messages/{productId}")]
        public IActionResult GetMessages(int productId)
        {
            var productMessages = GlobalMessages
                .Where(m => m.ProductId == productId)
                .OrderBy(m => m.Timestamp)
                .ToList();
            return Ok(productMessages);
        }

        // 8. SOHBET: Yeni Mesaj Gönder
        [HttpPost("messages/{productId}")]
        public IActionResult SendMessage(int productId, [FromBody] MessageDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.Text) || string.IsNullOrEmpty(dto.Sender))
            {
                return BadRequest(new { success = false, message = "Mesaj veya gönderici bilgisi boş olamaz." });
            }

            var newMessage = new MessageItem
            {
                ProductId = productId,
                Sender = dto.Sender,
                Text = dto.Text,
                Timestamp = DateTime.Now
            };

            GlobalMessages.Add(newMessage);
            return Ok(new { success = true, newMessage });
        }
    }
}
