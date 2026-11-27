using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
namespace E_Commerce_Project.Services;

public sealed class RecoveryEmail(HttpClient client, IConfiguration config)
{
    public async Task Send(string email, string userId, string token)
    {
        var key = config["Email:ApiKey"];
        var from = config["Email:From"];
        var origin = config["Payments:BaseUrl"];
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(from) ||
            !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)))
            throw new InvalidOperationException("Configure Email:ApiKey, Email:From and Payments:BaseUrl.");
        var link = origin!.TrimEnd('/') + "/Account/ResetPassword?userId=" + Uri.EscapeDataString(userId) + "&token=" + Uri.EscapeDataString(token);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = JsonContent.Create(new
        {
            from,
            to = new[] { email },
            subject = "Reset your CampusMarket password",
            html = "<p>A password reset was requested for your CampusMarket account.</p><p><a href=\"" + HtmlEncoder.Default.Encode(link) + "\">Reset password</a></p><p>This link expires in 20 minutes and works once. If you did not request this, ignore this email.</p>",
            text = "Reset your CampusMarket password: " + link + "\nExpires in 20 minutes. If you did not request this, ignore this email."
        });
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}
