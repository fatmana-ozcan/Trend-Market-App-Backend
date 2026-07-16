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
    [Route("api/seller")]
    [Authorize(Roles = "Seller")]
    public class SellerProductsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ImageStorageService _imageStorage;

        public SellerProductsController(AppDbContext db, ImageStorageService imageStorage)
        {
            _db = db;
            _imageStorage = imageStorage;
        }

        public class ProductFormDto
        {
            public string Name { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string? Brand { get; set; }
            public string? Color { get; set; }
            public string? Size { get; set; }
            public decimal Price { get; set; }
            public decimal CostPrice { get; set; }
            public int Stock { get; set; }
            public IFormFile? Image { get; set; }
        }

        public class ProductVariantDto
        {
            // null = yeni satır; dolu = mevcut varyantı günceller.
            public int? Id { get; set; }
            public string? Color { get; set; }
            public string? Size { get; set; }
            public int Stock { get; set; }
            public decimal? Price { get; set; }
        }

        private int CurrentSellerId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // 1. Bu satıcıya ait ürünleri listele
        [HttpGet("products")]
        public async Task<IActionResult> GetMyProducts()
        {
            var products = await _db.Products
                .Where(p => p.SellerId == CurrentSellerId)
                .OrderByDescending(p => p.Id)
                .ToListAsync();
            return Ok(products);
        }

        // 2. Yeni ürün ekle
        [HttpPost("products")]
        public async Task<IActionResult> CreateProduct([FromForm] ProductFormDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Category))
                return BadRequest(new { success = false, message = "Ürün adı ve kategori zorunludur." });

            string imageUrl = string.Empty;
            if (dto.Image != null)
            {
                var savedUrl = await _imageStorage.SaveAsync(dto.Image);
                if (savedUrl == null)
                    return BadRequest(new { success = false, message = "Desteklenmeyen veya bozuk görsel dosyası." });
                imageUrl = savedUrl;
            }

            var product = new Product
            {
                SellerId = CurrentSellerId,
                Name = dto.Name.Trim(),
                Category = dto.Category.Trim(),
                Brand = string.IsNullOrWhiteSpace(dto.Brand) ? null : dto.Brand.Trim(),
                Color = string.IsNullOrWhiteSpace(dto.Color) ? null : dto.Color.Trim(),
                Size = string.IsNullOrWhiteSpace(dto.Size) ? null : dto.Size.Trim(),
                Price = dto.Price,
                CostPrice = dto.CostPrice,
                Stock = dto.Stock,
                Image = imageUrl,
                SoldCount = 0,
                RatingSum = 0,
                RatingCount = 0,
            };

            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            _db.ProductPriceHistories.Add(new ProductPriceHistory { ProductId = product.Id, Price = product.Price });
            await _db.SaveChangesAsync();

            return Ok(new { success = true, product });
        }

        // 3. Ürün güncelle
        [HttpPut("products/{id}")]
        public async Task<IActionResult> UpdateProduct(int id, [FromForm] ProductFormDto dto)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null) return NotFound(new { success = false, message = "Ürün bulunamadı." });
            if (product.SellerId != CurrentSellerId) return Forbid();

            if (dto.Price != product.Price)
            {
                _db.ProductPriceHistories.Add(new ProductPriceHistory { ProductId = product.Id, Price = dto.Price });
            }

            product.Name = dto.Name.Trim();
            product.Category = dto.Category.Trim();
            product.Brand = string.IsNullOrWhiteSpace(dto.Brand) ? null : dto.Brand.Trim();
            product.Color = string.IsNullOrWhiteSpace(dto.Color) ? null : dto.Color.Trim();
            product.Size = string.IsNullOrWhiteSpace(dto.Size) ? null : dto.Size.Trim();
            product.Price = dto.Price;
            product.CostPrice = dto.CostPrice;
            product.Stock = dto.Stock;

            if (dto.Image != null)
            {
                var savedUrl = await _imageStorage.SaveAsync(dto.Image);
                if (savedUrl == null)
                    return BadRequest(new { success = false, message = "Desteklenmeyen veya bozuk görsel dosyası." });
                product.Image = savedUrl;
            }

            await _db.SaveChangesAsync();
            return Ok(new { success = true, product });
        }

        // 4. Ürün sil
        [HttpDelete("products/{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null) return NotFound(new { success = false, message = "Ürün bulunamadı." });
            if (product.SellerId != CurrentSellerId) return Forbid();

            _db.Products.Remove(product);
            await _db.SaveChangesAsync();
            ProductsController.RemoveFromFavorites(id);
            return Ok(new { success = true });
        }

        // 4b. Ürünün renk/beden varyantlarını listele (düzenleme formunu doldurmak için)
        [HttpGet("products/{productId}/variants")]
        public async Task<IActionResult> GetVariants(int productId)
        {
            var product = await _db.Products.FindAsync(productId);
            if (product == null) return NotFound(new { success = false, message = "Ürün bulunamadı." });
            if (product.SellerId != CurrentSellerId) return Forbid();

            var variants = await _db.ProductVariants
                .Where(v => v.ProductId == productId)
                .ToListAsync();
            return Ok(variants);
        }

        // 4c. Ürünün renk/beden varyantlarını topluca güncelle (satıcı formundaki listeyle değiştirir:
        // gönderilmeyen mevcut satırlar silinir, Id'si olanlar güncellenir, Id'siz olanlar eklenir).
        [HttpPut("products/{productId}/variants")]
        public async Task<IActionResult> SetVariants(int productId, [FromBody] List<ProductVariantDto> variants)
        {
            var product = await _db.Products.FindAsync(productId);
            if (product == null) return NotFound(new { success = false, message = "Ürün bulunamadı." });
            if (product.SellerId != CurrentSellerId) return Forbid();

            var existing = await _db.ProductVariants.Where(v => v.ProductId == productId).ToListAsync();
            var incomingIds = variants.Where(v => v.Id.HasValue).Select(v => v.Id!.Value).ToHashSet();

            _db.ProductVariants.RemoveRange(existing.Where(v => !incomingIds.Contains(v.Id)));

            foreach (var dto in variants)
            {
                var color = string.IsNullOrWhiteSpace(dto.Color) ? null : dto.Color.Trim();
                var size = string.IsNullOrWhiteSpace(dto.Size) ? null : dto.Size.Trim();

                if (dto.Id.HasValue)
                {
                    var row = existing.FirstOrDefault(v => v.Id == dto.Id.Value);
                    if (row == null) continue;
                    row.Color = color;
                    row.Size = size;
                    row.Stock = dto.Stock;
                    row.Price = dto.Price;
                }
                else
                {
                    _db.ProductVariants.Add(new ProductVariant
                    {
                        ProductId = productId,
                        Color = color,
                        Size = size,
                        Stock = dto.Stock,
                        Price = dto.Price,
                    });
                }
            }

            await _db.SaveChangesAsync();
            var result = await _db.ProductVariants.Where(v => v.ProductId == productId).ToListAsync();
            return Ok(new { success = true, variants = result });
        }

        // Kapak görseli (Product.Image) hariç, ürün başına en fazla bu kadar ek galeri görseli.
        private const int MaxExtraImages = 3;

        // 4d. Ürünün ek galeri görsellerini listele
        [HttpGet("products/{productId}/images")]
        public async Task<IActionResult> GetImages(int productId)
        {
            var product = await _db.Products.FindAsync(productId);
            if (product == null) return NotFound(new { success = false, message = "Ürün bulunamadı." });
            if (product.SellerId != CurrentSellerId) return Forbid();

            var images = await _db.ProductImages
                .Where(i => i.ProductId == productId)
                .OrderBy(i => i.SortOrder)
                .ToListAsync();
            return Ok(images);
        }

        // 4e. Ürüne yeni bir ek galeri görseli ekle (kapak + ek görseller toplamda 4'ü geçemez)
        [HttpPost("products/{productId}/images")]
        public async Task<IActionResult> AddImage(int productId, IFormFile image)
        {
            var product = await _db.Products.FindAsync(productId);
            if (product == null) return NotFound(new { success = false, message = "Ürün bulunamadı." });
            if (product.SellerId != CurrentSellerId) return Forbid();
            if (image == null) return BadRequest(new { success = false, message = "Görsel zorunludur." });

            var existingCount = await _db.ProductImages.CountAsync(i => i.ProductId == productId);
            if (existingCount >= MaxExtraImages)
                return BadRequest(new { success = false, message = $"Bir üründe en fazla {MaxExtraImages + 1} görsel olabilir." });

            var url = await _imageStorage.SaveAsync(image);
            if (url == null)
                return BadRequest(new { success = false, message = "Desteklenmeyen veya bozuk görsel dosyası." });

            var productImage = new ProductImage { ProductId = productId, Url = url, SortOrder = existingCount };
            _db.ProductImages.Add(productImage);
            await _db.SaveChangesAsync();

            return Ok(new { success = true, image = productImage });
        }

        // 4f. Ürünün ek galeri görsellerinden birini kaldır
        [HttpDelete("products/{productId}/images/{imageId}")]
        public async Task<IActionResult> DeleteImage(int productId, int imageId)
        {
            var product = await _db.Products.FindAsync(productId);
            if (product == null) return NotFound(new { success = false, message = "Ürün bulunamadı." });
            if (product.SellerId != CurrentSellerId) return Forbid();

            var image = await _db.ProductImages.FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == productId);
            if (image == null) return NotFound(new { success = false, message = "Görsel bulunamadı." });

            _db.ProductImages.Remove(image);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // 5. Satıcı Paneli Özet İstatistikleri
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var products = await _db.Products
                .Where(p => p.SellerId == CurrentSellerId)
                .ToListAsync();

            var totalProducts = products.Count;
            var totalStock = products.Sum(p => p.Stock);
            var totalSold = products.Sum(p => p.SoldCount);
            // Ciro/maliyet, satış anında biriktirilen TotalRevenue/TotalCost üzerinden hesaplanır;
            // ürünün GÜNCEL fiyatı üzerinden değil, aksi halde fiyat güncellemesi geçmiş satışların
            // kârını da geriye dönük değiştirirdi.
            var totalRevenue = products.Sum(p => p.TotalRevenue);
            var totalCost = products.Sum(p => p.TotalCost);
            var totalProfit = totalRevenue - totalCost;

            var breakdown = products.Select(p => new
            {
                p.Id,
                p.Name,
                p.Category,
                p.Image,
                p.Price,
                p.CostPrice,
                p.Stock,
                p.SoldCount,
                p.Rating,
                Revenue = p.TotalRevenue,
                Cost = p.TotalCost,
                Profit = p.TotalRevenue - p.TotalCost,
            });

            return Ok(new
            {
                totalProducts,
                totalStock,
                totalSold,
                totalRevenue,
                totalCost,
                totalProfit,
                products = breakdown,
            });
        }

    }
}
