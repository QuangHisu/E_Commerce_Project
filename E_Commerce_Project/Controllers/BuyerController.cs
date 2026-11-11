using E_Commerce_Project.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace E_Commerce_Project.Controllers;

[Authorize(Roles = Roles.Buyer)]
public sealed class BuyerController : Controller
{
    public IActionResult Index() => View();
}
