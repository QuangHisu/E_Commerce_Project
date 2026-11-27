using System.ComponentModel.DataAnnotations;
namespace E_Commerce_Project.ViewModels;

public sealed class ForgotPasswordForm
{
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
}
public sealed class ResetPasswordForm
{
    [Required, StringLength(128)] public string UserId { get; set; } = "";
    [Required, StringLength(128)] public string Token { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 12), DataType(DataType.Password)] public string Password { get; set; } = "";
    [Required, Compare(nameof(Password)), DataType(DataType.Password), Display(Name = "Confirm password")] public string ConfirmPassword { get; set; } = "";
}
