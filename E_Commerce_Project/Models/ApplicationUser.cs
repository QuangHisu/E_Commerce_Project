namespace E_Commerce_Project.Models;

public sealed class ApplicationUser
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string FullName { get; set; } = "";
    public string Email { get; init; } = "";
    public string Role { get; init; } = Roles.Buyer;
    public string Contact { get; set; } = "";
    public bool Active { get; set; } = true;
    public string PasswordHash { get; set; } = "";
}
