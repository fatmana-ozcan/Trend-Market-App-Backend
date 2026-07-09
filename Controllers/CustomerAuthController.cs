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
    [Route("api/customer-auth")]
    public class CustomerAuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly TokenService _tokenService;

        public CustomerAuthController(AppDbContext db, TokenService tokenService)
        {
            _db = db;
            _tokenService = tokenService;
        }

        private int CurrentCustomerId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Profil ekranı için hesap bilgileri (login/register yanıtı sadece isim döner, e-posta/telefon içermez)
        [HttpGet("me")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetMe()
        {
            var customer = await _db.Customers.FindAsync(CurrentCustomerId);
            if (customer == null) return NotFound();
            return Ok(new { name = customer.Name, email = customer.Email, phone = customer.Phone });
        }

        public class RegisterDto
        {
            public string Name { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class LoginDto
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class ForgotPasswordDto
        {
            public string Phone { get; set; } = string.Empty;
        }

        public class ResetPasswordDto
        {
            public string Phone { get; set; } = string.Empty;
            public string Code { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Phone) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { success = false, message = "Ad, e-posta, telefon ve şifre zorunludur." });

            if (dto.Password.Length < 6)
                return BadRequest(new { success = false, message = "Şifre en az 6 karakter olmalıdır." });

            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            var exists = await _db.Customers.AnyAsync(c => c.Email == normalizedEmail);
            if (exists)
                return BadRequest(new { success = false, message = "Bu e-posta ile kayıtlı bir hesap zaten var." });

            var customer = new Customer
            {
                Name = dto.Name.Trim(),
                Email = normalizedEmail,
                Phone = dto.Phone.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            };

            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();

            var token = _tokenService.GenerateToken(customer);
            return Ok(new { success = true, token, customerId = customer.Id, name = customer.Name });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == normalizedEmail);
            if (customer == null || !BCrypt.Net.BCrypt.Verify(dto.Password, customer.PasswordHash))
                return Unauthorized(new { success = false, message = "E-posta veya şifre hatalı." });

            var token = _tokenService.GenerateToken(customer);
            return Ok(new { success = true, token, customerId = customer.Id, name = customer.Name });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var phone = dto.Phone.Trim();
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Phone == phone);
            if (customer == null)
                return BadRequest(new { success = false, message = "Bu telefon numarasıyla kayıtlı bir hesap bulunamadı." });

            var code = VerificationStore.GenerateCode();
            VerificationStore.Entries[$"pwreset:customer:{phone}"] = new VerificationEntry
            {
                Code = code,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            };

            // Demo modu: gerçek SMS servisi bağlı değil, kod response içinde dönülüyor.
            return Ok(new { success = true, message = "Doğrulama kodu telefonunuza gönderildi.", demoCode = code });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            if (dto.NewPassword.Length < 6)
                return BadRequest(new { success = false, message = "Şifre en az 6 karakter olmalıdır." });

            var phone = dto.Phone.Trim();
            var key = $"pwreset:customer:{phone}";
            if (!VerificationStore.Entries.TryGetValue(key, out var entry) || entry.ExpiresAt < DateTime.UtcNow || entry.Code != dto.Code.Trim())
                return BadRequest(new { success = false, message = "Doğrulama kodu geçersiz veya süresi dolmuş." });

            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Phone == phone);
            if (customer == null)
                return BadRequest(new { success = false, message = "Hesap bulunamadı." });

            customer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await _db.SaveChangesAsync();
            VerificationStore.Entries.Remove(key);

            return Ok(new { success = true, message = "Şifreniz güncellendi." });
        }
    }
}
