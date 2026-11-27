using E_Commerce_Project.Data;
using E_Commerce_Project.Models;
using E_Commerce_Project.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
namespace E_Commerce_Project.Controllers;

public sealed class AccountController(IUserRepository users, IPasswordHasher<ApplicationUser> hasher, InMemoryStore store, E_Commerce_Project.Services.RecoveryEmail email, ILogger<AccountController> logger) : Controller
{
    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordForm());
    [HttpPost, EnableRateLimiting("login")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordForm model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await users.FindByEmailAsync(model.Email);
        if (user is { Active: true })
        {
            var token = store.IssuePasswordReset(user.Id);
            if (token != null)
                try { await email.Send(user.Email, user.Id, token); }
                catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException)
                {
                    store.RevokeReset(user.Id, token);
                    logger.LogWarning("Password reset email could not be sent. Check email provider configuration and availability.");
                }
        }
        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }
    [HttpGet] public IActionResult ForgotPasswordConfirmation() => View();
    [HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult ResetPassword(string? userId, string? token)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token)) return View("ResetInvalid");
        return View(new ResetPasswordForm { UserId = userId, Token = token });
    }
    [HttpPost, EnableRateLimiting("login"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ResetPassword(ResetPasswordForm model)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (!ModelState.IsValid) return View(model);
        if (!store.ResetPassword(model.UserId, model.Token, model.Password, hasher)) return View("ResetInvalid");
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Message"] = "Your password has been reset. Please log in with your new password.";
        return RedirectToAction(nameof(Login));
    }
    [HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });
    [HttpPost, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);
        var user = await users.FindByEmailAsync(model.Email);
        if (user is null || !user.Active || hasher.VerifyHashedPassword(user, user.PasswordHash, model.Password) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("", "The email or password is incorrect.");
            model.Password = "";
            ModelState.Remove(nameof(model.Password));
            return View(model);
        }
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Name, user.FullName), new Claim(ClaimTypes.Role, user.Role), new Claim("security_stamp", user.SecurityStamp) };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        if (Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl!);
        return RedirectToAction("Index", user.Role);
    }
    [HttpPost, Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    [HttpGet] public IActionResult Register() => View(new RegisterForm());
    [HttpPost, EnableRateLimiting("login")]
    public IActionResult Register(RegisterForm model)
    {
        if (!ModelState.IsValid)
            return View(model);
        var u = new ApplicationUser { FullName = model.FullName.Trim(), Email = model.Email.Trim().ToLowerInvariant(), Role = model.Role, Contact = model.Email.Trim() };
        u.PasswordHash = hasher.HashPassword(u, model.Password);
        if (!store.Register(u))
        {
            ModelState.AddModelError("", "This email is already registered.");
            return View(model);
        }
        TempData["Message"] = "Account created. Please log in.";
        return RedirectToAction(nameof(Login));
    }
    [Authorize, HttpGet]
    public IActionResult Profile()
    {
        var u = store.UserById(User.FindFirstValue(ClaimTypes.NameIdentifier)!)!;
        return View(new ProfileForm { FullName = u.FullName, Contact = string.IsNullOrWhiteSpace(u.Contact) ? u.Email : u.Contact });
    }
    [Authorize, HttpPost]
    public IActionResult Profile(ProfileForm model)
    {
        if (!ModelState.IsValid)
            return View(model);
        store.Profile(User.FindFirstValue(ClaimTypes.NameIdentifier)!, model.FullName.Trim(), model.Contact.Trim());
        TempData["Message"] = "Profile saved. Log in again to refresh your greeting.";
        return RedirectToAction(nameof(Profile));
    }
}
