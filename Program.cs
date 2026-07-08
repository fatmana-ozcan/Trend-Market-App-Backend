using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TrendMarketServer.Data;
using TrendMarketServer.Services;

// Sunucunun işletim sistemi Türkçe yerel ayarında (tr-TR) çalışıyor; bu ayarda ondalık
// ayıracı "," ve "." binlik ayıracıdır. Frontend'den gelen sayılar ise hep "." ondalık
// ayıracıyla (ör. "99.99") geliyor. Bu yüzden tüm sayı ayrıştırmasını sabit (invariant)
// kültüre zorluyoruz; aksi halde ör. "99.99" sunucuda "9999" olarak okunur.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

// Tarayıcıdan erişim için CORS politikasını ekliyoruz
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<TokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Veritabanını oluştur ve ilk verileri yükle
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DbSeeder.EnsureBaselineMigrationMarked(db);
    db.Database.Migrate();
    DbSeeder.Seed(db);
    DbSeeder.SeedPriceHistoryIfMissing(db);
}

// CORS politikasını aktif ediyoruz
app.UseCors("AllowAll");

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
