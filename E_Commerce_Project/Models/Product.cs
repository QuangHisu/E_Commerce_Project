namespace E_Commerce_Project.Models;

public sealed record Product(string Id, string SellerId, string Name, string Description, string Category, decimal Price, int Stock, string ImageUrl, bool Visible = true, bool Archived = false);
