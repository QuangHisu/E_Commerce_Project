using E_Commerce_Project.Models;
using E_Commerce_Project.ViewModels;
using Microsoft.AspNetCore.Identity;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;

namespace E_Commerce_Project.Data;

public sealed partial class MongoStore : IUserRepository, IProductRepository
{
    private readonly MongoContext context;
    private readonly TimeProvider clock;
    private readonly IMongoCollection<ApplicationUser> users;
    private readonly IMongoCollection<Product> products;
    private readonly IMongoCollection<CartDocument> carts;
    private readonly IMongoCollection<Order> orders;
    private readonly IMongoCollection<AuditEntry> audits;
    private readonly IMongoCollection<PasswordResetDocument> resets;

    public MongoStore(MongoContext context, TimeProvider? clock = null)
    {
        this.context = context;
        this.clock = clock ?? TimeProvider.System;
        users = context.Database.GetCollection<ApplicationUser>("Users");
        products = context.Database.GetCollection<Product>("Products");
        carts = context.Database.GetCollection<CartDocument>("Carts");
        orders = context.Database.GetCollection<Order>("Orders");
        audits = context.Database.GetCollection<AuditEntry>("AuditLogs");
        resets = context.Database.GetCollection<PasswordResetDocument>("PasswordResets");
    }

