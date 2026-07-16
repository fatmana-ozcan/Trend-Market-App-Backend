using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TrendMarketServer.Models;

namespace TrendMarketServer.Services;

public class TokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateToken(Seller seller)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, seller.Id.ToString()),
            new Claim(ClaimTypes.Email, seller.Email),
            new Claim(ClaimTypes.Role, "Seller"),
            new Claim("storeName", seller.StoreName),
            new Claim("sv", seller.SessionVersion.ToString()),
        };
        return BuildToken(claims);
    }

    public string GenerateToken(Admin admin)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
            new Claim(ClaimTypes.Email, admin.Email),
            new Claim(ClaimTypes.Role, "Admin"),
        };
        return BuildToken(claims);
    }

    public string GenerateToken(Customer customer)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, customer.Id.ToString()),
            new Claim(ClaimTypes.Email, customer.Email),
            new Claim(ClaimTypes.Role, "Customer"),
            new Claim("name", customer.Name),
            new Claim("sv", customer.SessionVersion.ToString()),
        };
        return BuildToken(claims);
    }

    private string BuildToken(Claim[] claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
