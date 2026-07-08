using System.ComponentModel.DataAnnotations.Schema;

namespace TrendMarketServer.Models;

public class Product
{
    public int Id { get; set; }
    public int SellerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal CostPrice { get; set; }
    public int Stock { get; set; }
    public int SoldCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public int RatingSum { get; set; }
    public int RatingCount { get; set; }

    [NotMapped]
    public double Rating => RatingCount > 0 ? (double)RatingSum / RatingCount : 0;
}
