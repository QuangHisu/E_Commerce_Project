using System.ComponentModel.DataAnnotations;
namespace E_Commerce_Project.ViewModels;

public sealed class RegisterForm
{
    [Required, StringLength(80)] public string FullName { get; set; } = "";
    [Required, EmailAddress, StringLength(160)] public string Email { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 10), DataType(DataType.Password)] public string Password { get; set; } = "";
    [Compare(nameof(Password)), DataType(DataType.Password)] public string Confirm { get; set; } = "";
    [Required, RegularExpression("^(Buyer|Seller)$")] public string Role { get; set; } = "Buyer";
}
public sealed class ProfileForm
{
    [Required, StringLength(80)] public string FullName { get; set; } = "";
    [Required, StringLength(160)] public string Contact { get; set; } = "";
}
public sealed class ProductForm
{
    public string? Id
    {
        get; set;
    }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(1000)] public string Description { get; set; } = "";
    [Required, RegularExpression("^(Stationery|Bags|Desk accessories)$")] public string Category { get; set; } = "Stationery";
    [Range(typeof(decimal), "0.50", "10000")] public decimal Price { get; set; } = 1;
    [Range(0, 10000)]
    public int Stock
    {
        get; set;
    }
    public IFormFile? Picture
    {
        get; set;
    }
    public string? ExistingImage
    {
        get; set;
    }
}
