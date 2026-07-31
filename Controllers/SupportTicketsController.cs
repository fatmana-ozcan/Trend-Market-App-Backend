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
    public class SupportTicketsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SupportTicketsController(AppDbContext context)
        {
            _context = context;
        }

        public class CreateTicketRequest
        {
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public TicketCategory Category { get; set; }
        }

        // GET: api/supporttickets (Tüm talepleri veya filtreye göre getirir — sadece admin)
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll([FromQuery] TicketStatus? status, [FromQuery] TicketCategory? category)
        {
            var query = _context.SupportTickets.AsQueryable();

            if (status.HasValue)
                query = query.Where(t => t.Status == status.Value);

            if (category.HasValue)
                query = query.Where(t => t.Category == category.Value);

            var list = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
            return Ok(list);
        }

        // GET: api/supporttickets/mine (Giriş yapan müşterinin kendi talepleri)
        [HttpGet("mine")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetMine()
        {
            var list = await _context.SupportTickets
                .Where(t => t.CustomerId == CurrentCustomerId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
            return Ok(list);
        }

        // POST: api/supporttickets (Giriş yapan müşteri için yeni talep/şikayet oluşturur)
        [HttpPost]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Create([FromBody] CreateTicketRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
                return BadRequest("Başlık ve açıklama zorunludur.");

            var ticket = new SupportTicket
            {
                CustomerId = CurrentCustomerId,
                UserName = User.FindFirstValue("name") ?? "",
                Title = request.Title.Trim(),
                Description = request.Description.Trim(),
                Category = request.Category,
            };
            _context.SupportTickets.Add(ticket);
            await _context.SaveChangesAsync();
            return Ok(ticket);
        }

        // PUT: api/supporttickets/5/status (Talep durumunu günceller — sadece admin)
        [HttpPut("{id}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] TicketStatus newStatus)
        {
            var ticket = await _context.SupportTickets.FindAsync(id);
            if (ticket == null) return NotFound("Talep bulunamadı.");

            ticket.Status = newStatus;
            await _context.SaveChangesAsync();
            return Ok(ticket);
        }

        private int CurrentCustomerId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}