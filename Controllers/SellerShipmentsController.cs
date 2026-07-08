using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrendMarketServer.Data;
using TrendMarketServer.Models;

namespace TrendMarketServer.Controllers
{
    [ApiController]
    [Route("api/seller/shipments")]
    [Authorize(Roles = "Seller")]
    public class SellerShipmentsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public SellerShipmentsController(AppDbContext db)
        {
            _db = db;
        }

        public class UpdateShipmentDto
        {
            public string Status { get; set; } = string.Empty;
            public string? CourierName { get; set; }
            public string? CourierPhone { get; set; }
            public DateTime? EstimatedDeliveryDate { get; set; }
            public string? TrackingNumber { get; set; }
        }

        private static readonly string[] ValidStatuses =
        {
            ShipmentStatus.Preparing,
            ShipmentStatus.Shipped,
            ShipmentStatus.InTransit,
            ShipmentStatus.Delivered,
        };

        private int CurrentSellerId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // 1. Bu satıcıya ait gönderileri listele
        [HttpGet]
        public async Task<IActionResult> GetMyShipments()
        {
            var shipments = await _db.Shipments
                .Where(s => s.SellerId == CurrentSellerId)
                .OrderByDescending(s => s.Id)
                .ToListAsync();

            var shipmentIds = shipments.Select(s => s.Id).ToList();
            var orderIds = shipments.Select(s => s.OrderId).Distinct().ToList();

            var orders = await _db.Orders.Where(o => orderIds.Contains(o.Id)).ToListAsync();
            var items = await _db.OrderItems.Where(i => shipmentIds.Contains(i.ShipmentId)).ToListAsync();

            var result = shipments.Select(s =>
            {
                var order = orders.First(o => o.Id == s.OrderId);
                return new
                {
                    s.Id,
                    s.OrderId,
                    s.Status,
                    s.CourierName,
                    s.CourierPhone,
                    s.EstimatedDeliveryDate,
                    s.TrackingNumber,
                    order.ShippingFullName,
                    order.ShippingPhone,
                    order.ShippingCity,
                    order.ShippingDistrict,
                    order.ShippingAddressText,
                    Items = items.Where(i => i.ShipmentId == s.Id).Select(i => new
                    {
                        i.ProductName,
                        i.ProductImage,
                        i.Quantity,
                        i.UnitPrice,
                    }),
                };
            });

            return Ok(result);
        }

        // 2. Gönderi Durumunu / Kargocu Bilgisini Güncelle
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateShipment(int id, [FromBody] UpdateShipmentDto dto)
        {
            var shipment = await _db.Shipments.FindAsync(id);
            if (shipment == null) return NotFound(new { success = false, message = "Gönderi bulunamadı." });
            if (shipment.SellerId != CurrentSellerId) return Forbid();

            if (!ValidStatuses.Contains(dto.Status))
                return BadRequest(new { success = false, message = "Geçersiz durum." });

            shipment.Status = dto.Status;
            shipment.CourierName = dto.CourierName;
            shipment.CourierPhone = dto.CourierPhone;
            shipment.EstimatedDeliveryDate = dto.EstimatedDeliveryDate;
            shipment.TrackingNumber = dto.TrackingNumber;
            shipment.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(new { success = true, shipment });
        }
    }
}
