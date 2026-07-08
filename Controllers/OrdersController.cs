using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrendMarketServer.Data;
using TrendMarketServer.Models;

namespace TrendMarketServer.Controllers
{
    [ApiController]
    [Route("api/orders")]
    [Authorize(Roles = "Customer")]
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _db;

        // Satın alınan her ürünün fiyatının %10'u kadar kupon, sipariş onaylandığında
        // müşterinin kupon cüzdanına yüklenir (ör. 100 TL'lik ürün -> 10 TL kupon).
        private const decimal CouponEarnRate = 0.10m;

        public OrdersController(AppDbContext db)
        {
            _db = db;
        }

        public class CheckoutDto
        {
            public int AddressId { get; set; }
            public string CardHolderName { get; set; } = string.Empty;
            public string CardNumber { get; set; } = string.Empty;
            public string CardExpiry { get; set; } = string.Empty; // "AA/YY"
            public string CardCvv { get; set; } = string.Empty;
            public decimal CouponAmountToUse { get; set; } = 0;
        }

        public class ConfirmCheckoutDto
        {
            public string Code { get; set; } = string.Empty;
        }

        public class UpdateAddressDto
        {
            public int AddressId { get; set; }
        }

        private int CurrentCustomerId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private async Task<decimal> GetCouponBalanceAsync(int customerId)
        {
            // SQLite depolar decimal'i TEXT olarak tuttuğu için EF Core SUM'u sunucu tarafında
            // çeviremiyor (NotSupportedException) — satırları çekip istemci tarafında topluyoruz.
            var amounts = await _db.CouponTransactions
                .Where(t => t.CustomerId == customerId)
                .Select(t => t.Amount)
                .ToListAsync();
            return amounts.Sum();
        }

        // 1a. Kart Bilgilerini Doğrula ve Sipariş Onayı İçin Doğrulama Kodu Gönder
        [HttpPost("checkout/request-code")]
        public async Task<IActionResult> RequestCheckoutCode([FromBody] CheckoutDto dto)
        {
            if (CartStore.Items.Count == 0)
                return BadRequest(new { success = false, message = "Sepetiniz boş." });

            var cardError = ValidateCard(dto);
            if (cardError != null)
                return BadRequest(new { success = false, message = cardError });

            var address = await _db.Addresses.FindAsync(dto.AddressId);
            if (address == null || address.CustomerId != CurrentCustomerId)
                return BadRequest(new { success = false, message = "Geçerli bir teslimat adresi seçin." });

            var cartTotal = CartStore.Items.Values.Sum(i => i.Product.Price * i.Quantity);
            var balance = await GetCouponBalanceAsync(CurrentCustomerId);
            if (dto.CouponAmountToUse < 0 || dto.CouponAmountToUse > balance || dto.CouponAmountToUse > cartTotal)
                return BadRequest(new { success = false, message = "Geçersiz kupon tutarı." });

            var code = VerificationStore.GenerateCode();
            VerificationStore.Entries[$"checkout:{CurrentCustomerId}"] = new VerificationEntry
            {
                Code = code,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                Payload = dto,
            };

            // Demo modu: gerçek SMS servisi bağlı değil, kart doğrulama kodu response içinde dönülüyor.
            return Ok(new { success = true, message = "Kart doğrulama kodu telefonunuza gönderildi.", demoCode = code });
        }

