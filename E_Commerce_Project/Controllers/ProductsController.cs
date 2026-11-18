using E_Commerce_Project.Data;
using Microsoft.AspNetCore.Mvc;
namespace E_Commerce_Project.Controllers;

public sealed class ProductsController(IProductRepository store) : Controller
{
    public async Task<IActionResult> Index(string? search, string? category, string? sort)
    {
        var data = (await store.GetAllAsync()).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
            data = data.Where(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || p.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(category))
            data = data.Where(p => p.Category == category);
        data = sort == "price" ? data.OrderBy(p => p.Price) : sort == "price-desc" ? data.OrderByDescending(p => p.Price) : data.OrderBy(p => p.Name);
        ViewBag.Search = search;
        ViewBag.Category = category;
        ViewBag.Sort = sort;
        return View(data.ToArray());
    }
    public async Task<IActionResult> Details(string id)
    {
        var p = (await store.GetAllAsync()).FirstOrDefault(p => p.Id == id);
        return p == null ? NotFound() : View(p);
    }
}
