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

    // Uygulamanın TEK admin hesabı — kimlik bilgileri appsettings(.Development).json'daki
    // "AdminAccount" bölümünden okunur, böylece kod değiştirmeden sadece config dosyasını
    // düzenleyerek e-posta/şifre güncellenebilir. Her başlangıçta çalışır ve config'teki
    // güncel değerlerle senkronize eder (idempotent: hash zaten eşleşiyorsa DB'ye dokunmaz).
    public static void EnsureAdminAccount(AppDbContext db, string email, string password)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var admin = db.Admins.FirstOrDefault();

        if (admin == null)
        {
            db.Admins.Add(new Admin
            {
                Email = normalizedEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            });
            db.SaveChanges();
            return;
        }

        var emailChanged = admin.Email != normalizedEmail;
        var passwordChanged = !BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash);
        if (emailChanged || passwordChanged)
        {
            admin.Email = normalizedEmail;
            if (passwordChanged) admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            db.SaveChanges();
        }
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

        var seedProducts = new (string Name, string NameEn, string NameDe, decimal Price, string Category, string Image, string Brand, string Color, string Size)[]
        {
            ("Minimalist Akıllı Saat", "Minimalist Smart Watch", "Minimalistische Smartwatch", 3499, "TEKNOLOJİ", "https://images.unsplash.com/photo-1523275335684-37898b6baf30?w=500&auto=format&fit=crop&q=60", "Chronotech", null, null),
            ("Sessiz Kablosuz Kulaklık", "Silent Wireless Earbuds", "Geräuschlose Kabellose Kopfhörer", 1999, "TEKNOLOJİ", "https://images.unsplash.com/photo-1505740420928-5e560c06d30e?w=500&auto=format&fit=crop&q=60", "SoundCore", null, null),
            ("Mekanik Oyuncu Klavyesi", "Mechanical Gaming Keyboard", "Mechanische Gaming-Tastatur", 1450, "TEKNOLOJİ", "https://images.unsplash.com/photo-1587829741301-dc798b83add3?w=500&auto=format&fit=crop&q=60", "KeyForge", null, null),
            ("Kablosuz Gaming Mouse", "Wireless Gaming Mouse", "Kabellose Gaming-Maus", 890, "TEKNOLOJİ", "https://images.unsplash.com/photo-1615663245857-ac93bb7c39e7?w=500&auto=format&fit=crop&q=60", "KeyForge", null, null),
            ("Taşınabilir Bluetooth Hoparlör", "Portable Bluetooth Speaker", "Tragbarer Bluetooth-Lautsprecher", 1200, "TEKNOLOJİ", "https://images.unsplash.com/photo-1608043152269-423dbba4e7e1?w=500&auto=format&fit=crop&q=60", "SoundCore", null, null),
            ("Ultra HD Web Kamerası", "Ultra HD Webcam", "Ultra-HD-Webcam", 1750, "TEKNOLOJİ", "https://images.unsplash.com/photo-1603162591624-9199c017cb83?w=500&auto=format&fit=crop&q=60", "VisionTech", null, null),
            ("20000 mAh Powerbank", "20000 mAh Power Bank", "20000-mAh-Powerbank", 650, "TEKNOLOJİ", "https://images.unsplash.com/photo-1609592424209-39080d0d3d51?w=500&auto=format&fit=crop&q=60", "VoltPack", null, null),
            ("RGB Oyuncu Kulaklığı", "RGB Gaming Headset", "RGB-Gaming-Headset", 1350, "TEKNOLOJİ", "https://images.unsplash.com/photo-1546435770-a3e426bf472b?w=500&auto=format&fit=crop&q=60", "SoundCore", null, null),
            ("Oversize Pamuklu Hoodie", "Oversized Cotton Hoodie", "Oversize-Baumwoll-Hoodie", 750, "MODA", "https://images.unsplash.com/photo-1556905055-8f358a7a47b2?w=500&auto=format&fit=crop&q=60", "UrbanWear", "Siyah", "L"),
            ("Klasik Deri Ceket", "Classic Leather Jacket", "Klassische Lederjacke", 2499, "MODA", "https://images.unsplash.com/photo-1551028719-00167b16eac5?w=500&auto=format&fit=crop&q=60", "LeatherCo", "Kahverengi", "M"),
            ("Beyaz Rahat Spor Ayakkabı", "White Casual Sneakers", "Weiße Bequeme Sneaker", 1850, "MODA", "https://images.unsplash.com/photo-1549298916-b41d501d3772?w=500&auto=format&fit=crop&q=60", "StrideFit", "Beyaz", "42"),
            ("Polarize Güneş Gözlüğü", "Polarized Sunglasses", "Polarisierte Sonnenbrille", 580, "MODA", "https://images.unsplash.com/photo-1572635196237-14b3f281503f?w=500&auto=format&fit=crop&q=60", "SunVue", "Siyah", null),
            ("Su Geçirmez Sırt Çantası", "Waterproof Backpack", "Wasserdichter Rucksack", 950, "MODA", "https://images.unsplash.com/photo-1553062407-98eeb64c6a62?w=500&auto=format&fit=crop&q=60", "TrailPack", "Lacivert", null),
            ("Örme Kışlık Bere", "Knitted Winter Beanie", "Gestrickte Wintermütze", 180, "MODA", "https://images.unsplash.com/photo-1576871337632-b9aef4c17ab9?w=500&auto=format&fit=crop&q=60", "UrbanWear", "Gri", "Standart"),
            ("Keten Yazlık Gömlek", "Linen Summer Shirt", "Leinen-Sommerhemd", 620, "MODA", "https://images.unsplash.com/photo-1596755094514-f87e34085b2c?w=500&auto=format&fit=crop&q=60", "LinenHouse", "Beyaz", "M"),
            ("Klasik Kol Saati", "Classic Wristwatch", "Klassische Armbanduhr", 2100, "MODA", "https://images.unsplash.com/photo-1524592094714-0f0654e20314?w=500&auto=format&fit=crop&q=60", "Chronotech", "Gümüş", null),
            ("El Yapımı Seramik Kupa", "Handmade Ceramic Mug", "Handgefertigte Keramiktasse", 250, "EV-YAŞAM", "https://images.unsplash.com/photo-1514432324607-a09d9b4aefdd?w=500&auto=format&fit=crop&q=60", "ClayCraft", "Beyaz", null),
            ("Masaüstü LED Abajur", "Desktop LED Lamp", "LED-Tischlampe", 420, "EV-YAŞAM", "https://images.unsplash.com/photo-1507473885765-e6ed057f782c?w=500&auto=format&fit=crop&q=60", "LumiHome", "Siyah", null),
            ("Kokulu Soya Mumu Seti", "Scented Soy Candle Set", "Duftendes Sojakerzen-Set", 290, "EV-YAŞAM", "https://images.unsplash.com/photo-1603006905003-be475563bc59?w=500&auto=format&fit=crop&q=60", "PureScent", "Bej", null),
            ("Minimalist Duvar Saati", "Minimalist Wall Clock", "Minimalistische Wanduhr", 540, "EV-YAŞAM", "https://images.unsplash.com/photo-1563861826100-9cb868fdbe1c?w=500&auto=format&fit=crop&q=60", "LumiHome", "Siyah", null),
            ("Ortopedik Ofis Minderi", "Orthopedic Office Cushion", "Orthopädisches Bürokissen", 380, "EV-YAŞAM", "https://images.unsplash.com/photo-1584100936595-c0654b55a2e2?w=500&auto=format&fit=crop&q=60", "ComfortZone", "Gri", null),
            ("Bitki Yetiştirme Standı", "Plant Growing Stand", "Pflanzenständer", 720, "EV-YAŞAM", "https://images.unsplash.com/photo-1485955900006-10f4d324d411?w=500&auto=format&fit=crop&q=60", "GreenNest", "Kahverengi", null),
            ("French Press Bitki Çayı Demliği", "French Press Herbal Tea Maker", "French-Press-Kräutertee-Bereiter", 210, "EV-YAŞAM", "https://images.unsplash.com/photo-1577968897966-3d4325b36b61?w=500&auto=format&fit=crop&q=60", "BrewCraft", "Şeffaf", null),
            ("Paslanmaz Çelik Termos (500ml)", "Stainless Steel Thermos (500ml)", "Edelstahl-Thermoskanne (500ml)", 480, "EV-YAŞAM", "https://images.unsplash.com/photo-1602143407151-7111542de6e8?w=500&auto=format&fit=crop&q=60", "ThermoPro", "Çelik", null),
        };

        foreach (var p in seedProducts)
        {
            db.Products.Add(new Product
            {
                SellerId = demoSeller.Id,
                Name = p.Name,
                NameEn = p.NameEn,
                NameDe = p.NameDe,
                Price = p.Price,
                CostPrice = Math.Round(p.Price * 0.6m, 2),
                Category = p.Category,
                Image = p.Image,
                Brand = p.Brand,
                Color = p.Color,
                Size = p.Size,
                Stock = 50,
                SoldCount = 0,
                RatingSum = 4,
                RatingCount = 1,
            });
        }

        db.SaveChanges();
    }

    // Var olan (uygulama daha önce çalıştırılmış) veritabanlarında ürünler NameEn/NameDe
    // olmadan seed edilmiş olabilir. Seed() yalnızca boş veritabanında çalıştığından, burada
    // isim eşleşmesine göre eksik çevirileri idempotent şekilde tamamlıyoruz.
    public static void BackfillProductNameTranslations(AppDbContext db)
    {
        var translations = new Dictionary<string, (string En, string De)>
        {
            ["Minimalist Akıllı Saat"] = ("Minimalist Smart Watch", "Minimalistische Smartwatch"),
            ["Minimalist Akilli Saat"] = ("Minimalist Smart Watch", "Minimalistische Smartwatch"),
            ["Sessiz Kablosuz Kulaklık"] = ("Silent Wireless Earbuds", "Geräuschlose Kabellose Kopfhörer"),
            ["Sessiz Kablosuz Kulaklik"] = ("Silent Wireless Earbuds", "Geräuschlose Kabellose Kopfhörer"),
            ["Mekanik Oyuncu Klavyesi"] = ("Mechanical Gaming Keyboard", "Mechanische Gaming-Tastatur"),
            ["Kablosuz Gaming Mouse"] = ("Wireless Gaming Mouse", "Kabellose Gaming-Maus"),
            ["Taşınabilir Bluetooth Hoparlör"] = ("Portable Bluetooth Speaker", "Tragbarer Bluetooth-Lautsprecher"),
            ["Ultra HD Web Kamerası"] = ("Ultra HD Webcam", "Ultra-HD-Webcam"),
            ["20000 mAh Powerbank"] = ("20000 mAh Power Bank", "20000-mAh-Powerbank"),
            ["RGB Oyuncu Kulaklığı"] = ("RGB Gaming Headset", "RGB-Gaming-Headset"),
            ["Oversize Pamuklu Hoodie"] = ("Oversized Cotton Hoodie", "Oversize-Baumwoll-Hoodie"),
            ["Klasik Deri Ceket"] = ("Classic Leather Jacket", "Klassische Lederjacke"),
            ["Beyaz Rahat Spor Ayakkabı"] = ("White Casual Sneakers", "Weiße Bequeme Sneaker"),
            ["Polarize Güneş Gözlüğü"] = ("Polarized Sunglasses", "Polarisierte Sonnenbrille"),
            ["Su Geçirmez Sırt Çantası"] = ("Waterproof Backpack", "Wasserdichter Rucksack"),
            ["Örme Kışlık Bere"] = ("Knitted Winter Beanie", "Gestrickte Wintermütze"),
            ["Keten Yazlık Gömlek"] = ("Linen Summer Shirt", "Leinen-Sommerhemd"),
            ["Klasik Kol Saati"] = ("Classic Wristwatch", "Klassische Armbanduhr"),
            ["El Yapımı Seramik Kupa"] = ("Handmade Ceramic Mug", "Handgefertigte Keramiktasse"),
            ["Masaüstü LED Abajur"] = ("Desktop LED Lamp", "LED-Tischlampe"),
            ["Kokulu Soya Mumu Seti"] = ("Scented Soy Candle Set", "Duftendes Sojakerzen-Set"),
            ["Minimalist Duvar Saati"] = ("Minimalist Wall Clock", "Minimalistische Wanduhr"),
            ["Ortopedik Ofis Minderi"] = ("Orthopedic Office Cushion", "Orthopädisches Bürokissen"),
            ["Bitki Yetiştirme Standı"] = ("Plant Growing Stand", "Pflanzenständer"),
            ["French Press Bitki Çayı Demliği"] = ("French Press Herbal Tea Maker", "French-Press-Kräutertee-Bereiter"),
            ["Paslanmaz Çelik Termos (500ml)"] = ("Stainless Steel Thermos (500ml)", "Edelstahl-Thermoskanne (500ml)"),
        };

        var candidates = db.Products.Where(p => p.NameEn == null || p.NameDe == null).ToList();
        var changed = false;
        foreach (var product in candidates)
        {
            if (!translations.TryGetValue(product.Name, out var names)) continue;
            product.NameEn ??= names.En;
            product.NameDe ??= names.De;
            changed = true;
        }

        if (changed) db.SaveChanges();
    }

    // Seed() yalnızca boş veritabanında çalıştığından, Brand/Color/Size alanları eklendiğinde
    // zaten seed edilmiş veritabanlarında bu alanlar boş kalır. BackfillProductNameTranslations
    // ile aynı desende, isim eşleşmesine göre eksik alanları idempotent şekilde dolduruyoruz.
    public static void BackfillProductAttributes(AppDbContext db)
    {
        var attributes = new Dictionary<string, (string? Brand, string? Color, string? Size)>
        {
            ["Minimalist Akıllı Saat"] = ("Chronotech", null, null),
            ["Sessiz Kablosuz Kulaklık"] = ("SoundCore", null, null),
            ["Mekanik Oyuncu Klavyesi"] = ("KeyForge", null, null),
            ["Kablosuz Gaming Mouse"] = ("KeyForge", null, null),
            ["Taşınabilir Bluetooth Hoparlör"] = ("SoundCore", null, null),
            ["Ultra HD Web Kamerası"] = ("VisionTech", null, null),
            ["20000 mAh Powerbank"] = ("VoltPack", null, null),
            ["RGB Oyuncu Kulaklığı"] = ("SoundCore", null, null),
            ["Oversize Pamuklu Hoodie"] = ("UrbanWear", "Siyah", "L"),
            ["Klasik Deri Ceket"] = ("LeatherCo", "Kahverengi", "M"),
            ["Beyaz Rahat Spor Ayakkabı"] = ("StrideFit", "Beyaz", "42"),
            ["Polarize Güneş Gözlüğü"] = ("SunVue", "Siyah", null),
            ["Su Geçirmez Sırt Çantası"] = ("TrailPack", "Lacivert", null),
            ["Örme Kışlık Bere"] = ("UrbanWear", "Gri", "Standart"),
            ["Keten Yazlık Gömlek"] = ("LinenHouse", "Beyaz", "M"),
            ["Klasik Kol Saati"] = ("Chronotech", "Gümüş", null),
            ["El Yapımı Seramik Kupa"] = ("ClayCraft", "Beyaz", null),
            ["Masaüstü LED Abajur"] = ("LumiHome", "Siyah", null),
            ["Kokulu Soya Mumu Seti"] = ("PureScent", "Bej", null),
            ["Minimalist Duvar Saati"] = ("LumiHome", "Siyah", null),
            ["Ortopedik Ofis Minderi"] = ("ComfortZone", "Gri", null),
            ["Bitki Yetiştirme Standı"] = ("GreenNest", "Kahverengi", null),
            ["French Press Bitki Çayı Demliği"] = ("BrewCraft", "Şeffaf", null),
            ["Paslanmaz Çelik Termos (500ml)"] = ("ThermoPro", "Çelik", null),
        };

        var candidates = db.Products.Where(p => p.Brand == null).ToList();
        var changed = false;
        foreach (var product in candidates)
        {
            if (!attributes.TryGetValue(product.Name, out var attrs)) continue;
            product.Brand = attrs.Brand;
            product.Color ??= attrs.Color;
            product.Size ??= attrs.Size;
            changed = true;
        }

        if (changed) db.SaveChanges();
    }

    // Renk ve beden, ürün detayında bağımsız seçenekler olarak gösterilir (bkz. ProductVariant).
    // Her ürün için sadece o üründe anlamlı olan eksen(ler) seed edilir — ör. güneş gözlüğünde
    // beden yoktur, bere ise tek bedendir ve beden varyantı almaz. Idempotent: bir ürünün zaten
    // varyantı varsa dokunulmaz.
    public static void SeedProductVariantsIfMissing(AppDbContext db)
    {
        // Görsel null bırakılan (ilk/varsayılan) renk seçeneği, ürünün zaten sahip olduğu ana
        // görselle aynıdır — o rengin fotoğrafı zaten Product.Image olarak mevcuttur.
        var colorVariants = new Dictionary<string, (string Color, int Stock, string? Image)[]>
        {
            ["Oversize Pamuklu Hoodie"] = new (string, int, string?)[] {
                ("Siyah", 49, null),
                ("Gri", 22, "https://images.unsplash.com/photo-1556821840-3a63f95609a7?w=500&auto=format&fit=crop&q=60"),
                ("Bordo", 0, "https://images.unsplash.com/photo-1721111259873-5a13f7fcd67b?w=500&auto=format&fit=crop&q=60"),
            },
            ["Klasik Deri Ceket"] = new (string, int, string?)[] {
                ("Kahverengi", 49, null),
                ("Siyah", 15, "https://images.unsplash.com/photo-1521223890158-f9f7c3d5d504?w=500&auto=format&fit=crop&q=60"),
                ("Lacivert", 0, "https://images.unsplash.com/photo-1592878904946-b3cd8ae243d0?w=500&auto=format&fit=crop&q=60"),
            },
            ["Beyaz Rahat Spor Ayakkabı"] = new (string, int, string?)[] {
                ("Beyaz", 49, null),
                ("Siyah", 20, "https://images.unsplash.com/photo-1574020462714-5451391cc336?w=500&auto=format&fit=crop&q=60"),
                ("Gri", 0, "https://images.unsplash.com/photo-1621665421571-2d325f9c7c6a?w=500&auto=format&fit=crop&q=60"),
            },
            ["Polarize Güneş Gözlüğü"] = new (string, int, string?)[] {
                ("Siyah", 49, null),
                ("Kahverengi", 18, "https://images.unsplash.com/photo-1567333126229-db29200c25f1?w=500&auto=format&fit=crop&q=60"),
                ("Mavi", 0, "https://images.unsplash.com/photo-1564867739458-f42235fab442?w=500&auto=format&fit=crop&q=60"),
            },
            ["Su Geçirmez Sırt Çantası"] = new (string, int, string?)[] {
                ("Lacivert", 49, null),
                ("Siyah", 25, "https://images.unsplash.com/photo-1642375352724-8b523c67b8be?w=500&auto=format&fit=crop&q=60"),
                ("Haki", 0, "https://images.unsplash.com/photo-1602845860431-35374f24f48d?w=500&auto=format&fit=crop&q=60"),
            },
            ["Örme Kışlık Bere"] = new (string, int, string?)[] {
                ("Gri", 49, null),
                ("Siyah", 12, "https://images.unsplash.com/photo-1618354691792-d1d42acfd860?w=500&auto=format&fit=crop&q=60"),
                ("Bordo", 0, "https://images.unsplash.com/photo-1767022518623-1d373c45076a?w=500&auto=format&fit=crop&q=60"),
            },
            ["Keten Yazlık Gömlek"] = new (string, int, string?)[] {
                ("Beyaz", 49, null),
                ("Mavi", 16, "https://images.unsplash.com/photo-1740711152088-88a009e877bb?w=500&auto=format&fit=crop&q=60"),
                ("Bej", 0, "https://images.unsplash.com/photo-1666358084687-14347fbf364c?w=500&auto=format&fit=crop&q=60"),
            },
            ["Klasik Kol Saati"] = new (string, int, string?)[] {
                ("Gümüş", 49, null),
                ("Siyah", 10, "https://images.unsplash.com/photo-1639736922209-793b59a41572?w=500&auto=format&fit=crop&q=60"),
                ("Altın", 0, "https://images.unsplash.com/photo-1541778480-fc1752bbc2a9?w=500&auto=format&fit=crop&q=60"),
            },
        };

        var sizeVariants = new Dictionary<string, (string Size, int Stock)[]>
        {
            ["Oversize Pamuklu Hoodie"] = new[] { ("S", 30), ("M", 40), ("L", 49), ("XL", 0) },
            ["Klasik Deri Ceket"] = new[] { ("S", 0), ("M", 49), ("L", 20), ("XL", 10) },
            ["Beyaz Rahat Spor Ayakkabı"] = new[] { ("40", 15), ("41", 25), ("42", 49), ("43", 0), ("44", 8) },
            ["Keten Yazlık Gömlek"] = new[] { ("S", 18), ("M", 49), ("L", 22), ("XL", 0) },
        };

        var productNames = colorVariants.Keys.Union(sizeVariants.Keys).ToList();
        var products = db.Products.Where(p => productNames.Contains(p.Name)).ToList();
        var productsWithoutVariants = products
            .Where(p => !db.ProductVariants.Any(v => v.ProductId == p.Id))
            .ToList();

        foreach (var product in productsWithoutVariants)
        {
            if (colorVariants.TryGetValue(product.Name, out var colors))
            {
                foreach (var (color, stock, image) in colors)
                {
                    db.ProductVariants.Add(new ProductVariant { ProductId = product.Id, Color = color, Stock = stock, Image = image });
                }
            }

            if (sizeVariants.TryGetValue(product.Name, out var sizes))
            {
                foreach (var (size, stock) in sizes)
                {
                    db.ProductVariants.Add(new ProductVariant { ProductId = product.Id, Size = size, Stock = stock });
                }
            }
        }

        if (productsWithoutVariants.Count > 0) db.SaveChanges();
    }

    // SeedProductVariantsIfMissing() sadece "hiç varyantı olmayan" ürünlere yeni satır ekler;
    // görsel eşlemesi eklendiğinde daha önce Image=null olarak yaratılmış satırlar hâlâ boş
    // kalır. Burada var olan renk varyantlarını, ürün adı + renk eşleşmesine göre idempotent
    // şekilde (sadece Image hâlâ null olanları) güncelliyoruz.
    public static void BackfillProductVariantImages(AppDbContext db)
    {
        var variantImages = new Dictionary<(string ProductName, string Color), string>
        {
            [("Oversize Pamuklu Hoodie", "Gri")] = "https://images.unsplash.com/photo-1556821840-3a63f95609a7?w=500&auto=format&fit=crop&q=60",
            [("Oversize Pamuklu Hoodie", "Bordo")] = "https://images.unsplash.com/photo-1721111259873-5a13f7fcd67b?w=500&auto=format&fit=crop&q=60",
            [("Klasik Deri Ceket", "Siyah")] = "https://images.unsplash.com/photo-1521223890158-f9f7c3d5d504?w=500&auto=format&fit=crop&q=60",
            [("Klasik Deri Ceket", "Lacivert")] = "https://images.unsplash.com/photo-1592878904946-b3cd8ae243d0?w=500&auto=format&fit=crop&q=60",
            [("Beyaz Rahat Spor Ayakkabı", "Siyah")] = "https://images.unsplash.com/photo-1574020462714-5451391cc336?w=500&auto=format&fit=crop&q=60",
            [("Beyaz Rahat Spor Ayakkabı", "Gri")] = "https://images.unsplash.com/photo-1621665421571-2d325f9c7c6a?w=500&auto=format&fit=crop&q=60",
            [("Polarize Güneş Gözlüğü", "Kahverengi")] = "https://images.unsplash.com/photo-1567333126229-db29200c25f1?w=500&auto=format&fit=crop&q=60",
            [("Polarize Güneş Gözlüğü", "Mavi")] = "https://images.unsplash.com/photo-1564867739458-f42235fab442?w=500&auto=format&fit=crop&q=60",
            [("Su Geçirmez Sırt Çantası", "Siyah")] = "https://images.unsplash.com/photo-1642375352724-8b523c67b8be?w=500&auto=format&fit=crop&q=60",
            [("Su Geçirmez Sırt Çantası", "Haki")] = "https://images.unsplash.com/photo-1602845860431-35374f24f48d?w=500&auto=format&fit=crop&q=60",
            [("Örme Kışlık Bere", "Siyah")] = "https://images.unsplash.com/photo-1618354691792-d1d42acfd860?w=500&auto=format&fit=crop&q=60",
            [("Örme Kışlık Bere", "Bordo")] = "https://images.unsplash.com/photo-1767022518623-1d373c45076a?w=500&auto=format&fit=crop&q=60",
            [("Keten Yazlık Gömlek", "Mavi")] = "https://images.unsplash.com/photo-1740711152088-88a009e877bb?w=500&auto=format&fit=crop&q=60",
            [("Keten Yazlık Gömlek", "Bej")] = "https://images.unsplash.com/photo-1666358084687-14347fbf364c?w=500&auto=format&fit=crop&q=60",
            [("Klasik Kol Saati", "Siyah")] = "https://images.unsplash.com/photo-1639736922209-793b59a41572?w=500&auto=format&fit=crop&q=60",
            [("Klasik Kol Saati", "Altın")] = "https://images.unsplash.com/photo-1541778480-fc1752bbc2a9?w=500&auto=format&fit=crop&q=60",
        };

        var candidates = (
            from v in db.ProductVariants
            join p in db.Products on v.ProductId equals p.Id
            where v.Color != null && v.Image == null
            select new { Variant = v, ProductName = p.Name }
        ).ToList();

        var changed = false;
        foreach (var c in candidates)
        {
            if (variantImages.TryGetValue((c.ProductName, c.Variant.Color!), out var image))
            {
                c.Variant.Image = image;
                changed = true;
            }
        }

        if (changed) db.SaveChanges();
    }

    // Vitrindeki her ürünün en az birkaç yorumu ve gerçekçi (birbirinden farklı) bir puanı
    // olsun diye, sahte ama makul birkaç "demo alıcı" hesabı üzerinden idempotent şekilde
    // yorum + puan seed ediyoruz. Zaten yorumu olan ürünlere dokunulmaz.
    public static void SeedDemoReviewsIfMissing(AppDbContext db)
    {
        var reviewerNames = new[] { "Elif Yıldız", "Mehmet Kaya", "Zeynep Arslan", "Can Demir", "Ayşe Şahin", "Burak Öztürk" };
        var reviewers = new List<Customer>();
        foreach (var name in reviewerNames)
        {
            var email = $"{name.Split(' ')[0].ToLowerInvariant()}.demo@trendmarket.com";
            var existing = db.Customers.FirstOrDefault(c => c.Email == email);
            if (existing != null)
            {
                reviewers.Add(existing);
                continue;
            }

            var reviewer = new Customer
            {
                Name = name,
                Email = email,
                Phone = "5000000000",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("demo1234"),
            };
            db.Customers.Add(reviewer);
            reviewers.Add(reviewer);
        }
        db.SaveChanges();

        var commentPool = new[]
        {
            "Ürün tam anlatıldığı gibi, çok memnun kaldım.",
            "Kalitesi fiyatına göre gayet iyi, tavsiye ederim.",
            "Kargo hızlıydı, paketleme de özenliydi.",
            "Beklediğimden daha kaliteli çıktı.",
            "Fotoğraftaki gibi geldi, gayet memnunum.",
            "Bir süredir kullanıyorum, hâlâ gayet iyi durumda.",
            "Fiyat/performans olarak başarılı bir ürün.",
            "İkinci kez alıyorum, güvenle tercih ediyorum.",
            "Beklentimi karşıladı, teşekkürler.",
            "Kullanışlı ve sağlam, herkese önerebilirim.",
        };

        var productsWithoutReviews = db.Products
            .Where(p => !db.ProductReviews.Any(r => r.ProductId == p.Id))
            .ToList();

        foreach (var product in productsWithoutReviews)
        {
            // Ürün kimliğine göre belirlenen, çalışmalar arasında hep aynı sonucu üreten
            // (idempotent) ama üründen ürüne farklılaşan bir yorum sayısı ve puan ortalaması.
            var reviewCount = 3 + (product.Id % 3); // 3-5 yorum
            for (int i = 0; i < reviewCount; i++)
            {
                var reviewer = reviewers[(product.Id + i) % reviewers.Count];
                var comment = commentPool[(product.Id * 3 + i) % commentPool.Length];
                db.ProductReviews.Add(new ProductReview
                {
                    ProductId = product.Id,
                    CustomerId = reviewer.Id,
                    CustomerName = reviewer.Name,
                    Comment = comment,
                    CreatedAt = DateTime.UtcNow.AddDays(-(i + 1) * 3),
                });
            }

            // 3.5 ile 5.0 arasında değişen, ürüne göre sabit bir ortalama puan.
            var ratingCount = 4 + (product.Id % 5); // 4-8 puan
            var avgRating = 3.5 + (product.Id % 4) * 0.5; // 3.5 / 4.0 / 4.5 / 5.0
            product.RatingCount = ratingCount;
            product.RatingSum = (int)Math.Round(avgRating * ratingCount);
        }

        if (productsWithoutReviews.Count > 0) db.SaveChanges();
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
