using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrendMarketServer.Data;
using TrendMarketServer.Models;

namespace TrendMarketServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public ProductsController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public class ReviewDto
        {
            public string Comment { get; set; } = string.Empty;
            public IFormFile? Image { get; set; }
        }

        private int CurrentCustomerId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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

            return products.Select(p =>
            {
                decimal? last30DayLowestPrice = null;
                bool hasPriceDrop = false;
                if (historyByProduct.TryGetValue(p.Id, out var rows))
                {
                    last30DayLowestPrice = rows.Min(r => r.Price);
                    hasPriceDrop = rows.Any(r => r.Price > p.Price);
                }

                return (object)new
                {
                    p.Id,
                    p.SellerId,
                    p.Name,
                    p.Category,
                    p.Image,
                    p.Price,
                    p.CostPrice,
                    p.Stock,
                    p.SoldCount,
                    p.RatingSum,
                    p.RatingCount,
                    p.Rating,
                    IsFavorite = FavoriteProducts.Contains(p.Id),
                    CartQuantity = CartStore.Items.TryGetValue(p.Id, out var cartItem) ? cartItem.Quantity : 0,
                    Last30DayLowestPrice = last30DayLowestPrice,
                    HasPriceDrop = hasPriceDrop,
                };
            }).ToList();
        }

        private async Task<string> SaveReviewImageAsync(IFormFile image)
        {
            var uploadsRoot = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads");
            Directory.CreateDirectory(uploadsRoot);

            var ext = Path.GetExtension(image.FileName);
            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadsRoot, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            return $"/uploads/{fileName}";
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
                imageUrl = await SaveReviewImageAsync(dto.Image);
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

        // --- BELLEKTE TUTULAN FAVORİ / MESAJ VERİLERİ (sepet artık CartStore'da, ürünler veritabanında) ---
        private static readonly HashSet<int> FavoriteProducts = new HashSet<int>();

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
                query = query.Where(p => p.Name.Contains(search));
            }

            var products = await query.ToListAsync();
            var shaped = await ShapeProductsAsync(products);
            return Ok(shaped);
        }

        // 12. Önceden Gezdiğim Ürünler
        [HttpGet("recently-viewed")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetRecentlyViewed()
        {
            var viewedProductIds = await _db.ProductViews
                .Where(v => v.CustomerId == CurrentCustomerId)
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
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> RecordView(int id)
        {
            var productExists = await _db.Products.AnyAsync(p => p.Id == id);
            if (!productExists) return NotFound(new { success = false, message = "Ürün bulunamadı." });

            var existing = await _db.ProductViews
                .FirstOrDefaultAsync(v => v.CustomerId == CurrentCustomerId && v.ProductId == id);

            if (existing != null)
            {
                existing.ViewedAt = DateTime.UtcNow;
            }
            else
            {
                _db.ProductViews.Add(new ProductView { CustomerId = CurrentCustomerId, ProductId = id });
            }

            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // 2. Sepete Ürün Ekleme
        [HttpPost("cart/{id}")]
        public async Task<IActionResult> AddToCart(int id)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null) return NotFound(new { success = false, message = "Ürün bulunamadı" });

            if (CartStore.Items.ContainsKey(id))
            {
                CartStore.Items[id].Quantity += 1;
            }
            else
            {
                CartStore.Items[id] = new CartItem { Product = product, Quantity = 1 };
            }

            int totalItemsCount = CartStore.Items.Values.Sum(item => item.Quantity);
            return Ok(new { success = true, currentCartCount = totalItemsCount });
        }

        // 3. Sepetten Ürün Silme / Azaltma
        [HttpDelete("cart/{id}")]
        public IActionResult RemoveFromCart(int id)
        {
            if (!CartStore.Items.ContainsKey(id))
                return BadRequest(new { success = false, message = "Ürün sepette yok" });

            CartStore.Items[id].Quantity -= 1;
            if (CartStore.Items[id].Quantity <= 0)
            {
                CartStore.Items.Remove(id);
            }

            int totalItemsCount = CartStore.Items.Values.Sum(item => item.Quantity);
            return Ok(new { success = true, currentCartCount = totalItemsCount });
        }

        // 4. Sepet Detaylarını Getirme
        [HttpGet("cart-details")]
        public IActionResult GetCartDetails()
        {
            return Ok(CartStore.Items.Values.ToList());
        }

        // 5. Favorilere Ekleme / Kaldırma
        [HttpPost("favorites/{id}")]
        public IActionResult ToggleFavorite(int id)
        {
            bool isFavorite;
            if (FavoriteProducts.Contains(id))
            {
                FavoriteProducts.Remove(id);
                isFavorite = false;
            }
            else
            {
                FavoriteProducts.Add(id);
                isFavorite = true;
            }
            return Ok(new { success = true, favoriteCount = FavoriteProducts.Count, isFavorite });
        }

        // 6. Ürüne Puan Verme
        [HttpPost("rate/{id}")]
        public async Task<IActionResult> RateProduct(int id, [FromQuery] int score)
        {
            if (score < 1 || score > 5) return BadRequest("Puan 1-5 arasında olmalıdır.");

            var product = await _db.Products.FindAsync(id);
            if (product == null) return NotFound("Ürün bulunamadı");

            product.RatingSum += score;
            product.RatingCount += 1;
            await _db.SaveChangesAsync();

            return Ok(new { success = true, newRating = product.Rating });
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
