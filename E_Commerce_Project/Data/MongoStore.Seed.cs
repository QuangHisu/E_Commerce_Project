using E_Commerce_Project.Models;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;
namespace E_Commerce_Project.Data;

public sealed partial class MongoStore
{
    public (int UsersAdded, int ProductsAdded) Seed(string password, IPasswordHasher<ApplicationUser> hasher)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12 || password.Length > 128)
            throw new InvalidOperationException("Set Seed:Password to a 12–128 character password before seeding.");
        var samples = new List<ApplicationUser>();
        for (int i = 1; i <= 6; i++) samples.Add(new ApplicationUser
        { Id = $"buyer-{i}", FullName = $"Sample Buyer {i}", Email = i == 1 ? "buyer@campus.example" : $"buyer{i}@campus.example", Role = Roles.Buyer });
        for (int i = 1; i <= 3; i++) samples.Add(new ApplicationUser
        { Id = $"seller-{i}", FullName = $"Campus Seller {i}", Email = i == 1 ? "seller@campus.example" : $"seller{i}@campus.example", Role = Roles.Seller });
        samples.Add(new ApplicationUser { Id = "admin-1", FullName = "Sample Admin", Email = "admin@campus.example", Role = Roles.Admin });
        foreach (var user in samples)
        {
            user.NormalizedEmail = Normalize(user.Email);
            user.Contact = user.Email;
            user.PasswordHash = hasher.HashPassword(user, password);
        }
        var listings = new List<Product>
        {
            new("notebook", "seller-1", "Notebook", "A ruled notebook for lecture notes.", "Stationery", 8m, 30, "/images/products/notebook.svg"),
            new("pens", "seller-1", "Pen Set", "Three pens for everyday writing.", "Stationery", 6m, 25, "/images/products/pens.svg"),
            new("pencils", "seller-1", "Pencil Set", "Sketching and writing pencils.", "Stationery", 5m, 20, "/images/products/pencils.svg"),
            new("planner", "seller-1", "Weekly Planner", "A weekly planner for assignments.", "Stationery", 12m, 18, "/images/products/planner.svg"),
            new("backpack", "seller-1", "Backpack", "A roomy backpack for campus days.", "Bags", 40m, 8, "/images/products/backpack.svg"),
            new("tote", "seller-1", "Canvas Tote", "A reusable bag for books.", "Bags", 15m, 15, "/images/products/tote.svg"),
            new("pouch", "seller-2", "Pencil Pouch", "A zip pouch for small supplies.", "Bags", 9m, 22, "/images/products/pouch.svg"),
            new("sleeve", "seller-2", "Laptop Sleeve", "A protective sleeve for a laptop.", "Bags", 22m, 10, "/images/products/sleeve.svg"),
            new("lamp", "seller-2", "Desk Lamp", "An adjustable study lamp.", "Desk accessories", 25m, 12, "/images/products/lamp.svg"),
            new("stand", "seller-2", "Phone Stand", "A compact desktop phone stand.", "Desk accessories", 10m, 16, "/images/products/stand.svg"),
            new("organizer", "seller-2", "Desk Organizer", "Keep pens and notes in one place.", "Desk accessories", 14m, 14, "/images/products/organizer.svg"),
            new("pad", "seller-2", "Mouse Pad", "A smooth pad for your study desk.", "Desk accessories", 7m, 24, "/images/products/pad.svg"),
            new("seed-product-13", "seller-2", "Graph Notebook", "Graph Notebook for campus study and everyday use.", "Stationery", 20.31m, 23, "/images/products/notebook.svg"),
            new("seed-product-14", "seller-3", "Sticky Notes", "Sticky Notes for campus study and everyday use.", "Stationery", 21.68m, 24, "/images/products/planner.svg"),
            new("seed-product-15", "seller-1", "Highlighter Set", "Highlighter Set for campus study and everyday use.", "Stationery", 23.05m, 25, "/images/products/pens.svg"),
            new("seed-product-16", "seller-2", "Index Cards", "Index Cards for campus study and everyday use.", "Stationery", 24.42m, 26, "/images/products/notebook.svg"),
            new("seed-product-17", "seller-3", "Binder", "Binder for campus study and everyday use.", "Stationery", 25.79m, 27, "/images/products/notebook.svg"),
            new("seed-product-18", "seller-1", "Document Folder", "Document Folder for campus study and everyday use.", "Stationery", 27.16m, 28, "/images/products/pouch.svg"),
            new("seed-product-19", "seller-2", "Eraser Pack", "Eraser Pack for campus study and everyday use.", "Stationery", 28.53m, 29, "/images/products/pencils.svg"),
            new("seed-product-20", "seller-3", "Ruler Set", "Ruler Set for campus study and everyday use.", "Stationery", 29.90m, 30, "/images/products/pencils.svg"),
            new("seed-product-21", "seller-1", "Mechanical Pencil", "Mechanical Pencil for campus study and everyday use.", "Stationery", 31.27m, 31, "/images/products/pencils.svg"),
            new("seed-product-22", "seller-2", "Gel Pens", "Gel Pens for campus study and everyday use.", "Stationery", 32.64m, 32, "/images/products/pens.svg"),
            new("seed-product-23", "seller-3", "Sketchbook", "Sketchbook for campus study and everyday use.", "Stationery", 34.01m, 33, "/images/products/notebook.svg"),
            new("seed-product-24", "seller-1", "Watercolor Notebook", "Watercolor Notebook for campus study and everyday use.", "Stationery", 35.38m, 34, "/images/products/notebook.svg"),
            new("seed-product-25", "seller-2", "Academic Planner", "Academic Planner for campus study and everyday use.", "Stationery", 36.75m, 35, "/images/products/planner.svg"),
            new("seed-product-26", "seller-3", "Page Markers", "Page Markers for campus study and everyday use.", "Stationery", 38.12m, 36, "/images/products/planner.svg"),
            new("seed-product-27", "seller-1", "Correction Tape", "Correction Tape for campus study and everyday use.", "Stationery", 39.49m, 37, "/images/products/pens.svg"),
            new("seed-product-28", "seller-2", "Scissors", "Scissors for campus study and everyday use.", "Stationery", 40.86m, 38, "/images/products/pencils.svg"),
            new("seed-product-29", "seller-3", "Glue Sticks", "Glue Sticks for campus study and everyday use.", "Stationery", 42.23m, 39, "/images/products/pens.svg"),
            new("seed-product-30", "seller-1", "Geometry Set", "Geometry Set for campus study and everyday use.", "Stationery", 43.60m, 40, "/images/products/pencils.svg"),
            new("seed-product-31", "seller-2", "Ring Binder", "Ring Binder for campus study and everyday use.", "Stationery", 2.97m, 41, "/images/products/notebook.svg"),
            new("seed-product-32", "seller-3", "Flash Cards", "Flash Cards for campus study and everyday use.", "Stationery", 4.34m, 42, "/images/products/notebook.svg"),
            new("seed-product-33", "seller-1", "Laptop Backpack", "Laptop Backpack for campus study and everyday use.", "Bags", 5.71m, 43, "/images/products/backpack.svg"),
            new("seed-product-34", "seller-2", "Drawstring Bag", "Drawstring Bag for campus study and everyday use.", "Bags", 7.08m, 44, "/images/products/tote.svg"),
            new("seed-product-35", "seller-3", "Lunch Bag", "Lunch Bag for campus study and everyday use.", "Bags", 8.45m, 10, "/images/products/tote.svg"),
            new("seed-product-36", "seller-1", "Messenger Bag", "Messenger Bag for campus study and everyday use.", "Bags", 9.82m, 11, "/images/products/backpack.svg"),
            new("seed-product-37", "seller-2", "Travel Pouch", "Travel Pouch for campus study and everyday use.", "Bags", 11.19m, 12, "/images/products/pouch.svg"),
            new("seed-product-38", "seller-3", "Tablet Sleeve", "Tablet Sleeve for campus study and everyday use.", "Bags", 12.56m, 13, "/images/products/sleeve.svg"),
            new("seed-product-39", "seller-1", "Book Tote", "Book Tote for campus study and everyday use.", "Bags", 13.93m, 14, "/images/products/tote.svg"),
            new("seed-product-40", "seller-2", "Laptop Stand", "Laptop Stand for campus study and everyday use.", "Desk accessories", 15.30m, 15, "/images/products/stand.svg"),
            new("seed-product-41", "seller-3", "Book Stand", "Book Stand for campus study and everyday use.", "Desk accessories", 16.67m, 16, "/images/products/stand.svg"),
            new("seed-product-42", "seller-1", "Cable Organizer", "Cable Organizer for campus study and everyday use.", "Desk accessories", 18.04m, 17, "/images/products/organizer.svg"),
            new("seed-product-43", "seller-2", "Desk Mat", "Desk Mat for campus study and everyday use.", "Desk accessories", 19.41m, 18, "/images/products/pad.svg"),
            new("seed-product-44", "seller-3", "Pen Holder", "Pen Holder for campus study and everyday use.", "Desk accessories", 20.78m, 19, "/images/products/organizer.svg"),
            new("seed-product-45", "seller-1", "Reading Lamp", "Reading Lamp for campus study and everyday use.", "Desk accessories", 22.15m, 20, "/images/products/lamp.svg"),
            new("seed-product-46", "seller-2", "Document Tray", "Document Tray for campus study and everyday use.", "Desk accessories", 23.52m, 21, "/images/products/organizer.svg"),
            new("seed-product-47", "seller-3", "Monitor Riser", "Monitor Riser for campus study and everyday use.", "Desk accessories", 24.89m, 22, "/images/products/stand.svg"),
            new("seed-product-48", "seller-1", "Wrist Rest", "Wrist Rest for campus study and everyday use.", "Desk accessories", 26.26m, 23, "/images/products/pad.svg"),
            new("seed-product-49", "seller-2", "Study Timer", "Study Timer for campus study and everyday use.", "Desk accessories", 27.63m, 24, "/images/products/organizer.svg"),
            new("seed-product-50", "seller-3", "Desk Calendar", "Desk Calendar for campus study and everyday use.", "Desk accessories", 29.00m, 25, "/images/products/planner.svg")
        };
        return Transaction(s =>
        {
            int addedUsers = 0, addedProducts = 0;
            foreach (var user in samples)
            {
                var existing = users.Find(s, u => u.Id == user.Id).FirstOrDefault();
                if (existing != null)
                {
                    if (existing.NormalizedEmail != user.NormalizedEmail || existing.Role != user.Role)
                        throw new InvalidOperationException("A sample user ID conflicts with an existing account. Use a new development database.");
                    continue;
                }
                if (users.Find(s, u => u.NormalizedEmail == user.NormalizedEmail).Any())
                    throw new InvalidOperationException("A sample email already belongs to a different account. Use a new development database.");
                users.InsertOne(s, user); addedUsers++;
            }
            foreach (var product in listings)
            {
                var existing = products.Find(s, p => p.Id == product.Id).FirstOrDefault();
                if (existing != null)
                {
                    if (existing.SellerId != product.SellerId)
                        throw new InvalidOperationException("A sample product ID conflicts with an existing listing. Use a new development database.");
                    continue;
                }
                products.InsertOne(s, product); addedProducts++;
            }
            Log(s, "Seed", "Inserted missing sample records", $"{addedUsers} users; {addedProducts} products");
            return (addedUsers, addedProducts);
        });
    }
}
