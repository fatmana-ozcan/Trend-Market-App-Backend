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

        public class SendMessageDto
        {
            public string Text { get; set; } = string.Empty;
        }

        private int CurrentCustomerId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private string CartSessionId =>
            Request.Headers.TryGetValue("X-Cart-Session", out var values) && !string.IsNullOrWhiteSpace(values.ToString())
                ? values.ToString()
                : "anonymous";

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
            var sessionId = CartSessionId;
            var cartEntries = await _db.CartEntries.Where(c => c.SessionId == sessionId).ToListAsync();
            if (cartEntries.Count == 0)
                return BadRequest(new { success = false, message = "Sepetiniz boş." });

            var cardError = ValidateCard(dto);
            if (cardError != null)
                return BadRequest(new { success = false, message = cardError });

            var address = await _db.Addresses.FindAsync(dto.AddressId);
            if (address == null || address.CustomerId != CurrentCustomerId)
                return BadRequest(new { success = false, message = "Geçerli bir teslimat adresi seçin." });

            var cartProductIds = cartEntries.Select(c => c.ProductId).ToList();
            var cartProducts = await _db.Products.Where(p => cartProductIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
            var cartTotal = cartEntries
                .Where(c => cartProducts.ContainsKey(c.ProductId))
                .Sum(c => cartProducts[c.ProductId].Price * c.Quantity);
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

            var sessionId = CartSessionId;
            var cartEntries = await _db.CartEntries.Where(c => c.SessionId == sessionId).ToListAsync();
            if (cartEntries.Count == 0)
                return BadRequest(new { success = false, message = "Sepetiniz boş." });

            var address = await _db.Addresses.FindAsync(dto.AddressId);
            if (address == null || address.CustomerId != CurrentCustomerId)
                return BadRequest(new { success = false, message = "Geçerli bir teslimat adresi seçin." });

            var productIds = cartEntries.Select(c => c.ProductId).ToList();
            var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

            foreach (var cartEntry in cartEntries)
            {
                if (!products.TryGetValue(cartEntry.ProductId, out var product) || product.Stock < cartEntry.Quantity)
                {
                    var name = products.TryGetValue(cartEntry.ProductId, out var p2) ? p2.Name : $"#{cartEntry.ProductId}";
                    return BadRequest(new { success = false, message = $"{name} için yeterli stok yok." });
                }
            }

            var cartTotal = cartEntries.Sum(c => products[c.ProductId].Price * c.Quantity);
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
            var groupedBySeller = cartEntries.GroupBy(c => products[c.ProductId].SellerId);

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

                foreach (var cartEntry in group)
                {
                    var product = products[cartEntry.ProductId];

                    _db.OrderItems.Add(new OrderItem
                    {
                        OrderId = order.Id,
                        ShipmentId = shipment.Id,
                        ProductId = product.Id,
                        ProductName = product.Name,
                        ProductImage = product.Image,
                        Quantity = cartEntry.Quantity,
                        UnitPrice = product.Price,
                        SellerId = product.SellerId,
                    });

                    product.Stock -= cartEntry.Quantity;
                    product.SoldCount += cartEntry.Quantity;
                    product.TotalRevenue += product.Price * cartEntry.Quantity;
                    product.TotalCost += product.CostPrice * cartEntry.Quantity;

                    var reward = Math.Round(product.Price * cartEntry.Quantity * CouponEarnRate, 2);
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

            _db.CartEntries.RemoveRange(cartEntries);
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
                    s.CarrierCode,
                    s.CourierName,
                    s.CourierPhone,
                    s.CourierEmail,
                    s.EstimatedDeliveryDate,
                    s.TrackingNumber,
                    TrackingUrl = Carriers.BuildTrackingUrl(s.CarrierCode, s.TrackingNumber),
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

        // 4. Bu Gönderiye Ait Mesajları Listele (müşteri tarafı — kargo hakkında satıcıyla yazışma)
        [HttpGet("shipments/{shipmentId}/messages")]
        public async Task<IActionResult> GetShipmentMessages(int shipmentId)
        {
            var shipment = await _db.Shipments.FindAsync(shipmentId);
            if (shipment == null) return NotFound(new { success = false, message = "Gönderi bulunamadı." });

            var order = await _db.Orders.FindAsync(shipment.OrderId);
            if (order == null || order.CustomerId != CurrentCustomerId) return Forbid();

            var messages = await _db.ShipmentMessages
                .Where(m => m.ShipmentId == shipmentId)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            return Ok(messages);
        }

        // 5. Bu Gönderiye Mesaj Gönder (müşteri tarafı)
        [HttpPost("shipments/{shipmentId}/messages")]
        public async Task<IActionResult> SendShipmentMessage(int shipmentId, [FromBody] SendMessageDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Text))
                return BadRequest(new { success = false, message = "Mesaj metni zorunludur." });

            var shipment = await _db.Shipments.FindAsync(shipmentId);
            if (shipment == null) return NotFound(new { success = false, message = "Gönderi bulunamadı." });

            var order = await _db.Orders.FindAsync(shipment.OrderId);
            if (order == null || order.CustomerId != CurrentCustomerId) return Forbid();

            var message = new ShipmentMessage
            {
                ShipmentId = shipmentId,
                SenderRole = ShipmentMessageSender.Customer,
                Text = dto.Text.Trim(),
            };
            _db.ShipmentMessages.Add(message);
            await _db.SaveChangesAsync();

            return Ok(new { success = true, message });
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
