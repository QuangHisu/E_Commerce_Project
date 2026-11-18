using E_Commerce_Project.Data;
using E_Commerce_Project.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
namespace E_Commerce_Project.Controllers;
[Authorize(Roles = Roles.Buyer)]
public sealed class CartController(InMemoryStore store) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public IActionResult Index() => View(store.Cart(UserId));
    [HttpPost]
    public IActionResult Add(string id, int quantity = 1)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Enter a valid quantity.";
            return RedirectToAction(nameof(Index));
        }
        try
        {
            store.Quantity(UserId, id, quantity, true);
            TempData["Message"] = "Added to cart.";
        }
        catch (InvalidOperationException e) { TempData["Error"] = e.Message; }
        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    public IActionResult Update(string id, int quantity)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Enter a valid quantity.";
            return RedirectToAction(nameof(Index));
        }
        try
        {
            store.Quantity(UserId, id, quantity);
        }
        catch (InvalidOperationException e) { TempData["Error"] = e.Message; }
        return RedirectToAction(nameof(Index));
    }
}