        // 1b. Doğrulama Kodunu Onayla ve Siparişi Oluştur (Sepeti Ödeme ile Siparişe Dönüştür — SİMÜLE ÖDEME)
        [HttpPost("checkout/confirm")]
        public async Task<IActionResult> ConfirmCheckout([FromBody] ConfirmCheckoutDto confirmDto)
        {
            var key = $"checkout:{CurrentCustomerId}";
            if (!VerificationStore.Entries.TryGetValue(key, out var entry) || entry.ExpiresAt < DateTime.UtcNow || entry.Code != confirmDto.Code.Trim())
                return BadRequest(new { success = false, message = "Doğrulama kodu geçersiz veya süresi dolmuş." });

            var dto = (CheckoutDto)entry.Payload!;

            if (CartStore.Items.Count == 0)
                return BadRequest(new { success = false, message = "Sepetiniz boş." });

            var address = await _db.Addresses.FindAsync(dto.AddressId);
            if (address == null || address.CustomerId != CurrentCustomerId)
                return BadRequest(new { success = false, message = "Geçerli bir teslimat adresi seçin." });

            var productIds = CartStore.Items.Keys.ToList();
            var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

            foreach (var id in productIds)
            {
                var cartItem = CartStore.Items[id];
                if (!products.TryGetValue(id, out var product) || product.Stock < cartItem.Quantity)
                {
                    var name = products.TryGetValue(id, out var p2) ? p2.Name : cartItem.Product.Name;
                    return BadRequest(new { success = false, message = $"{name} için yeterli stok yok." });
                }
            }

            var cartTotal = CartStore.Items.Values.Sum(i => i.Product.Price * i.Quantity);
            var balance = await GetCouponBalanceAsync(CurrentCustomerId);
            var couponToUse = Math.Max(0, Math.Min(Math.Min(dto.CouponAmountToUse, balance), cartTotal));

            var order = new Order
            {
                CustomerId = CurrentCustomerId,
                TotalAmount = cartTotal - couponToUse,
                CouponUsed = couponToUse,
                ShippingFullName = address.FullName,
                ShippingPhone = address.Phone,
                ShippingCity = address.City,
                ShippingDistrict = address.District,
                ShippingAddressText = address.AddressText,
            };
            _db.Orders.Add(order);
            await _db.SaveChangesAsync(); // Id almak için önce siparişi kaydet

            if (couponToUse > 0)
            {
                _db.CouponTransactions.Add(new CouponTransaction
                {
                    CustomerId = CurrentCustomerId,
                    Amount = -couponToUse,
                    OrderId = order.Id,
                    Description = $"Sipariş #{order.Id} için kullanılan kupon",
                });
            }

            // Sepeti satıcıya göre grupla — karışık sepette her satıcı sadece kendi gönderisini yönetir.
            var groupedBySeller = CartStore.Items.Values.GroupBy(i => i.Product.SellerId);

            foreach (var group in groupedBySeller)
            {
                var shipment = new Shipment
                {
                    OrderId = order.Id,
                    SellerId = group.Key,
                    Status = ShipmentStatus.Preparing,
                };
                _db.Shipments.Add(shipment);
                await _db.SaveChangesAsync(); // Id almak için

                foreach (var cartItem in group)
                {
                    var product = products[cartItem.Product.Id];

                    _db.OrderItems.Add(new OrderItem
                    {
                        OrderId = order.Id,
                        ShipmentId = shipment.Id,
                        ProductId = product.Id,
                        ProductName = product.Name,
                        ProductImage = product.Image,
                        Quantity = cartItem.Quantity,
                        UnitPrice = product.Price,
                        SellerId = product.SellerId,
                    });

                    product.Stock -= cartItem.Quantity;
                    product.SoldCount += cartItem.Quantity;
                    product.TotalRevenue += product.Price * cartItem.Quantity;
                    product.TotalCost += product.CostPrice * cartItem.Quantity;

                    var reward = Math.Round(product.Price * cartItem.Quantity * CouponEarnRate, 2);
                    if (reward > 0)
                    {
                        _db.CouponTransactions.Add(new CouponTransaction
                        {
                            CustomerId = CurrentCustomerId,
                            Amount = reward,
                            OrderId = order.Id,
                            Description = $"{product.Name} alışverişinden kazanılan kupon",
                        });
                    }
                }
            }

            CartStore.Items.Clear();
            await _db.SaveChangesAsync();
            VerificationStore.Entries.Remove(key);

            return Ok(new { success = true, orderId = order.Id });
        }

