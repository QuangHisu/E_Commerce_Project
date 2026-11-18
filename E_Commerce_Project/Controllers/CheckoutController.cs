using E_Commerce_Project.Data;
using E_Commerce_Project.Models;
using E_Commerce_Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
namespace E_Commerce_Project.Controllers;
[Authorize(Roles = Roles.Buyer)]
public sealed class CheckoutController(InMemoryStore store, StripePayments payments) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private Order? Own(string id)
    {
        var o = store.GetOrder(id);
        return o?.BuyerId == UserId ? o : null;
    }
    public IActionResult Index()
    {
        ViewBag.Mode = payments.Mode;
        return View(store.Cart(UserId));
    }
    [HttpPost]
    public async Task<IActionResult> Start()
    {
        Order o;
        try
        {
            o = store.BeginOrder(UserId, payments.Mode);
        }
        catch (InvalidOperationException e) { TempData["Error"] = e.Message; return RedirectToAction("Index", "Cart"); }
        return await Continue(o);
    }
    private async Task<IActionResult> Continue(Order o)
    {
        if (o.PaymentStatus != "Pending")
            return RedirectToAction(nameof(Result), new
            {
                id = o.Id
            });
        if (o.Provider == "Demo")
            return RedirectToAction(nameof(Demo), new
            {
                id = o.Id
            });
        try
        {
            return Redirect(await payments.Create(o));
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException or JsonException) { TempData["Error"] = "Payment could not start. " + (e is InvalidOperationException ? e.Message : "Check your connection and retry."); return RedirectToAction(nameof(Result), new { id = o.Id }); }
    }
    public IActionResult Demo(string id)
    {
        var o = Own(id);
        return o == null || o.Provider != "Demo" ? NotFound() : View(o);
    }
    [HttpPost]
    public IActionResult DemoPay(string id)
    {
        var o = Own(id);
        if (o == null || o.Provider != "Demo" || payments.Mode != "Demo")
            return NotFound();
        try
        {
            store.Paid(id, "Demo", "demo-" + id, o.Total);
        }
        catch (InvalidOperationException e) { TempData["Error"] = e.Message; }
        return RedirectToAction(nameof(Result), new
        {
            id
        });
    }
    public async Task<IActionResult> Result(string id, string? session_id = null)
    {
        var o = Own(id);
        if (o == null)
            return NotFound();
        if (session_id != null && session_id == o.SessionId)
            await Check(o);
        return View(store.GetOrder(id));
    }
    private async Task Check(Order o)
    {
        try
        {
            await payments.Refresh(o);
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException or JsonException) { TempData["Error"] = "Payment status could not be verified yet. Refresh the status or retry later."; }
    }
    [HttpPost]
    public async Task<IActionResult> Refresh(string id)
    {
        var o = Own(id);
        if (o == null)
            return NotFound();
        await Check(o);
        return RedirectToAction(nameof(Result), new
        {
            id
        });
    }
    [HttpPost]
    public async Task<IActionResult> Retry(string id)
    {
        var o = Own(id);
        return o == null ? NotFound() : await Continue(o);
    }
    [HttpPost]
    public async Task<IActionResult> Cancel(string id)
    {
        var o = Own(id);
        if (o == null)
            return NotFound();
        try
        {
            if (o.Provider == "Demo")
                store.Cancel(id);
            else
                await payments.Cancel(o);
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException or JsonException) { TempData["Error"] = "Cancellation could not be verified. Stock stays reserved. Refresh the payment status and retry."; }
        return RedirectToAction(nameof(Result), new
        {
            id
        });
    }
}
