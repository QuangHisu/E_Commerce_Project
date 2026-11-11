using E_Commerce_Project.Models;
namespace E_Commerce_Project.Data;

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetAllAsync();
    Task<Product?> GetByIdAsync(string id);
    Task<IReadOnlyList<Product>> GetBySellerAsync(string sellerId);
}
