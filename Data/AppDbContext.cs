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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Seller>()
            .HasIndex(s => s.Email)
            .IsUnique();

        modelBuilder.Entity<Customer>()
            .HasIndex(c => c.Email)
            .IsUnique();

        modelBuilder.Entity<ProductView>()
            .HasIndex(v => new { v.CustomerId, v.ProductId })
            .IsUnique();
    }
}
