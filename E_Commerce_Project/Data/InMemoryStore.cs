using E_Commerce_Project.Models;
using Microsoft.AspNetCore.Identity;
namespace E_Commerce_Project.Data;

public sealed class InMemoryStore : IUserRepository, IProductRepository
{
    private readonly TimeProvider clock;
    private readonly List<ApplicationUser> users;
    private readonly List<Product> products;
    private readonly object gate = new();
    private readonly Dictionary<string, Dictionary<string, int>> carts = new();
    private readonly List<Order> orders = new();
    private readonly List<AuditEntry> audits = new();
    private readonly Dictionary<string, (string Hash, DateTimeOffset Expires)> resets = new();
    public string? IssuePasswordReset(string userId)
    {
        lock (gate)
        {
            if (!users.Any(u => u.Id == userId && u.Active)) return null;
            var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            resets[userId] = (ResetHash(token), clock.GetUtcNow().AddMinutes(20));
            return token;
        }
    }
    private static string ResetHash(string token) => Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
    public bool ResetPassword(string userId, string token, string password, IPasswordHasher<ApplicationUser> hasher)
    {
        lock (gate)
        {
            var user = users.FirstOrDefault(u => u.Id == userId && u.Active);
            if (user == null || !resets.TryGetValue(userId, out var entry) || entry.Expires <= clock.GetUtcNow() ||
                !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(entry.Hash), Convert.FromHexString(ResetHash(token)))) return false;
            user.PasswordHash = hasher.HashPassword(user, password);
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            resets.Remove(userId);
            Log(userId, "Reset password", userId);
            return true;
        }
    }
    public void RevokeReset(string userId, string token)
    {
        lock (gate)
            if (resets.TryGetValue(userId, out var entry) && entry.Hash == ResetHash(token)) resets.Remove(userId);
    }
    public InMemoryStore(IPasswordHasher<ApplicationUser> hasher, TimeProvider? clock = null)
    {
        this.clock = clock ?? TimeProvider.System;
        users = new()
        {
            new() { Id = "buyer-1", FullName = "Alex Student", Email = "buyer@campus.example", Role = Roles.Buyer },
            new() { Id = "seller-1", FullName = "Campus Supplies", Email = "seller@campus.example", Role = Roles.Seller },
            new() { Id = "seller-2", FullName = "Study Shop", Email = "seller2@campus.example", Role = Roles.Seller },
            new() { Id = "admin-1", FullName = "Demo Admin", Email = "admin@campus.example", Role = Roles.Admin }
        };
        foreach (var user in users)
            user.PasswordHash = hasher.HashPassword(user, "CampusDemo!2026");
        products = new List<Product>
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
            new("pad", "seller-2", "Mouse Pad", "A smooth pad for your study desk.", "Desk accessories", 7m, 24, "/images/products/pad.svg")
        };
    }

    public Task<ApplicationUser?> FindByEmailAsync(string email)
    {
        lock (gate)
            return Task.FromResult(users.FirstOrDefault(u => u.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase)));
    }
    public ApplicationUser? UserById(string id)
    {
        lock (gate)
            return users.FirstOrDefault(u => u.Id == id);
    }
    Task<IReadOnlyList<ApplicationUser>> IUserRepository.GetAllAsync()
    {
        lock (gate)
            return Task.FromResult<IReadOnlyList<ApplicationUser>>(users.ToArray());
    }
    public IReadOnlyList<ApplicationUser> Users()
    {
        lock (gate)
            return users.ToArray();
    }
    public Task<IReadOnlyList<Product>> GetAllAsync()
    {
        lock (gate)
            return Task.FromResult<IReadOnlyList<Product>>(products.Where(Available).ToArray());
    }
    private bool Available(Product p) => !p.Archived && p.Visible && users.Any(u => u.Id == p.SellerId && u.Active);
    public IReadOnlyList<Product> AllProducts()
    {
        lock (gate)
            return products.ToArray();
    }
    public Task<Product?> GetByIdAsync(string id)
    {
        lock (gate)
            return Task.FromResult(products.FirstOrDefault(p => p.Id == id));
    }
    public Task<IReadOnlyList<Product>> GetBySellerAsync(string id)
    {
        lock (gate)
            return Task.FromResult<IReadOnlyList<Product>>(products.Where(p => p.SellerId == id && !p.Archived).ToArray());
    }
    public bool Register(ApplicationUser user)
    {
        lock (gate)
        {
            if (users.Any(u => u.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase)))
                return false;
            users.Add(user);
            Log(user.Id, "Registered", user.Role);
            return true;
        }
    }
    public void Profile(string id, string name, string contact)
    {
        lock (gate)
        {
            var u = users.Single(u => u.Id == id);
            u.FullName = name;
            u.Contact = contact;
            Log(id, "Updated profile", id);
        }
    }
    private void Log(string actor, string action, string record) => audits.Add(new(DateTime.UtcNow, actor, action, record));
    public IReadOnlyList<AuditEntry> Logs()
    {
        lock (gate)
            return audits.AsEnumerable().Reverse().Take(500).ToArray();
    }
    public void SetActive(string actor, string id, bool active)
    {
        lock (gate)
        {
            var u = users.SingleOrDefault(u => u.Id == id) ?? throw new InvalidOperationException("Account not found.");
            if (u.Role == Roles.Admin)
                throw new InvalidOperationException("Admin accounts cannot be suspended.");
            u.Active = active;
            Log(actor, active ? "Restored account" : "Suspended account", id);
        }
    }
    public Product SaveProduct(string seller, E_Commerce_Project.ViewModels.ProductForm f, string image)
    {
        lock (gate)
        {
            var old = products.FirstOrDefault(p => p.Id == f.Id);
            if (f.Id != null && (old == null || old.SellerId != seller || old.Archived))
                throw new InvalidOperationException("Product not found.");
            var p = new Product(old?.Id ?? Guid.NewGuid().ToString("N"), seller, f.Name.Trim(), f.Description.Trim(), f.Category, f.Price, f.Stock, image, old?.Visible ?? true);
            if (old != null)
                products[products.IndexOf(old)] = p;
            else
                products.Add(p);
            Log(seller, old == null ? "Created product" : "Updated product", p.Id);
            return p;
        }
    }
    public void DeleteProduct(string seller, string id)
    {
        lock (gate)
        {
            var p = products.SingleOrDefault(p => p.Id == id && p.SellerId == seller && !p.Archived) ?? throw new InvalidOperationException("Product not found.");
            if (orders.Any(o => o.Items.Any(i => i.ProductId == id)))
                products[products.IndexOf(p)] = p with
                {
                    Archived = true,
                    Visible = false
                };
            else
                products.Remove(p);
            foreach (var c in carts.Values)
                c.Remove(id);
            Log(seller, "Removed product", id);
        }
    }
    public void Visibility(string actor, string id, bool visible)
    {
        lock (gate)
        {
            var p = products.SingleOrDefault(p => p.Id == id) ?? throw new InvalidOperationException("Product not found.");
            if (p.Archived)
                throw new InvalidOperationException("Archived listing.");
            products[products.IndexOf(p)] = p with
            {
                Visible = visible
            };
            Log(actor, visible ? "Restored listing" : "Hid listing", id);
        }
    }
    public IReadOnlyList<CartLine> Cart(string buyer)
    {
        lock (gate)
        {
            if (!carts.TryGetValue(buyer, out var c))
                return Array.Empty<CartLine>();
            return c.Where(x => products.Any(p => p.Id == x.Key)).Select(x => new CartLine(products.Single(p => p.Id == x.Key), x.Value)).ToArray();
        }
    }
    public void Quantity(string buyer, string id, int amount, bool add = false)
    {
        lock (gate)
        {
            if (!carts.TryGetValue(buyer, out var c))
                carts[buyer] = c = new();
            if (amount == 0 && !add)
            {
                c.Remove(id);
                return;
            }
            var p = products.SingleOrDefault(p => p.Id == id && Available(p)) ?? throw new InvalidOperationException("Product unavailable.");
            var q = (long)amount + (add ? c.GetValueOrDefault(id) : 0);
            if (q < 1 || q > 99 || q > p.Stock)
                throw new InvalidOperationException("Quantity must be 1–99 and within stock.");
            c[id] = (int)q;
        }
    }
    public Order BeginOrder(string buyer, string provider)
    {
        lock (gate)
        {
            var pending = orders.FirstOrDefault(o => o.BuyerId == buyer && o.PaymentStatus == "Pending");
            if (pending != null)
                return pending.Copy();
            var user = users.Single(u => u.Id == buyer && u.Active);
            var lines = Cart(buyer);
            if (lines.Count == 0)
                throw new InvalidOperationException("Your cart is empty.");
            foreach (var l in lines)
                if (l.Quantity > l.Product.Stock || !Available(l.Product))
                    throw new InvalidOperationException("An item is unavailable or has insufficient stock. Update your cart.");
            var order = new Order { BuyerId = buyer, BuyerName = user.FullName, Contact = string.IsNullOrWhiteSpace(user.Contact) ? user.Email : user.Contact, Provider = provider, Items = lines.Select(l => new OrderItem(l.Product.Id, l.Product.SellerId, l.Product.Name, l.Product.ImageUrl, l.Product.Price, l.Quantity)).ToList() };
            foreach (var l in lines)
            {
                var p = l.Product;
                products[products.IndexOf(p)] = p with
                {
                    Stock = p.Stock - l.Quantity
                };
            }
            orders.Add(order);
            carts.Remove(buyer);
            Log(buyer, "Created pending order", order.Id);
            return order.Copy();
        }
    }
    public Order? GetOrder(string id)
    {
        lock (gate)
            return orders.FirstOrDefault(o => o.Id == id)?.Copy();
    }
    public IReadOnlyList<Order> Orders()
    {
        lock (gate)
            return orders.OrderByDescending(o => o.CreatedAt).Select(o => o.Copy()).ToArray();
    }
    public void AttachSession(string id, string session, string url)
    {
        lock (gate)
        {
            var o = orders.Single(o => o.Id == id);
            if (o.SessionId != null && o.SessionId != session)
                throw new InvalidOperationException("Session mismatch.");
            o.SessionId = session;
            o.PaymentUrl = url;
        }
    }
    public void Paid(string id, string provider, string reference, decimal total, string? session = null)
    {
        lock (gate)
        {
            var o = orders.SingleOrDefault(o => o.Id == id) ?? throw new InvalidOperationException("Order not found.");
            if (o.Provider != provider || o.Total != total || (provider == "Stripe" && o.SessionId != session))
                throw new InvalidOperationException("Payment mismatch.");
            if (o.PaymentStatus == "Paid")
                return;
            if (o.PaymentStatus != "Pending")
                throw new InvalidOperationException("Order no longer pending.");
            o.PaymentStatus = "Paid";
            o.PaymentReference = reference;
            Log("System", "Verified " + provider + " payment", id);
        }
    }
    public void Cancel(string id)
    {
        lock (gate)
        {
            var o = orders.Single(o => o.Id == id);
            if (o.PaymentStatus != "Pending")
                return;
            o.PaymentStatus = "Cancelled";
            foreach (var i in o.Items)
            {
                var p = products.FirstOrDefault(p => p.Id == i.ProductId);
                if (p != null)
                    products[products.IndexOf(p)] = p with
                    {
                        Stock = p.Stock + i.Quantity
                    };
            }
            Log(o.BuyerId, "Cancelled order and released stock", id);
        }
    }
    public void Fulfill(string seller, string id, string status)
    {
        lock (gate)
        {
            var o = orders.SingleOrDefault(o => o.Id == id) ?? throw new InvalidOperationException("Order not found.");
            if (o.PaymentStatus != "Paid")
                throw new InvalidOperationException("Only paid orders can be fulfilled.");
            if (status != "Ready for pickup" && status != "Completed")
                throw new InvalidOperationException("Invalid status.");
            var mine = o.Items.Where(i => i.SellerId == seller).ToArray();
            var expected = status == "Ready for pickup" ? "Processing" : "Ready for pickup";
            if (mine.Length == 0 || mine.Any(i => i.Status != expected))
                throw new InvalidOperationException("Follow Processing, Ready for pickup, then Completed.");
            o.Items = o.Items.Select(i => i.SellerId == seller ? i with { Status = status } : i).ToList();
            Log(seller, "Updated pickup status", id);
        }
    }
}
