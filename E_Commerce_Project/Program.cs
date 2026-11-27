using E_Commerce_Project.Data;
using E_Commerce_Project.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
if (!builder.Environment.IsDevelopment())
    throw new InvalidOperationException("This learning application currently requires the Development environment.");
var mode = builder.Configuration["Payments:Mode"] ?? "Demo";
if (mode != "Demo" && mode != "Stripe") throw new InvalidOperationException("Payments:Mode must be Demo or Stripe.");
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddSingleton<IPasswordHasher<ApplicationUser>, PasswordHasher<ApplicationUser>>();
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<MongoStore>();
builder.Services.AddSingleton<IUserRepository>(sp => sp.GetRequiredService<MongoStore>());
builder.Services.AddSingleton<IProductRepository>(sp => sp.GetRequiredService<MongoStore>());
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Events.OnValidatePrincipal = async context =>
        {
            var id = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var user = id == null ? null : context.HttpContext.RequestServices.GetRequiredService<MongoStore>().UserById(id);
            if (user == null || !user.Active || context.Principal?.FindFirst("security_stamp")?.Value != user.SecurityStamp)
            {
                context.RejectPrincipal();
                await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.SignOutAsync(context.HttpContext);
            }
        };
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "E_Commerce_Project.FullDemo";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = false;
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});
builder.Services.AddHttpClient<E_Commerce_Project.Services.StripePayments>(c => c.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddHttpClient<E_Commerce_Project.Services.RecoveryEmail>(c => c.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddSingleton<E_Commerce_Project.Services.ImageStorage>();
var app = builder.Build();
try
{
    app.Services.GetRequiredService<MongoContext>().Initialize();
    if (args.Contains("--seed"))
    {
        var result = app.Services.GetRequiredService<MongoStore>().Seed(
            builder.Configuration["Seed:Password"] ?? "",
            app.Services.GetRequiredService<IPasswordHasher<ApplicationUser>>());
        Console.WriteLine($"Seed complete: {result.UsersAdded} users and {result.ProductsAdded} products added. Existing records preserved.");
        return;
    }
}
catch (MongoDB.Driver.MongoException)
{
    Console.Error.WriteLine("MongoDB initialization failed. Check the connection string, database permissions, Atlas IP access list and replica set. Credentials are not printed.");
    Environment.ExitCode = 1;
    return;
}
catch (TimeoutException)
{
    Console.Error.WriteLine("MongoDB connection timed out. Check network access and the configured server.");
    Environment.ExitCode = 1;
    return;
}
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
