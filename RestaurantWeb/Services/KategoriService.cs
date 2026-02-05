// Kategori iş akışlarını ve controller’a özel VM hazırlığını yapar.
// DB erişimi repo’da; servis katmanı validasyon/orkestrasyon ve iş kuralı kararlarını toplar.

using RestaurantWeb.Data;
using RestaurantWeb.Models;
using RestaurantWeb.Models.ViewModels;

namespace RestaurantWeb.Services
{
    public class KategoriService : IKategoriService
    {
        private readonly KategoriRepository _repo;

        public KategoriService(KategoriRepository repo)
        {
            _repo = repo;
        }

        // İnce servis: listeleme/tekil getirme doğrudan repo üzerinden
        public OperationResult<List<Kategori>> GetAll() => _repo.GetAll(); 
        public OperationResult<Kategori> GetById(int id) => _repo.GetById(id); 

        public OperationResult Add(string ad)
        {
            ad = (ad ?? "").Trim(); 
            return _repo.Add(ad);   
        }

        public OperationResult Update(int id, string ad)
        {
            ad = (ad ?? "").Trim(); 
            return _repo.Update(id, ad); 
        }

        // Silme ekranı için: kategori + bağlı ürün preview verisini tek VM’de toplar
        public OperationResult<KategoriDeleteVm> GetDeleteVm(int id, int previewLimit = 20) 
        {
            // 1) Kategori var mı?
            var katRes = _repo.GetById(id);
            if (!katRes.Success || katRes.Data == null)
                return OperationResult<KategoriDeleteVm>.Fail(katRes.Message);

            // 2) Ürün bağlı mı?
            var hasRes = _repo.HasProducts(id);
            if (!hasRes.Success)
                return OperationResult<KategoriDeleteVm>.Fail(hasRes.Message);

            // 3) Preview için bağlı ürünleri limitli getir
            var prodRes = _repo.GetProductsByKategoriId(id, previewLimit);
            if (!prodRes.Success)
                return OperationResult<KategoriDeleteVm>.Fail(prodRes.Message);

            var vm = new KategoriDeleteVm
            {
                Kategori = katRes.Data,
                HasProducts = hasRes.Data == true,
                BagliUrunler = prodRes.Data ?? new List<Urun>()
            };

            return OperationResult<KategoriDeleteVm>.Ok(vm); 
        }

        // İş kuralı: kategori ürün bağlıysa silinemez
        public OperationResult DeleteIfNoProducts(int id) 
        {
            var hasRes = _repo.HasProducts(id);
            if (!hasRes.Success)
                return OperationResult.Fail(hasRes.Message);

            if (hasRes.Data == true)
                return OperationResult.Fail("Bu kategoriye bağlı ürünler bulunduğu için silinemez.");

            return _repo.Delete(id);
        }

        public OperationResult ToggleAktif(int id) => _repo.ToggleAktif(id); 
    }
}
