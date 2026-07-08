using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TrendMarketServer.Models;

namespace TrendMarketServer.Data;

public static class DbSeeder
{
    // The project has adopted EF Core Migrations (see Migrations/), but the live database
    // already had Sellers/Products tables from before migrations existed (created via the old
    // EnsureCreated() + hand-rolled ALTER TABLE approach). The "InitialBaseline" migration
    // reflects that already-existing schema exactly, so its Up() must never actually run
    // (it would try to CREATE TABLE Products/Sellers again and fail) — instead we mark it as
    // already-applied by hand, once, so that Database.Migrate() skips straight to applying
    // only the real new migrations (e.g. AddCustomerOrdersShipments).
    private const string BaselineMigrationId = "20260707145040_InitialBaseline";

    public static void EnsureBaselineMigrationMarked(AppDbContext db)
    {
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        connection.Open();

        using (var createHistoryCmd = connection.CreateCommand())
        {
            createHistoryCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                    ""MigrationId"" TEXT NOT NULL CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY,
                    ""ProductVersion"" TEXT NOT NULL
                );";
            createHistoryCmd.ExecuteNonQuery();
        }

        // Only mark the baseline as applied if the tables it would create already exist
        // (i.e. this is a pre-migrations database) — a genuinely fresh database should let
        // Migrate() run every migration, baseline included.
        bool productsTableExists;
        using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Products';";
            productsTableExists = Convert.ToInt64(checkCmd.ExecuteScalar()) > 0;
        }

        if (!productsTableExists) return;

        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = @"
            INSERT OR IGNORE INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
            VALUES ($id, '8.0.11');";
        insertCmd.Parameters.AddWithValue("$id", BaselineMigrationId);
        insertCmd.ExecuteNonQuery();
    }

    public static void Seed(AppDbContext db)
    {
        if (db.Sellers.Any()) return;

        var demoSeller = new Seller
        {
            StoreName = "TrendMarket Demo Mağaza",
            Email = "demo@trendmarket.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("demo1234"),
        };
        db.Sellers.Add(demoSeller);
        db.SaveChanges();

        var seedProducts = new (string Name, decimal Price, string Category, string Image)[]
        {
            ("Minimalist Akıllı Saat", 3499, "TEKNOLOJİ", "https://images.unsplash.com/photo-1523275335684-37898b6baf30?w=500&auto=format&fit=crop&q=60"),
            ("Sessiz Kablosuz Kulaklık", 1999, "TEKNOLOJİ", "https://images.unsplash.com/photo-1505740420928-5e560c06d30e?w=500&auto=format&fit=crop&q=60"),
            ("Mekanik Oyuncu Klavyesi", 1450, "TEKNOLOJİ", "https://images.unsplash.com/photo-1587829741301-dc798b83add3?w=500&auto=format&fit=crop&q=60"),
            ("Kablosuz Gaming Mouse", 890, "TEKNOLOJİ", "https://images.unsplash.com/photo-1615663245857-ac93bb7c39e7?w=500&auto=format&fit=crop&q=60"),
            ("Taşınabilir Bluetooth Hoparlör", 1200, "TEKNOLOJİ", "https://images.unsplash.com/photo-1608043152269-423dbba4e7e1?w=500&auto=format&fit=crop&q=60"),
            ("Ultra HD Web Kamerası", 1750, "TEKNOLOJİ", "https://images.unsplash.com/photo-1603162591624-9199c017cb83?w=500&auto=format&fit=crop&q=60"),
            ("20000 mAh Powerbank", 650, "TEKNOLOJİ", "https://images.unsplash.com/photo-1609592424209-39080d0d3d51?w=500&auto=format&fit=crop&q=60"),
            ("RGB Oyuncu Kulaklığı", 1350, "TEKNOLOJİ", "https://images.unsplash.com/photo-1546435770-a3e426bf472b?w=500&auto=format&fit=crop&q=60"),
            ("Oversize Pamuklu Hoodie", 750, "MODA", "https://images.unsplash.com/photo-1556905055-8f358a7a47b2?w=500&auto=format&fit=crop&q=60"),
            ("Klasik Deri Ceket", 2499, "MODA", "https://images.unsplash.com/photo-1551028719-00167b16eac5?w=500&auto=format&fit=crop&q=60"),
            ("Beyaz Rahat Spor Ayakkabı", 1850, "MODA", "https://images.unsplash.com/photo-1549298916-b41d501d3772?w=500&auto=format&fit=crop&q=60"),
            ("Polarize Güneş Gözlüğü", 580, "MODA", "https://images.unsplash.com/photo-1572635196237-14b3f281503f?w=500&auto=format&fit=crop&q=60"),
            ("Su Geçirmez Sırt Çantası", 950, "MODA", "https://images.unsplash.com/photo-1553062407-98eeb64c6a62?w=500&auto=format&fit=crop&q=60"),
            ("Örme Kışlık Bere", 180, "MODA", "https://images.unsplash.com/photo-1576871337632-b9aef4c17ab9?w=500&auto=format&fit=crop&q=60"),
            ("Keten Yazlık Gömlek", 620, "MODA", "https://images.unsplash.com/photo-1596755094514-f87e34085b2c?w=500&auto=format&fit=crop&q=60"),
            ("Klasik Kol Saati", 2100, "MODA", "https://images.unsplash.com/photo-1524592094714-0f0654e20314?w=500&auto=format&fit=crop&q=60"),
            ("El Yapımı Seramik Kupa", 250, "EV-YAŞAM", "https://images.unsplash.com/photo-1514432324607-a09d9b4aefdd?w=500&auto=format&fit=crop&q=60"),
            ("Masaüstü LED Abajur", 420, "EV-YAŞAM", "https://images.unsplash.com/photo-1507473885765-e6ed057f782c?w=500&auto=format&fit=crop&q=60"),
            ("Kokulu Soya Mumu Seti", 290, "EV-YAŞAM", "https://images.unsplash.com/photo-1603006905003-be475563bc59?w=500&auto=format&fit=crop&q=60"),
            ("Minimalist Duvar Saati", 540, "EV-YAŞAM", "https://images.unsplash.com/photo-1563861826100-9cb868fdbe1c?w=500&auto=format&fit=crop&q=60"),
            ("Ortopedik Ofis Minderi", 380, "EV-YAŞAM", "https://images.unsplash.com/photo-1584100936595-c0654b55a2e2?w=500&auto=format&fit=crop&q=60"),
            ("Bitki Yetiştirme Standı", 720, "EV-YAŞAM", "https://images.unsplash.com/photo-1485955900006-10f4d324d411?w=500&auto=format&fit=crop&q=60"),
            ("French Press Bitki Çayı Demliği", 210, "EV-YAŞAM", "https://images.unsplash.com/photo-1577968897966-3d4325b36b61?w=500&auto=format&fit=crop&q=60"),
            ("Paslanmaz Çelik Termos (500ml)", 480, "EV-YAŞAM", "https://images.unsplash.com/photo-1602143407151-7111542de6e8?w=500&auto=format&fit=crop&q=60"),
        };

        foreach (var p in seedProducts)
        {
            db.Products.Add(new Product
            {
                SellerId = demoSeller.Id,
                Name = p.Name,
                Price = p.Price,
                CostPrice = Math.Round(p.Price * 0.6m, 2),
                Category = p.Category,
                Image = p.Image,
                Stock = 50,
                SoldCount = 0,
                RatingSum = 4,
                RatingCount = 1,
            });
        }

        db.SaveChanges();
    }

    // Bağımsız ve idempotent: Seed() yalnızca boş veritabanında çalışır, ama fiyat geçmişi
    // özelliğini zaten dolu olan geliştirme veritabanlarında da göstermek istiyoruz. Bu yüzden
    // her ürün için ayrı ayrı "geçmişi var mı" kontrolü yapıp eksik olanları tamamlıyoruz —
    // her uygulama başlangıcında çalışsa da var olan kayıtları asla tekrar eklemez.
    public static void SeedPriceHistoryIfMissing(AppDbContext db)
    {
        var productsWithoutHistory = db.Products
            .Where(p => !db.ProductPriceHistories.Any(h => h.ProductId == p.Id))
            .ToList();

        foreach (var product in productsWithoutHistory)
        {
            // Her ~4 üründen birinde, "bazı ürünlerde" görünmesi gereken bir fiyat indirimi
            // simüle ediyoruz: 12 gün önce daha yüksek bir fiyat, şimdi mevcut (düşük) fiyat.
            if (product.Id % 4 == 0)
            {
                db.ProductPriceHistories.Add(new ProductPriceHistory
                {
                    ProductId = product.Id,
                    Price = Math.Round(product.Price * 1.2m, 2),
                    RecordedAt = DateTime.UtcNow.AddDays(-12),
                });
            }

            db.ProductPriceHistories.Add(new ProductPriceHistory
            {
                ProductId = product.Id,
                Price = product.Price,
                RecordedAt = DateTime.UtcNow,
            });
        }

        db.SaveChanges();
    }
}
