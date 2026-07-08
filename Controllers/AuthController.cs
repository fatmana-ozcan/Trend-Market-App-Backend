using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrendMarketServer.Data;
using TrendMarketServer.Models;
using TrendMarketServer.Services;

namespace TrendMarketServer.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly TokenService _tokenService;

        public AuthController(AppDbContext db, TokenService tokenService)
        {
            _db = db;
            _tokenService = tokenService;
        }

        public class RegisterDto
        {
            public string StoreName { get; set; } = string.Empty;
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
            if (string.IsNullOrWhiteSpace(dto.StoreName) || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Phone) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { success = false, message = "Mağaza adı, e-posta, telefon ve şifre zorunludur." });

            if (dto.Password.Length < 6)
                return BadRequest(new { success = false, message = "Şifre en az 6 karakter olmalıdır." });

            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            var exists = await _db.Sellers.AnyAsync(s => s.Email == normalizedEmail);
            if (exists)
                return BadRequest(new { success = false, message = "Bu e-posta ile kayıtlı bir satıcı zaten var." });

            var seller = new Seller
            {
                StoreName = dto.StoreName.Trim(),
                Email = normalizedEmail,
                Phone = dto.Phone.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            };

            _db.Sellers.Add(seller);
            await _db.SaveChangesAsync();

            var token = _tokenService.GenerateToken(seller);
            return Ok(new { success = true, token, sellerId = seller.Id, storeName = seller.StoreName });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.Email == normalizedEmail);
            if (seller == null || !BCrypt.Net.BCrypt.Verify(dto.Password, seller.PasswordHash))
                return Unauthorized(new { success = false, message = "E-posta veya şifre hatalı." });

            var token = _tokenService.GenerateToken(seller);
            return Ok(new { success = true, token, sellerId = seller.Id, storeName = seller.StoreName });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var phone = dto.Phone.Trim();
            var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.Phone == phone);
            if (seller == null)
                return BadRequest(new { success = false, message = "Bu telefon numarasıyla kayıtlı bir satıcı bulunamadı." });

            var code = VerificationStore.GenerateCode();
            VerificationStore.Entries[$"pwreset:seller:{phone}"] = new VerificationEntry
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
            var key = $"pwreset:seller:{phone}";
            if (!VerificationStore.Entries.TryGetValue(key, out var entry) || entry.ExpiresAt < DateTime.UtcNow || entry.Code != dto.Code.Trim())
                return BadRequest(new { success = false, message = "Doğrulama kodu geçersiz veya süresi dolmuş." });

            var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.Phone == phone);
            if (seller == null)
                return BadRequest(new { success = false, message = "Hesap bulunamadı." });

            seller.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await _db.SaveChangesAsync();
            VerificationStore.Entries.Remove(key);

            return Ok(new { success = true, message = "Şifreniz güncellendi." });
        }
    }
}
