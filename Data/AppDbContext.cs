using Microsoft.EntityFrameworkCore;
using TrendMarketServer.Models;

namespace TrendMarketServer.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Seller> Sellers => Set<Seller>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
    public DbSet<ProductPriceHistory> ProductPriceHistories => Set<ProductPriceHistory>();
    public DbSet<CouponTransaction> CouponTransactions => Set<CouponTransaction>();
    public DbSet<ProductView> ProductViews => Set<ProductView>();
    public DbSet<ShipmentMessage> ShipmentMessages => Set<ShipmentMessage>();
    public DbSet<CartEntry> CartEntries => Set<CartEntry>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductNotifyRequest> ProductNotifyRequests => Set<ProductNotifyRequest>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<FavoriteEntry> FavoriteEntries => Set<FavoriteEntry>();
public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Seller>()
            .HasIndex(s => s.Email)
            .IsUnique();

        modelBuilder.Entity<Customer>()
            .HasIndex(c => c.Email)
            .IsUnique();

        // Sepet/favori/gezinti satırları ya bir hesaba (CustomerId) ya da misafir cihaz
        // oturumuna (CustomerId = 0 + SessionId) aittir; tekillik bu ikisinin birlikte
        // oluşturduğu sahiplik anahtarına göre uygulanır. Aksi halde aynı cihazda A hesabının
        // bıraktığı satır, B hesabının aynı ürünü eklemesini benzersizlik ihlaliyle engellerdi.
        modelBuilder.Entity<ProductView>()
            .HasIndex(v => new { v.CustomerId, v.SessionId, v.ProductId })
            .IsUnique();

        // Aynı ürünün farklı renk/beden varyantları ayrı sepet satırları olabildiğinden
        // (bkz. CartEntry.ColorVariantId/SizeVariantId), tekillik sahiplik anahtarıyla birlikte
        // bu alanların tamamının kombinasyonuna göre uygulanır.
        modelBuilder.Entity<CartEntry>()
            .HasIndex(c => new { c.CustomerId, c.SessionId, c.ProductId, c.ColorVariantId, c.SizeVariantId })
            .IsUnique();

        modelBuilder.Entity<ProductNotifyRequest>()
            .HasIndex(n => new { n.CustomerId, n.ProductId, n.VariantId })
            .IsUnique();

        modelBuilder.Entity<Admin>()
            .HasIndex(a => a.Email)
            .IsUnique();

        modelBuilder.Entity<FavoriteEntry>()
            .HasIndex(f => new { f.CustomerId, f.SessionId, f.ProductId })
            .IsUnique();
    }
}
