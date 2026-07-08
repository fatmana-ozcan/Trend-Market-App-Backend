namespace TrendMarketServer.Models;

public class Address
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string AddressText { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}
