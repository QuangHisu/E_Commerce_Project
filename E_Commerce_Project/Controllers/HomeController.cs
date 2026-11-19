using Microsoft.AspNetCore.Mvc;
namespace E_Commerce_Project.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
    public IActionResult Privacy() => View();
}
