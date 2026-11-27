using E_Commerce_Project.Data;
using E_Commerce_Project.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
namespace E_Commerce_Project.Controllers;

[Authorize(Roles = Roles.Admin)]
public sealed class AdminController(MongoStore store) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public IActionResult Index() => View(store.Users().Select(u => new AdminUser(u.Id, u.FullName, u.Email, u.Role, u.Contact, u.Active)).ToArray());
    [HttpPost]
    public IActionResult Status(string id, bool active)
    {
        try
        {
            store.SetActive(UserId, id, active);
        }
        catch (InvalidOperationException e) { TempData["Error"] = e.Message; }
        return RedirectToAction(nameof(Index));
    }
    public IActionResult Products() => View(store.AllProducts());
    [HttpPost]
    public IActionResult Visibility(string id, bool visible)
    {
        try
        {
            store.Visibility(UserId, id, visible);
        }
        catch (InvalidOperationException e) { TempData["Error"] = e.Message; }
        return RedirectToAction(nameof(Products));
    }
    public IActionResult Orders() => View(store.Orders());
    public IActionResult Activity() => View(store.Logs());
}
public sealed record AdminUser(string Id, string FullName, string Email, string Role, string Contact, bool Active);
