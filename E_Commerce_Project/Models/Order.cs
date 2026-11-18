namespace E_Commerce_Project.Models;
public sealed class Order
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public required string BuyerId
    {
        get; init;
    }
    public required string BuyerName
    {
        get; init;
    }
    public required string Contact
    {
        get; init;
    }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public required List<OrderItem> Items
    {
        get; set;
    }
    public decimal Total => Items.Sum(i => i.Price * i.Quantity);
    public string PaymentStatus { get; set; } = "Pending";
    public string Provider { get; init; } = "Demo";
    public string? SessionId
    {
        get; set;
    }
    public string? PaymentUrl
    {
        get; set;
    }
    public string? PaymentReference
    {
        get; set;
    }
    public Order Copy() => new() { Id = Id, BuyerId = BuyerId, BuyerName = BuyerName, Contact = Contact, CreatedAt = CreatedAt, Items = Items.Select(i => i with { }).ToList(), PaymentStatus = PaymentStatus, Provider = Provider, SessionId = SessionId, PaymentUrl = PaymentUrl, PaymentReference = PaymentReference };
}
public sealed record OrderItem(string ProductId, string SellerId, string Name, string ImageUrl, decimal Price, int Quantity, string Status = "Processing");
public sealed record CartLine(Product Product, int Quantity);
public sealed record AuditEntry(DateTime Time, string Actor, string Action, string Record);
