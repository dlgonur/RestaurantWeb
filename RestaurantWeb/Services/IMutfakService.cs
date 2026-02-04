using RestaurantWeb.Models;
using RestaurantWeb.Models.ViewModels;

namespace RestaurantWeb.Services
{
    public interface IMutfakService
    {
        OperationResult<List<MutfakSiparisVm>> GetPendingOrders();
        OperationResult SetItemStatus(int kalemId, short durum, string? actorUsername);
    }
}
