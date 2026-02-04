using RestaurantWeb.Models;

namespace RestaurantWeb.Services
{
    public interface IPersonelLogService
    {
        OperationResult<List<PersonelLog>> GetList(
            DateTime? baslangic,
            DateTime? bitis,
            string? aksiyon,
            string? targetKullaniciAdi,
            int limit = 200
        ); // ★
    }
}
