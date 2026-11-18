using E_Commerce_Project.Data;
using E_Commerce_Project.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
namespace E_Commerce_Project.Controllers;
[Authorize(Roles = Roles.Buyer)]
public sealed class OrdersController(InMemoryStore store) : Controller
{
    public IActionResult Index() => View(store.Orders().Where(o => o.BuyerId == User.FindFirstValue(ClaimTypes.NameIdentifier)).ToArray());
    public IActionResult Details(string id)
    {
        var o = store.GetOrder(id);
        return o == null || o.BuyerId != User.FindFirstValue(ClaimTypes.NameIdentifier) ? NotFound() : View(o);
    }
}