        // 2. Siparişlerim
        [HttpGet("my")]
        public async Task<IActionResult> GetMyOrders()
        {
            var orders = await _db.Orders
                .Where(o => o.CustomerId == CurrentCustomerId)
                .OrderByDescending(o => o.Id)
                .ToListAsync();

            var orderIds = orders.Select(o => o.Id).ToList();
            var shipments = await _db.Shipments.Where(s => orderIds.Contains(s.OrderId)).ToListAsync();
            var shipmentIds = shipments.Select(s => s.Id).ToList();
            var items = await _db.OrderItems.Where(i => shipmentIds.Contains(i.ShipmentId)).ToListAsync();

            var result = orders.Select(o => new
            {
                o.Id,
                o.CreatedAt,
                o.TotalAmount,
                o.CouponUsed,
                o.ShippingFullName,
                o.ShippingPhone,
                o.ShippingCity,
                o.ShippingDistrict,
                o.ShippingAddressText,
                CanChangeAddress = shipments.Where(s => s.OrderId == o.Id).All(s => s.Status == ShipmentStatus.Preparing),
                Shipments = shipments.Where(s => s.OrderId == o.Id).Select(s => new
                {
                    s.Id,
                    s.SellerId,
                    s.Status,
                    s.CourierName,
                    s.CourierPhone,
                    s.EstimatedDeliveryDate,
                    s.TrackingNumber,
                    Items = items.Where(i => i.ShipmentId == s.Id).Select(i => new
                    {
                        i.ProductId,
                        i.ProductName,
                        i.ProductImage,
                        i.Quantity,
                        i.UnitPrice,
                    }),
                }),
            });

            return Ok(result);
        }

        // 3. Sipariş Adresini Sonradan Değiştirme (henüz kargoya verilmediyse)
        [HttpPut("{orderId}/address")]
        public async Task<IActionResult> UpdateOrderAddress(int orderId, [FromBody] UpdateAddressDto dto)
        {
            var order = await _db.Orders.FindAsync(orderId);
            if (order == null || order.CustomerId != CurrentCustomerId)
                return NotFound(new { success = false, message = "Sipariş bulunamadı." });

            var shipments = await _db.Shipments.Where(s => s.OrderId == orderId).ToListAsync();
            if (shipments.Any(s => s.Status != ShipmentStatus.Preparing))
                return BadRequest(new { success = false, message = "Kargoya verilmiş bir siparişin adresi değiştirilemez." });

            var address = await _db.Addresses.FindAsync(dto.AddressId);
            if (address == null || address.CustomerId != CurrentCustomerId)
                return BadRequest(new { success = false, message = "Geçerli bir adres seçin." });

            order.ShippingFullName = address.FullName;
            order.ShippingPhone = address.Phone;
            order.ShippingCity = address.City;
            order.ShippingDistrict = address.District;
            order.ShippingAddressText = address.AddressText;

            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        private static string? ValidateCard(CheckoutDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.CardHolderName))
                return "Kart sahibinin adı zorunludur.";

            var digitsOnly = Regex.Replace(dto.CardNumber ?? "", @"\s+", "");
            if (!Regex.IsMatch(digitsOnly, @"^\d{13,19}$"))
                return "Kart numarası geçersiz.";

            var expiryMatch = Regex.Match(dto.CardExpiry ?? "", @"^(\d{2})/(\d{2})$");
            if (!expiryMatch.Success)
                return "Son kullanma tarihi AA/YY formatında olmalıdır.";

            var month = int.Parse(expiryMatch.Groups[1].Value);
            var year = 2000 + int.Parse(expiryMatch.Groups[2].Value);
            if (month < 1 || month > 12)
                return "Son kullanma ayı geçersiz.";

            var expiry = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);
            if (expiry < DateTime.UtcNow.Date)
                return "Kartın son kullanma tarihi geçmiş.";

            if (!Regex.IsMatch(dto.CardCvv ?? "", @"^\d{3,4}$"))
                return "CVV geçersiz.";

            return null;
        }
    }
}
