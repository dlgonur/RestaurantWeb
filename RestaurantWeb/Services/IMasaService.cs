using RestaurantWeb.Models;
using RestaurantWeb.Models.ViewModels;

namespace RestaurantWeb.Services
{
    public interface IMasaService
    {
        OperationResult<List<Masa>> GetAll();
        OperationResult<Masa> GetById(int id);

        OperationResult Add(int masaNo, int kapasite);
        OperationResult Update(int id, int masaNo, int kapasite);
        OperationResult<bool> ToggleAktif(int id);
        OperationResult Delete(int id);
        OperationResult<MasaBoardVm> GetBoard(DateTime now); 
        OperationResult<int> EnsureOpenTable(int masaId, DateTime now); 
    }
}
