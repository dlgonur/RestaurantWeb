using RestaurantWeb.Models;
using RestaurantWeb.Models.ViewModels;

namespace RestaurantWeb.Services
{
    public interface IRezervasyonService
    {
        OperationResult Create(RezervasyonCreateVm model); 
        OperationResult Cancel(int rezervasyonId); 
        OperationResult<List<RezervasyonListItemVm>> GetList(
            DateTime? baslangic, DateTime? bitis, int? masaNo, short? durum, string? q, int limit);

        OperationResult MarkUsed(int rezervasyonId); 
    }

}
