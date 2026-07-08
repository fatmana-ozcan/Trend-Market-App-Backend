using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrendMarketServer.Data;
using TrendMarketServer.Models;

namespace TrendMarketServer.Controllers
{
    [ApiController]
    [Route("api/seller")]
    [Authorize(Roles = "Seller")]
    public class SellerProductsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public SellerProductsController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public class ProductFormDto
        {
            public string Name { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public decimal CostPrice { get; set; }
            public int Stock { get; set; }
            public IFormFile? Image { get; set; }
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
                imageUrl = await SaveImageAsync(dto.Image);
            }

            var product = new Product
            {
                SellerId = CurrentSellerId,
                Name = dto.Name.Trim(),
                Category = dto.Category.Trim(),
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
            product.Price = dto.Price;
            product.CostPrice = dto.CostPrice;
            product.Stock = dto.Stock;

            if (dto.Image != null)
            {
                product.Image = await SaveImageAsync(dto.Image);
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

        private async Task<string> SaveImageAsync(IFormFile image)
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
    }
}
