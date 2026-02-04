using RestaurantWeb.Data;
using RestaurantWeb.Models;

namespace RestaurantWeb.Services
{
    public class UrunService : IUrunService
    {
        private readonly UrunRepository _repo;

        public UrunService(UrunRepository repo)
        {
            _repo = repo;
        }

        public OperationResult<List<Urun>> GetAllWithKategori() => _repo.GetAllWithKategori(); 
        public OperationResult<Urun> GetByIdWithKategori(int id) => _repo.GetByIdWithKategori(id); 

        public OperationResult Add(int kategoriId, string ad, decimal fiyat, int stok,
            byte[]? resimBytes, string? resimMime, string? resimAdi) 
        {
            ad = (ad ?? "").Trim(); 
            return _repo.Add(kategoriId, ad, fiyat, stok, resimBytes, resimMime, resimAdi); 
        }

        public OperationResult Update(int id, int kategoriId, string ad, decimal fiyat, int stok,
            byte[]? resimBytes, string? resimMime, string? resimAdi, bool resimGuncellensin) 
        {
            ad = (ad ?? "").Trim(); 
            return _repo.Update(id, kategoriId, ad, fiyat, stok, resimBytes, resimMime, resimAdi, resimGuncellensin); 
        }

        public OperationResult Delete(int id) => _repo.Delete(id); 
        public OperationResult<bool> ToggleAktif(int id) => _repo.ToggleAktif(id); 
        public OperationResult<(byte[] Bytes, string Mime)> GetResim(int id) => _repo.GetResimByUrunId(id); 


    }
}
