using E_Commerce_Project.Models;
namespace E_Commerce_Project.Data;

public interface IUserRepository
{
    Task<ApplicationUser?> FindByEmailAsync(string email);
    Task<IReadOnlyList<ApplicationUser>> GetAllAsync();
}
