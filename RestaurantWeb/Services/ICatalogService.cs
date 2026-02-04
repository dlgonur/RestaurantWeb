using RestaurantWeb.Models;
using RestaurantWeb.Models.Dtos;

namespace RestaurantWeb.Services
{
    public interface ICatalogService
    {
        OperationResult<List<CatalogCategoryDto>> GetActiveCategories(); // ★
        OperationResult<List<CatalogProductDto>> GetActiveProducts(int? kategoriId); // ★
    }
}
