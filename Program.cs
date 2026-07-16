using System.Globalization;
using System.Security.Claims;
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

// Telefon kameralarından gelen yüksek çözünürlüklü fotoğraflar Kestrel'in varsayılan istek
// gövdesi sınırını (30 MB) aşabildiğinden, ürün/yorum görseli yüklerken 413 hatası almamak
// için bu sınırı yükseltiyoruz (küçültme/sıkıştırma zaten ImageStorageService'te yapılıyor).
builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024;
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddHttpClient();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<ImageStorageService>();

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

        // Admin panelinden "zorla çıkış yap" ile bir hesabın SessionVersion'ı artırıldığında,
        // o hesaba ait daha önce üretilmiş tüm token'lar (imzaları hâlâ geçerli olsa da) burada
        // reddedilir — JWT'ler doğası gereği stateless olduğundan gerçek bir "iptal" yalnızca
        // bu şekilde, DB'deki güncel değerle karşılaştırarak yapılabilir.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var role = context.Principal?.FindFirstValue(ClaimTypes.Role);
                var idClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (role != "Seller" && role != "Customer") return;
                if (!int.TryParse(idClaim, out var id))
                {
                    context.Fail("Geçersiz token.");
                    return;
                }

                var tokenSv = int.TryParse(context.Principal?.FindFirstValue("sv"), out var sv) ? sv : 0;
                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

                var currentSv = role == "Seller"
                    ? await db.Sellers.Where(s => s.Id == id).Select(s => (int?)s.SessionVersion).FirstOrDefaultAsync()
                    : await db.Customers.Where(c => c.Id == id).Select(c => (int?)c.SessionVersion).FirstOrDefaultAsync();

                if (currentSv == null || currentSv != tokenSv)
                {
                    context.Fail("Oturum sonlandırıldı.");
                }
            },
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
    DbSeeder.EnsureAdminAccount(db, builder.Configuration["AdminAccount:Email"]!, builder.Configuration["AdminAccount:Password"]!);
    DbSeeder.BackfillProductNameTranslations(db);
    DbSeeder.BackfillProductAttributes(db);
    DbSeeder.SeedProductVariantsIfMissing(db);
    DbSeeder.BackfillProductVariantImages(db);
    DbSeeder.SeedDemoReviewsIfMissing(db);
    DbSeeder.SeedPriceHistoryIfMissing(db);
}

// CORS politikasını aktif ediyoruz
app.UseCors("AllowAll");

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
