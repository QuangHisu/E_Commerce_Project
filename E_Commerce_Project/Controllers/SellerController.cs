using E_Commerce_Project.Data;
using E_Commerce_Project.Models;
using E_Commerce_Project.ViewModels;
using E_Commerce_Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
namespace E_Commerce_Project.Controllers;

[Authorize(Roles = Roles.Seller)]
public sealed class SellerController(MongoStore store, ImageStorage images) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public async Task<IActionResult> Index() => View(await store.GetBySellerAsync(UserId));
    [HttpGet]
    public async Task<IActionResult> Edit(string? id)
    {
        if (id == null)
            return View(new ProductForm());
        var p = await store.GetByIdAsync(id);
        if (p == null || p.SellerId != UserId || p.Archived)
            return NotFound();
        return View(new ProductForm { Id = p.Id, Name = p.Name, Description = p.Description, Category = p.Category, Price = p.Price, Stock = p.Stock, ExistingImage = p.ImageUrl });
    }
    [HttpPost, RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> Edit(ProductForm model)
    {
        var old = model.Id == null ? null : await store.GetByIdAsync(model.Id);
        if (model.Id != null && (old == null || old.SellerId != UserId || old.Archived))
            return NotFound();
        model.ExistingImage = old?.ImageUrl;
        if (decimal.Round(model.Price, 2) != model.Price)
            ModelState.AddModelError(nameof(model.Price), "Use at most two decimal places.");
        if (!ModelState.IsValid)
            return View(model);
        try
        {
            var image = await images.Save(model.Picture, old?.ImageUrl);
            store.SaveProduct(UserId, model, image);
            TempData["Message"] = "Product saved.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException e) { ModelState.AddModelError("", e.Message); return View(model); }
    }
    [HttpGet]
    public async Task<IActionResult> Delete(string id)
    {
        var p = await store.GetByIdAsync(id);
        return p == null || p.SellerId != UserId || p.Archived ? NotFound() : View(p);
    }
    [HttpPost, ActionName("Delete")]
    public IActionResult ConfirmDelete(string id)
    {
        try
        {
            store.DeleteProduct(UserId, id);
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException) { return NotFound(); }
    }
    public IActionResult Orders() => View(store.Orders().Where(o => o.PaymentStatus == "Paid" && o.Items.Any(i => i.SellerId == UserId)).Select(o => { o.Items = o.Items.Where(i => i.SellerId == UserId).ToList(); return o; }).ToArray());
    [HttpPost]
    public IActionResult Fulfill(string id, string status)
    {
        try
        {
            store.Fulfill(UserId, id, status);
        }
        catch (InvalidOperationException e) { TempData["Error"] = e.Message; }
        return RedirectToAction(nameof(Orders));
    }
}
