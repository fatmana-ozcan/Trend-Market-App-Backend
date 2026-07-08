using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrendMarketServer.Data;
using TrendMarketServer.Models;

namespace TrendMarketServer.Controllers
{
    [ApiController]
    [Route("api/addresses")]
    [Authorize(Roles = "Customer")]
    public class AddressesController : ControllerBase
    {
        private readonly AppDbContext _db;

        public AddressesController(AppDbContext db)
        {
            _db = db;
        }

        public class AddressDto
        {
            public string Title { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string City { get; set; } = string.Empty;
            public string District { get; set; } = string.Empty;
            public string AddressText { get; set; } = string.Empty;
            public bool IsDefault { get; set; }
        }

        private int CurrentCustomerId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<IActionResult> GetMyAddresses()
        {
            var addresses = await _db.Addresses
                .Where(a => a.CustomerId == CurrentCustomerId)
                .OrderByDescending(a => a.Id)
                .ToListAsync();
            return Ok(addresses);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAddress([FromBody] AddressDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.Phone) ||
                string.IsNullOrWhiteSpace(dto.City) || string.IsNullOrWhiteSpace(dto.District) ||
                string.IsNullOrWhiteSpace(dto.AddressText))
            {
                return BadRequest(new { success = false, message = "Tüm adres alanları zorunludur." });
            }

            if (dto.IsDefault)
            {
                await ClearDefaultFlag();
            }

            var address = new Address
            {
                CustomerId = CurrentCustomerId,
                Title = string.IsNullOrWhiteSpace(dto.Title) ? "Adresim" : dto.Title.Trim(),
                FullName = dto.FullName.Trim(),
                Phone = dto.Phone.Trim(),
                City = dto.City.Trim(),
                District = dto.District.Trim(),
                AddressText = dto.AddressText.Trim(),
                IsDefault = dto.IsDefault,
            };

            _db.Addresses.Add(address);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, address });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAddress(int id, [FromBody] AddressDto dto)
        {
            var address = await _db.Addresses.FindAsync(id);
            if (address == null) return NotFound(new { success = false, message = "Adres bulunamadı." });
            if (address.CustomerId != CurrentCustomerId) return Forbid();

            if (dto.IsDefault && !address.IsDefault)
            {
                await ClearDefaultFlag();
            }

            address.Title = string.IsNullOrWhiteSpace(dto.Title) ? "Adresim" : dto.Title.Trim();
            address.FullName = dto.FullName.Trim();
            address.Phone = dto.Phone.Trim();
            address.City = dto.City.Trim();
            address.District = dto.District.Trim();
            address.AddressText = dto.AddressText.Trim();
            address.IsDefault = dto.IsDefault;

            await _db.SaveChangesAsync();
            return Ok(new { success = true, address });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAddress(int id)
        {
            var address = await _db.Addresses.FindAsync(id);
            if (address == null) return NotFound(new { success = false, message = "Adres bulunamadı." });
            if (address.CustomerId != CurrentCustomerId) return Forbid();

            _db.Addresses.Remove(address);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        private async Task ClearDefaultFlag()
        {
            var current = await _db.Addresses
                .Where(a => a.CustomerId == CurrentCustomerId && a.IsDefault)
                .ToListAsync();
            foreach (var a in current) a.IsDefault = false;
        }
    }
}
