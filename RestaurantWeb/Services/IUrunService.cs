using RestaurantWeb.Models;
using RestaurantWeb.Models.ViewModels;

namespace RestaurantWeb.Services
{
    public interface IUrunService
    {
        OperationResult<List<Urun>> GetAllWithKategori(); // ★
        OperationResult<Urun> GetByIdWithKategori(int id); // ★

        OperationResult Add(int kategoriId, string ad, decimal fiyat, int stok,
            byte[]? resimBytes, string? resimMime, string? resimAdi); // ★

        OperationResult Update(int id, int kategoriId, string ad, decimal fiyat, int stok,
            byte[]? resimBytes, string? resimMime, string? resimAdi, bool resimGuncellensin); // ★

        OperationResult Delete(int id); // ★
        OperationResult<bool> ToggleAktif(int id); // ★

        OperationResult<(byte[] Bytes, string Mime)> GetResim(int id); // ★
    }
}
