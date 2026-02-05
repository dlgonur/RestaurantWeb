// Menü/Katalog verilerini okuma amaçlı servis.

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

        // Aktif kategorileri döndürür (sipariş ekranı gibi "seçim" UI'ları için).
        public OperationResult<List<CatalogCategoryDto>> GetActiveCategories() 
            => _repo.GetActiveCategories();

        // Aktif ürünleri döndürür; kategoriId verilirse filtreli gelir.
        public OperationResult<List<CatalogProductDto>> GetActiveProducts(int? kategoriId) 
            => _repo.GetActiveProducts(kategoriId);
    }
}
