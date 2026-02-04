using RestaurantWeb.Models;
using RestaurantWeb.Models.ViewModels;
using RestaurantWeb.Models.Dtos;

namespace RestaurantWeb.Services
{
    public interface ISiparisService
    {
        OperationResult<int> GetMasaNoById(int masaId);
        OperationResult<int?> GetOpenOrderId(int masaId);
        OperationResult<SiparisAdisyonVm> GetSiparisAdisyon(int siparisId);

        OperationResult SubmitOrder(int siparisId, List<(int UrunId, int Adet)> items);
        OperationResult UpdateDiscountRate(int siparisId, decimal iskontoOran, string? actorUsername);
        OperationResult CloseOrderWithPayment(int siparisId, OdemeYontemi yontem, string? actorUsername, int kapatanPersonelId);

        OperationResult<List<SiparisListItemVm>> GetOrders(DateTime baslangic, DateTime bitis, int? masaNo);
        OperationResult<SiparisDetayVm> GetOrderDetail(int siparisId);

        OperationResult<List<SiparisLogItemVm>> GetLogs(int siparisId);

        OperationResult<List<ProductListItemDto>> GetActiveProducts(int? kategoriId = null); 
        OperationResult<List<CategoryItemDto>> GetActiveCategories();
    }
}
