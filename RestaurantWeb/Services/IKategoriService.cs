using RestaurantWeb.Models;
using RestaurantWeb.Models.ViewModels;

namespace RestaurantWeb.Services
{
    public interface IKategoriService
    {
        OperationResult<List<Kategori>> GetAll(); 
        OperationResult<Kategori> GetById(int id); 

        OperationResult Add(string ad); 
        OperationResult Update(int id, string ad); 

        OperationResult<KategoriDeleteVm> GetDeleteVm(int id, int previewLimit = 20); 
        OperationResult DeleteIfNoProducts(int id); 

        OperationResult ToggleAktif(int id); 
    }
}
