using E_Commerce_Project.Data;
using E_Commerce_Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
namespace E_Commerce_Project.Controllers;

[AllowAnonymous]
public sealed class StripeWebhookController(InMemoryStore store, StripePayments payments) : Controller
{
    [HttpPost("/payments/stripe/webhook"), IgnoreAntiforgeryToken, RequestSizeLimit(1048576)]
    public async Task<IActionResult> Receive()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        if (!payments.Verify(body, Request.Headers["Stripe-Signature"].ToString()))
            return BadRequest();
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();
            if (type is not ("checkout.session.completed" or "checkout.session.async_payment_succeeded" or "checkout.session.expired"))
                return Ok();
            var obj = root.GetProperty("data").GetProperty("object");
            var id = obj.GetProperty("client_reference_id").GetString();
            var o = id == null ? null : store.GetOrder(id);
            if (o == null)
                return Ok();
            if (o.SessionId == null)
                return StatusCode(503);
            if (o.SessionId != obj.GetProperty("id").GetString())
                return BadRequest();
            await payments.Refresh(o);
            return Ok();
        }
        catch (JsonException) { return BadRequest(); }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException) { return StatusCode(503); }
    }
}
