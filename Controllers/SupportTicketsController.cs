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

        // GET: api/supporttickets (Tüm talepleri veya filtreye göre getirir)
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] TicketStatus? status, [FromQuery] TicketCategory? category)
        {
            var query = _context.SupportTickets.AsQueryable();

            if (status.HasValue)
                query = query.Where(t => t.Status == status.Value);

            if (category.HasValue)
                query = query.Where(t => t.Category == category.Value);

            var list = await query.ToListAsync();
            return Ok(list);
        }

        // POST: api/supporttickets (Yeni talep/şikayet oluşturur)
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SupportTicket ticket)
        {
            _context.SupportTickets.Add(ticket);
            await _context.SaveChangesAsync();
            return Ok(ticket);
        }

        // PUT: api/supporttickets/5/status (Talep durumunu günceller)
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] TicketStatus newStatus)
        {
            var ticket = await _context.SupportTickets.FindAsync(id);
            if (ticket == null) return NotFound("Talep bulunamadı.");

            ticket.Status = newStatus;
            await _context.SaveChangesAsync();
            return Ok(ticket);
        }
    }
}