using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrendMarketServer.Data;
using TrendMarketServer.Models;
using TrendMarketServer.Services;

namespace TrendMarketServer.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ImageStorageService _imageStorage;

        public AdminController(AppDbContext db, ImageStorageService imageStorage)
        {
            _db = db;
            _imageStorage = imageStorage;
        }

        public class ProductFormDto
        {
            public int SellerId { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string? Brand { get; set; }
            public string? Color { get; set; }
            public string? Size { get; set; }
            public decimal Price { get; set; }
            public decimal CostPrice { get; set; }
            public int Stock { get; set; }
            public IFormFile? Image { get; set; }
            // true ise mevcut görsel silinir (yeni bir görsel gönderilmediyse ürün görselsiz kalır).
            public bool RemoveImage { get; set; }
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

        // --- SATICI ONAY YÖNETİMİ ---

        // 1. Tüm satıcıları listele (onay durumuyla birlikte)
        [HttpGet("sellers")]
        public async Task<IActionResult> GetSellers()
        {
            var sellers = await _db.Sellers
                .OrderBy(s => s.IsApproved)
                .ThenByDescending(s => s.Id)
                .Select(s => new { s.Id, s.StoreName, s.Email, s.Phone, s.IsApproved, s.CreatedAt })
                .ToListAsync();
            return Ok(sellers);
        }

        // 2. Bekleyen satıcıyı onayla
        [HttpPost("sellers/{id}/approve")]
        public async Task<IActionResult> ApproveSeller(int id)
        {
            var seller = await _db.Sellers.FindAsync(id);
            if (seller == null) return NotFound(new { success = false, message = "Satıcı bulunamadı." });

            seller.IsApproved = true;
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // 3. Satıcıyı reddet/kaldır (onaylı ya da bekleyen fark etmez — hesabı tamamen siler)
        [HttpDelete("sellers/{id}")]
        public async Task<IActionResult> RemoveSeller(int id)
        {
            var seller = await _db.Sellers.FindAsync(id);
            if (seller == null) return NotFound(new { success = false, message = "Satıcı bulunamadı." });

            var productIds = await _db.Products.Where(p => p.SellerId == id).Select(p => p.Id).ToListAsync();
            foreach (var productId in productIds)
            {
                ProductsController.RemoveFromFavorites(productId);
            }

            _db.Products.RemoveRange(_db.Products.Where(p => p.SellerId == id));
            _db.Sellers.Remove(seller);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // 3b. Satıcıyı zorla çıkış yaptır — hesabı silmeden mevcut oturumunu (token'ını) geçersiz kılar.
        [HttpPost("sellers/{id}/force-logout")]
        public async Task<IActionResult> ForceLogoutSeller(int id)
        {
            var seller = await _db.Sellers.FindAsync(id);
            if (seller == null) return NotFound(new { success = false, message = "Satıcı bulunamadı." });

            seller.SessionVersion++;
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // --- MÜŞTERİ YÖNETİMİ ---

        // Tüm müşterileri listele
        [HttpGet("customers")]
        public async Task<IActionResult> GetCustomers()
        {
            var customers = await _db.Customers
                .OrderByDescending(c => c.Id)
                .Select(c => new { c.Id, c.Name, c.Email, c.Phone, c.CreatedAt })
                .ToListAsync();
            return Ok(customers);
        }

        // Müşteriyi zorla çıkış yaptır — hesabı silmeden mevcut oturumunu (token'ını) geçersiz kılar.
        [HttpPost("customers/{id}/force-logout")]
        public async Task<IActionResult> ForceLogoutCustomer(int id)
        {
            var customer = await _db.Customers.FindAsync(id);
            if (customer == null) return NotFound(new { success = false, message = "Kullanıcı bulunamadı." });

            customer.SessionVersion++;
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // --- ÜRÜN YÖNETİMİ (tüm satıcıların ürünleri üzerinde tam yetki) ---

        // 4. Tüm ürünleri listele (mağaza adıyla birlikte)
        [HttpGet("products")]
        public async Task<IActionResult> GetProducts()
        {
            var products = await (
                from p in _db.Products
                join s in _db.Sellers on p.SellerId equals s.Id
                orderby p.Id descending
                select new
                {
                    p.Id,
                    p.SellerId,
                    SellerStoreName = s.StoreName,
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
                    p.Rating,
                }
            ).ToListAsync();

            return Ok(products);
        }

        // 5. Yeni ürün ekle (istenen satıcı adına)
        [HttpPost("products")]
        public async Task<IActionResult> CreateProduct([FromForm] ProductFormDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Category))
                return BadRequest(new { success = false, message = "Ürün adı ve kategori zorunludur." });

            var sellerExists = await _db.Sellers.AnyAsync(s => s.Id == dto.SellerId);
            if (!sellerExists) return BadRequest(new { success = false, message = "Satıcı bulunamadı." });

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
                SellerId = dto.SellerId,
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

        // 6. Herhangi bir ürünü güncelle (satıcı sahipliği kontrolü yok — admin her ürünü düzenleyebilir)
        [HttpPut("products/{id}")]
        public async Task<IActionResult> UpdateProduct(int id, [FromForm] ProductFormDto dto)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null) return NotFound(new { success = false, message = "Ürün bulunamadı." });

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
            else if (dto.RemoveImage)
            {
                product.Image = string.Empty;
            }

            await _db.SaveChangesAsync();
            return Ok(new { success = true, product });
        }

        // Kapak görseli (Product.Image) hariç, ürün başına en fazla bu kadar ek galeri görseli.
        private const int MaxExtraImages = 3;

        // 6z. Ürünün ek galeri görsellerini listele
        [HttpGet("products/{productId}/images")]
        public async Task<IActionResult> GetImages(int productId)
        {
            var product = await _db.Products.FindAsync(productId);
            if (product == null) return NotFound(new { success = false, message = "Ürün bulunamadı." });

            var images = await _db.ProductImages
                .Where(i => i.ProductId == productId)
                .OrderBy(i => i.SortOrder)
                .ToListAsync();
            return Ok(images);
        }

        // 6y. Ürüne yeni bir ek galeri görseli ekle (kapak + ek görseller toplamda 4'ü geçemez)
        [HttpPost("products/{productId}/images")]
        public async Task<IActionResult> AddImage(int productId, IFormFile image)
        {
            var product = await _db.Products.FindAsync(productId);
            if (product == null) return NotFound(new { success = false, message = "Ürün bulunamadı." });
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

        // 6x. Ürünün ek galeri görsellerinden birini kaldır
        [HttpDelete("products/{productId}/images/{imageId}")]
        public async Task<IActionResult> DeleteImage(int productId, int imageId)
        {
            var product = await _db.Products.FindAsync(productId);
            if (product == null) return NotFound(new { success = false, message = "Ürün bulunamadı." });

            var image = await _db.ProductImages.FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == productId);
            if (image == null) return NotFound(new { success = false, message = "Görsel bulunamadı." });

            _db.ProductImages.Remove(image);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // 6b. Ürünün renk/beden varyantlarını listele (düzenleme formunu doldurmak için)
        [HttpGet("products/{productId}/variants")]
        public async Task<IActionResult> GetVariants(int productId)
        {
            var product = await _db.Products.FindAsync(productId);
            if (product == null) return NotFound(new { success = false, message = "Ürün bulunamadı." });

            var variants = await _db.ProductVariants
                .Where(v => v.ProductId == productId)
                .ToListAsync();
            return Ok(variants);
        }

        // 6c. Ürünün renk/beden varyantlarını topluca güncelle (bkz. SellerProductsController.SetVariants
        // — aynı mantık, sadece sahiplik kontrolü yok çünkü admin her ürünü düzenleyebilir).
        [HttpPut("products/{productId}/variants")]
        public async Task<IActionResult> SetVariants(int productId, [FromBody] List<ProductVariantDto> variants)
        {
            var product = await _db.Products.FindAsync(productId);
            if (product == null) return NotFound(new { success = false, message = "Ürün bulunamadı." });

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

        // 7. Ürünün sadece görselini kaldır (ürünün geri kalanına dokunmadan)
        [HttpDelete("products/{id}/image")]
        public async Task<IActionResult> DeleteProductImage(int id)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null) return NotFound(new { success = false, message = "Ürün bulunamadı." });

            product.Image = string.Empty;
            await _db.SaveChangesAsync();
            return Ok(new { success = true, product });
        }

        // 8. Herhangi bir ürünü sil (satıcı sahipliği kontrolü yok)
        [HttpDelete("products/{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null) return NotFound(new { success = false, message = "Ürün bulunamadı." });

            _db.Products.Remove(product);
            await _db.SaveChangesAsync();
            ProductsController.RemoveFromFavorites(id);
            return Ok(new { success = true });
        }

    }
}
