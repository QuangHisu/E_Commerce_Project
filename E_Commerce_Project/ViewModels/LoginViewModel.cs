using System.ComponentModel.DataAnnotations;
namespace E_Commerce_Project.ViewModels;

public sealed class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";
    public string? ReturnUrl
    {
        get; set;
    }
}
