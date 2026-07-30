using System;

namespace TrendMarketServer.Models
{
    public enum TicketStatus
    {
        Open,
        InReview,
        Resolved,
        Closed
    }

    public enum TicketCategory
    {
        Bug = 1,
        FeatureRequest,
        Account,
        Other
    }

    public class SupportTicket
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TicketCategory Category { get; set; }
        public TicketStatus Status { get; set; } = TicketStatus.Open;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}