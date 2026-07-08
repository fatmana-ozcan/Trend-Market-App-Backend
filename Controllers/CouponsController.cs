using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrendMarketServer.Data;

namespace TrendMarketServer.Controllers
{
    [ApiController]
    [Route("api/coupons")]
    [Authorize(Roles = "Customer")]
    public class CouponsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public CouponsController(AppDbContext db)
        {
            _db = db;
        }

        private int CurrentCustomerId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet("balance")]
        public async Task<IActionResult> GetBalance()
        {
            // SQLite depolar decimal'i TEXT olarak tuttuğu için EF Core SUM'u sunucu tarafında
            // çeviremiyor (NotSupportedException) — satırları çekip istemci tarafında topluyoruz.
            var amounts = await _db.CouponTransactions
                .Where(t => t.CustomerId == CurrentCustomerId)
                .Select(t => t.Amount)
                .ToListAsync();
            return Ok(new { balance = amounts.Sum() });
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            var history = await _db.CouponTransactions
                .Where(t => t.CustomerId == CurrentCustomerId)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new
                {
                    t.Id,
                    t.Amount,
                    t.OrderId,
                    t.Description,
                    t.CreatedAt,
                })
                .ToListAsync();
            return Ok(history);
        }
    }
}
