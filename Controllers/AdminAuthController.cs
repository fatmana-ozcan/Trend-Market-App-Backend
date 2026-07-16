using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrendMarketServer.Data;
using TrendMarketServer.Services;

namespace TrendMarketServer.Controllers
{
    [ApiController]
    [Route("api/admin-auth")]
    public class AdminAuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly TokenService _tokenService;

        public AdminAuthController(AppDbContext db, TokenService tokenService)
        {
            _db = db;
            _tokenService = tokenService;
        }

        public class LoginDto
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        // Admin hesabı kayıt ekranı yok — tek admin hesabı appsettings'teki AdminAccount
        // bölümünden (bkz. DbSeeder.EnsureAdminAccount) kurulur.
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            var admin = await _db.Admins.FirstOrDefaultAsync(a => a.Email == normalizedEmail);
            if (admin == null || !BCrypt.Net.BCrypt.Verify(dto.Password, admin.PasswordHash))
                return Unauthorized(new { success = false, message = "E-posta veya şifre hatalı." });

            var token = _tokenService.GenerateToken(admin);
            return Ok(new { success = true, token, adminId = admin.Id, email = admin.Email });
        }
    }
}
