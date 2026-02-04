using RestaurantWeb.Data;
using RestaurantWeb.Models;
using RestaurantWeb.Models.Dtos;

namespace RestaurantWeb.Services
{
    public class CatalogService : ICatalogService
    {
        private readonly CatalogRepository _repo;

        public CatalogService(CatalogRepository repo)
        {
            _repo = repo;
        }

        public OperationResult<List<CatalogCategoryDto>> GetActiveCategories() // ★
            => _repo.GetActiveCategories();

        public OperationResult<List<CatalogProductDto>> GetActiveProducts(int? kategoriId) // ★
            => _repo.GetActiveProducts(kategoriId);
    }
}
