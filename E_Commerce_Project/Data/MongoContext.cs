using E_Commerce_Project.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace E_Commerce_Project.Data;

public sealed class MongoContext
{
    static MongoContext()
    {
        ConventionRegistry.Register("ECommerce", new ConventionPack { new IgnoreExtraElementsConvention(true) },
            type => type.Namespace?.StartsWith("E_Commerce_Project") == true);
        BsonSerializer.RegisterSerializer(new DecimalSerializer(BsonType.Decimal128));
    }

    public MongoClient Client { get; }
    public IMongoDatabase Database { get; }
    public MongoContext(IConfiguration config)
    {
        var connection = config["MongoDB:ConnectionString"];
        var name = config["MongoDB:DatabaseName"];
        if (string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Set MongoDB:ConnectionString and MongoDB:DatabaseName using user secrets or environment variables. See MONGODB_SETUP_VI.md.");
        var settings = MongoClientSettings.FromConnectionString(connection);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
        settings.RetryWrites = true;
        Client = new MongoClient(settings);
        Database = Client.GetDatabase(name);
    }

    public void Initialize()
    {
        var hello = Database.RunCommand<BsonDocument>(new BsonDocument("hello", 1));
        if (!hello.Contains("setName") && hello.GetValue("msg", "").AsString != "isdbgrid")
            throw new InvalidOperationException("MongoDB transactions require Atlas or a replica set. A standalone mongod is not supported. See MONGODB_SETUP_VI.md.");
        var existing = Database.ListCollectionNames().ToList();
        foreach (var name in new[] { "Users", "Products", "Carts", "Orders", "AuditLogs", "PasswordResets", "AppState" })
            if (!existing.Contains(name))
                try { Database.CreateCollection(name); }
                catch (MongoCommandException e) when (e.CodeName == "NamespaceExists") { }

        Database.GetCollection<ApplicationUser>("Users").Indexes.CreateOne(new CreateIndexModel<ApplicationUser>(
            Builders<ApplicationUser>.IndexKeys.Ascending(u => u.NormalizedEmail),
            new CreateIndexOptions { Unique = true, Name = "unique_email" }));
        Database.GetCollection<Product>("Products").Indexes.CreateOne(new CreateIndexModel<Product>(
            Builders<Product>.IndexKeys.Ascending(p => p.SellerId), new CreateIndexOptions { Name = "seller" }));
        var orders = Database.GetCollection<Order>("Orders");
        orders.Indexes.CreateOne(new CreateIndexModel<Order>(Builders<Order>.IndexKeys.Ascending(o => o.BuyerId).Descending(o => o.CreatedAt),
            new CreateIndexOptions { Name = "buyer_created" }));
        orders.Indexes.CreateOne(new CreateIndexModel<Order>(Builders<Order>.IndexKeys.Ascending(o => o.BuyerId),
            new CreateIndexOptions<Order>
            {
                Unique = true,
                Name = "one_pending_per_buyer",
                PartialFilterExpression = Builders<Order>.Filter.Eq(o => o.PaymentStatus, "Pending")
            }));
        Database.GetCollection<PasswordResetDocument>("PasswordResets").Indexes.CreateOne(new CreateIndexModel<PasswordResetDocument>(
            Builders<PasswordResetDocument>.IndexKeys.Ascending(r => r.ExpiresAt),
            new CreateIndexOptions { Name = "expires_at", ExpireAfter = TimeSpan.Zero }));
        Database.GetCollection<AuditEntry>("AuditLogs").Indexes.CreateOne(new CreateIndexModel<AuditEntry>(
            Builders<AuditEntry>.IndexKeys.Descending(a => a.Time), new CreateIndexOptions { Name = "recent" }));
        Database.GetCollection<BsonDocument>("AppState").UpdateOne(new BsonDocument("_id", "mutation"),
            new BsonDocument("$setOnInsert", new BsonDocument("Revision", 0L)), new UpdateOptions { IsUpsert = true });
    }
}

public sealed class CartDocument
{
    public string Id { get; set; } = "";
    public List<CartEntry> Items { get; set; } = new();
}
public sealed record CartEntry(string ProductId, int Quantity);
public sealed class PasswordResetDocument
{
    public string Id { get; set; } = "";
    public string Hash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}
