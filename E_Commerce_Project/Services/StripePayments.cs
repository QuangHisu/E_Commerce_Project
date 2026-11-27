using System.Net.Http.Headers;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using E_Commerce_Project.Data;
using E_Commerce_Project.Models;
namespace E_Commerce_Project.Services;

public sealed class StripePayments(HttpClient client, IConfiguration config, InMemoryStore store)
{
    public string Mode => config["Payments:Mode"] == "Stripe" ? "Stripe" : "Demo";
    private async Task<JsonElement> Request(HttpMethod method, string path, Dictionary<string, string>? values = null, string? key = null)
    {
        var secret = config["Stripe:SecretKey"] ?? "";
        if (!secret.StartsWith("sk_test_"))
            throw new InvalidOperationException("Configure a Stripe test secret key using user secrets first.");
        using var req = new HttpRequestMessage(method, "https://api.stripe.com/v1/" + path);
        req.Headers.Add("Stripe-Version", "2024-06-20");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        if (key != null)
            req.Headers.Add("Idempotency-Key", key);
        if (values != null)
            req.Content = new FormUrlEncodedContent(values);
        using var res = await client.SendAsync(req);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException("Stripe could not complete the request. Check the test configuration and retry; stock remains reserved.");
        using var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }
    public string PublishableKey => config["Stripe:PublishableKey"] ?? "";
    public async Task<string> ClientSecret(Order order)
    {
        if (order.Provider != "Stripe" || order.SessionId == null || order.PaymentStatus != "Pending")
            throw new InvalidOperationException("Order is not awaiting payment.");
        var data = await Request(HttpMethod.Get, "checkout/sessions/" + Uri.EscapeDataString(order.SessionId));
        if (data.GetProperty("client_reference_id").GetString() != order.Id || data.GetProperty("status").GetString() != "open")
            throw new InvalidOperationException("Checkout is no longer open. Refresh payment status.");
        return data.GetProperty("client_secret").GetString() ?? throw new InvalidOperationException("Missing checkout client secret.");
    }
    public async Task<string> Create(Order order)
    {
        if (order.Provider != "Stripe" || order.PaymentStatus != "Pending")
            throw new InvalidOperationException("Order is not awaiting Stripe payment.");
        if (order.PaymentUrl != null)
            return order.PaymentUrl;
        if (!PublishableKey.StartsWith("pk_test_")) throw new InvalidOperationException("Configure Stripe:PublishableKey with your test publishable key.");
        var origin = (config["Payments:BaseUrl"] ?? "http://localhost:5080").TrimEnd('/');
        var form = new Dictionary<string, string>{{"mode","payment"},{"payment_method_types[0]","card"},{"client_reference_id",order.Id},{"metadata[orderId]",order.Id},
  {"ui_mode","embedded"},{"return_url",origin+"/Checkout/Result/"+order.Id+"?session_id={CHECKOUT_SESSION_ID}"}};
        for (int i = 0; i < order.Items.Count; i++)
        {
            var item = order.Items[i];
            var prefix = $"line_items[{i}]";
            form[prefix + "[price_data][currency]"] = "cad";
            form[prefix + "[price_data][product_data][name]"] = item.Name;
            form[prefix + "[price_data][unit_amount]"] = checked((long)(item.Price * 100)).ToString();
            form[prefix + "[quantity]"] = item.Quantity.ToString();
        }
        var data = await Request(HttpMethod.Post, "checkout/sessions", form, "campus-" + order.Id);
        var url = "/Checkout/Payment/" + order.Id;
        store.AttachSession(order.Id, data.GetProperty("id").GetString()!, url);
        return url;
    }
    public async Task Refresh(Order order)
    {
        if (order.Provider != "Stripe" || order.SessionId == null)
            return;
        var data = await Request(HttpMethod.Get, "checkout/sessions/" + Uri.EscapeDataString(order.SessionId));
        if (data.GetProperty("livemode").GetBoolean() || data.GetProperty("client_reference_id").GetString() != order.Id || data.GetProperty("currency").GetString() != "cad" || data.GetProperty("amount_total").GetInt64() != checked((long)(order.Total * 100)))
            throw new InvalidOperationException("Stripe payment verification failed.");
        if (data.GetProperty("payment_status").GetString() == "paid")
            store.Paid(order.Id, "Stripe", order.SessionId, order.Total, order.SessionId);
        else if (data.GetProperty("status").GetString() == "expired")
            store.Cancel(order.Id);
    }
    public async Task Cancel(Order order)
    {
        if (order.PaymentStatus != "Pending")
            return;
        if (order.SessionId == null)
        {
            await Create(order);
            order = store.GetOrder(order.Id)!;
        }
        await Refresh(order);
        order = store.GetOrder(order.Id)!;
        if (order.PaymentStatus != "Pending")
            return;
        await Request(HttpMethod.Post, "checkout/sessions/" + Uri.EscapeDataString(order.SessionId!) + "/expire", new());
        await Refresh(order);
    }
    public bool Verify(string body, string header)
    {
        var secret = config["Stripe:WebhookSecret"];
        if (string.IsNullOrEmpty(secret))
            return false;
        var parts = header.Split(',').Select(x => x.Trim().Split('=', 2)).Where(x => x.Length == 2).ToArray();
        var timestamp = parts.FirstOrDefault(x => x[0] == "t")?[1];
        if (!long.TryParse(timestamp, out var seconds) || Math.Abs((decimal)DateTimeOffset.UtcNow.ToUnixTimeSeconds() - seconds) > 300)
            return false;
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(timestamp + "." + body));
        foreach (var p in parts.Where(x => x[0] == "v1"))
            try
            {
                if (CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(p[1])))
                    return true;
            }
            catch (FormatException) { }
        return false;
    }
}
