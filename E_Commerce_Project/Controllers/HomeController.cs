using Microsoft.AspNetCore.Mvc;
namespace CampusMarket.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
    public IActionResult Privacy() => View();
}