    // A database write fence serializes mutations across app instances. This small
    // project favors consistency over write throughput; no in-process data cache is used.
    private T Transaction<T>(Func<IClientSessionHandle, T> operation)
    {
        using var session = context.Client.StartSession();
        return session.WithTransaction((s, _) =>
        {
            context.Database.GetCollection<BsonDocument>("AppState").UpdateOne(s,
                new BsonDocument("_id", "mutation"), new BsonDocument("$inc", new BsonDocument("Revision", 1L)));
            return operation(s);
        }, new TransactionOptions(readConcern: ReadConcern.Snapshot,
            readPreference: ReadPreference.Primary, writeConcern: WriteConcern.WMajority));
    }
    private void Log(IClientSessionHandle s, string actor, string action, string record) =>
        audits.InsertOne(s, new AuditEntry(clock.GetUtcNow().UtcDateTime, actor, action, record));
    private static string Normalize(string email) => email.Trim().ToUpperInvariant();
    private static string ResetHash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public string? IssuePasswordReset(string userId) => Transaction(s =>
    {
        if (!users.Find(s, u => u.Id == userId && u.Active).Any()) return null;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        resets.ReplaceOne(s, r => r.Id == userId, new PasswordResetDocument
        { Id = userId, Hash = ResetHash(token), ExpiresAt = clock.GetUtcNow().AddMinutes(20).UtcDateTime },
            new ReplaceOptions { IsUpsert = true });
        return token;
    });
    public bool ResetPassword(string userId, string token, string password, IPasswordHasher<ApplicationUser> hasher) => Transaction(s =>
    {
        var user = users.Find(s, u => u.Id == userId && u.Active).FirstOrDefault();
        var reset = resets.Find(s, r => r.Id == userId).FirstOrDefault();
        if (user == null || reset == null || reset.ExpiresAt <= clock.GetUtcNow().UtcDateTime ||
            !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(reset.Hash), Convert.FromHexString(ResetHash(token)))) return false;
        users.UpdateOne(s, u => u.Id == userId, Builders<ApplicationUser>.Update
            .Set(u => u.PasswordHash, hasher.HashPassword(user, password)).Set(u => u.SecurityStamp, Guid.NewGuid().ToString("N")));
        resets.DeleteOne(s, r => r.Id == userId);
        Log(s, userId, "Reset password", userId);
        return true;
    });
    public void RevokeReset(string userId, string token) => Transaction(s =>
        resets.DeleteOne(s, r => r.Id == userId && r.Hash == ResetHash(token)).DeletedCount);

    public async Task<ApplicationUser?> FindByEmailAsync(string email) =>
        await users.Find(u => u.NormalizedEmail == Normalize(email)).FirstOrDefaultAsync();
    public ApplicationUser? UserById(string id) => users.Find(u => u.Id == id).FirstOrDefault();
    async Task<IReadOnlyList<ApplicationUser>> IUserRepository.GetAllAsync() => await users.Find(_ => true).ToListAsync();
    public IReadOnlyList<ApplicationUser> Users() => users.Find(_ => true).ToList();
    public async Task<IReadOnlyList<Product>> GetAllAsync()
    {
        var active = await users.Find(u => u.Active && u.Role == Roles.Seller).Project(u => u.Id).ToListAsync();
        return await products.Find(p => !p.Archived && p.Visible && active.Contains(p.SellerId)).ToListAsync();
    }
    public IReadOnlyList<Product> AllProducts() => products.Find(_ => true).ToList();
    public async Task<Product?> GetByIdAsync(string id) => await products.Find(p => p.Id == id).FirstOrDefaultAsync();
    public async Task<IReadOnlyList<Product>> GetBySellerAsync(string id) =>
        await products.Find(p => p.SellerId == id && !p.Archived).ToListAsync();
    private bool Available(IClientSessionHandle s, Product p) => !p.Archived && p.Visible &&
        users.Find(s, u => u.Id == p.SellerId && u.Active && u.Role == Roles.Seller).Any();

    public bool Register(ApplicationUser user)
    {
        user.NormalizedEmail = Normalize(user.Email);
        if (user.Role != Roles.Buyer && user.Role != Roles.Seller) return false;
        return Transaction(s =>
        {
            if (users.Find(s, u => u.NormalizedEmail == user.NormalizedEmail).Any()) return false;
            users.InsertOne(s, user);
            Log(s, user.Id, "Registered", user.Role);
            return true;
        });
    }
    public void Profile(string id, string name, string contact) => Transaction(s =>
    {
        if (users.UpdateOne(s, u => u.Id == id, Builders<ApplicationUser>.Update
            .Set(u => u.FullName, name).Set(u => u.Contact, contact)).MatchedCount != 1)
            throw new InvalidOperationException("Account not found.");
        Log(s, id, "Updated profile", id);
        return true;
    });
    public IReadOnlyList<AuditEntry> Logs() => audits.Find(_ => true).SortByDescending(a => a.Time).Limit(500).ToList();
    public void SetActive(string actor, string id, bool active) => Transaction(s =>
    {
        var user = users.Find(s, u => u.Id == id).FirstOrDefault() ?? throw new InvalidOperationException("Account not found.");
        if (user.Role == Roles.Admin) throw new InvalidOperationException("Admin accounts cannot be suspended.");
        users.UpdateOne(s, u => u.Id == id, Builders<ApplicationUser>.Update.Set(u => u.Active, active)
            .Set(u => u.SecurityStamp, Guid.NewGuid().ToString("N")));
        Log(s, actor, active ? "Restored account" : "Suspended account", id);
        return true;
    });
    public Product SaveProduct(string seller, ProductForm f, string image) => Transaction(s =>
    {
        if (!users.Find(s, u => u.Id == seller && u.Active && u.Role == Roles.Seller).Any())
            throw new InvalidOperationException("Seller unavailable.");
        var old = f.Id == null ? null : products.Find(s, p => p.Id == f.Id).FirstOrDefault();
        if (f.Id != null && (old == null || old.SellerId != seller || old.Archived)) throw new InvalidOperationException("Product not found.");
        var product = new Product(old?.Id ?? Guid.NewGuid().ToString("N"), seller, f.Name.Trim(), f.Description.Trim(),
            f.Category, f.Price, f.Stock, image, old?.Visible ?? true);
        if (old == null) products.InsertOne(s, product);
        else products.ReplaceOne(s, p => p.Id == product.Id, product);
        Log(s, seller, old == null ? "Created product" : "Updated product", product.Id);
        return product;
    });
    public void DeleteProduct(string seller, string id) => Transaction(s =>
    {
        var p = products.Find(s, p => p.Id == id && p.SellerId == seller && !p.Archived).FirstOrDefault()
            ?? throw new InvalidOperationException("Product not found.");
        if (orders.Find(s, o => o.Items.Any(i => i.ProductId == id)).Any())
            products.UpdateOne(s, p => p.Id == id, Builders<Product>.Update.Set(p => p.Archived, true).Set(p => p.Visible, false));
        else products.DeleteOne(s, p => p.Id == id);
        carts.UpdateMany(s, _ => true, Builders<CartDocument>.Update.PullFilter(c => c.Items, i => i.ProductId == id));
        Log(s, seller, "Removed product", id);
        return true;
    });
    public void Visibility(string actor, string id, bool visible) => Transaction(s =>
    {
        var p = products.Find(s, p => p.Id == id).FirstOrDefault() ?? throw new InvalidOperationException("Product not found.");
        if (p.Archived) throw new InvalidOperationException("Archived listing.");
        products.UpdateOne(s, p => p.Id == id, Builders<Product>.Update.Set(p => p.Visible, visible));
        Log(s, actor, visible ? "Restored listing" : "Hid listing", id);
        return true;
    });
    public IReadOnlyList<CartLine> Cart(string buyer)
    {
        var cart = carts.Find(c => c.Id == buyer).FirstOrDefault();
        if (cart == null) return Array.Empty<CartLine>();
        var ids = cart.Items.Select(i => i.ProductId).ToArray();
        var found = products.Find(p => ids.Contains(p.Id)).ToList().ToDictionary(p => p.Id);
        return cart.Items.Where(i => found.ContainsKey(i.ProductId)).Select(i => new CartLine(found[i.ProductId], i.Quantity)).ToArray();
    }
    public void Quantity(string buyer, string id, int amount, bool add = false) => Transaction(s =>
    {
        if (!users.Find(s, u => u.Id == buyer && u.Active && u.Role == Roles.Buyer).Any())
            throw new InvalidOperationException("Buyer unavailable.");
        var cart = carts.Find(s, c => c.Id == buyer).FirstOrDefault() ?? new CartDocument { Id = buyer };
        var previous = cart.Items.FirstOrDefault(i => i.ProductId == id);
        if (amount != 0 || add)
        {
            var p = products.Find(s, p => p.Id == id).FirstOrDefault();
            if (p == null || !Available(s, p)) throw new InvalidOperationException("Product unavailable.");
            var quantity = (long)amount + (add ? previous?.Quantity ?? 0 : 0);
            if (quantity < 1 || quantity > 99 || quantity > p.Stock) throw new InvalidOperationException("Quantity must be 1–99 and within stock.");
            cart.Items.RemoveAll(i => i.ProductId == id);
            cart.Items.Add(new CartEntry(id, (int)quantity));
        }
        else cart.Items.RemoveAll(i => i.ProductId == id);
        carts.ReplaceOne(s, c => c.Id == buyer, cart, new ReplaceOptions { IsUpsert = true });
        return true;
    });
    public Order BeginOrder(string buyer, string provider) => Transaction(s =>
    {
        if (provider is not ("Demo" or "Stripe")) throw new InvalidOperationException("Invalid provider.");
        var user = users.Find(s, u => u.Id == buyer && u.Active && u.Role == Roles.Buyer).FirstOrDefault()
            ?? throw new InvalidOperationException("Buyer unavailable.");
        var pending = orders.Find(s, o => o.BuyerId == buyer && o.PaymentStatus == "Pending").FirstOrDefault();
        if (pending != null) return pending;
        var cart = carts.Find(s, c => c.Id == buyer).FirstOrDefault();
        if (cart == null || cart.Items.Count == 0) throw new InvalidOperationException("Your cart is empty.");
        var items = new List<OrderItem>();
        foreach (var line in cart.Items)
        {
            var p = products.Find(s, p => p.Id == line.ProductId).FirstOrDefault();
            if (p == null || line.Quantity < 1 || line.Quantity > 99 || p.Stock < line.Quantity || !Available(s, p))
                throw new InvalidOperationException("An item is unavailable or has insufficient stock. Update your cart.");
            var changed = products.UpdateOne(s, p => p.Id == line.ProductId && p.Stock >= line.Quantity,
                Builders<Product>.Update.Inc(p => p.Stock, -line.Quantity));
            if (changed.ModifiedCount != 1) throw new InvalidOperationException("Insufficient stock.");
            items.Add(new OrderItem(p.Id, p.SellerId, p.Name, p.ImageUrl, p.Price, line.Quantity));
        }
        var order = new Order
        {
            BuyerId = buyer,
            BuyerName = user.FullName,
            Contact = string.IsNullOrWhiteSpace(user.Contact) ? user.Email : user.Contact,
            Provider = provider,
            Items = items
        };
        orders.InsertOne(s, order);
        carts.DeleteOne(s, c => c.Id == buyer);
        Log(s, buyer, "Created pending order", order.Id);
        return order;
    });
    public Order? GetOrder(string id) => orders.Find(o => o.Id == id).FirstOrDefault();
    public IReadOnlyList<Order> Orders() => orders.Find(_ => true).SortByDescending(o => o.CreatedAt).ToList();
    public void AttachSession(string id, string session, string url) => Transaction(s =>
    {
        var order = orders.Find(s, o => o.Id == id).FirstOrDefault() ?? throw new InvalidOperationException("Order not found.");
        if (order.SessionId != null && order.SessionId != session) throw new InvalidOperationException("Session mismatch.");
        orders.UpdateOne(s, o => o.Id == id, Builders<Order>.Update.Set(o => o.SessionId, session).Set(o => o.PaymentUrl, url));
        return true;
    });
    public void Paid(string id, string provider, string reference, decimal total, string? session = null) => Transaction(s =>
    {
        var order = orders.Find(s, o => o.Id == id).FirstOrDefault() ?? throw new InvalidOperationException("Order not found.");
        if (order.Provider != provider || order.Total != total || (provider == "Stripe" && order.SessionId != session))
            throw new InvalidOperationException("Payment mismatch.");
        if (order.PaymentStatus == "Paid") return true;
        if (order.PaymentStatus != "Pending") throw new InvalidOperationException("Order no longer pending.");
        orders.UpdateOne(s, o => o.Id == id, Builders<Order>.Update.Set(o => o.PaymentStatus, "Paid").Set(o => o.PaymentReference, reference));
        Log(s, "System", "Verified " + provider + " payment", id);
        return true;
    });
    public void Cancel(string id) => Transaction(s =>
    {
        var order = orders.Find(s, o => o.Id == id).FirstOrDefault() ?? throw new InvalidOperationException("Order not found.");
        if (order.PaymentStatus != "Pending") return true;
        orders.UpdateOne(s, o => o.Id == id, Builders<Order>.Update.Set(o => o.PaymentStatus, "Cancelled"));
        foreach (var item in order.Items)
            products.UpdateOne(s, p => p.Id == item.ProductId, Builders<Product>.Update.Inc(p => p.Stock, item.Quantity));
        Log(s, order.BuyerId, "Cancelled order and released stock", id);
        return true;
    });
    public void Fulfill(string seller, string id, string status) => Transaction(s =>
    {
        var order = orders.Find(s, o => o.Id == id).FirstOrDefault() ?? throw new InvalidOperationException("Order not found.");
        if (order.PaymentStatus != "Paid") throw new InvalidOperationException("Only paid orders can be fulfilled.");
        if (status is not ("Ready for pickup" or "Completed")) throw new InvalidOperationException("Invalid status.");
        var mine = order.Items.Where(i => i.SellerId == seller).ToArray();
        var expected = status == "Ready for pickup" ? "Processing" : "Ready for pickup";
        if (mine.Length == 0 || mine.Any(i => i.Status != expected)) throw new InvalidOperationException("Follow Processing, Ready for pickup, then Completed.");
        orders.UpdateOne(s, o => o.Id == id, Builders<Order>.Update.Set(o => o.Items,
            order.Items.Select(i => i.SellerId == seller ? i with { Status = status } : i).ToList()));
        Log(s, seller, "Updated pickup status", id);
        return true;
    });
}
