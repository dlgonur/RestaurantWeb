// Kategori yönetimi (CRUD + aktif/pasif) controller’ı.
// İş kuralları servis katmanında; controller akışı yönetir ve TempData ile UI mesajı verir.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantWeb.Models;
using RestaurantWeb.Models.ViewModels;
using RestaurantWeb.Services; 



namespace RestaurantWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class KategorilerController : Controller
    {
        private readonly IKategoriService _service; 
        private readonly ILogger<KategorilerController> _logger;

        public KategorilerController(IKategoriService service, ILogger<KategorilerController> logger) 
        {
            _service = service; 
            _logger = logger;
        }

        // Listeleme
        public IActionResult Index()
        {
            var result = _service.GetAll();

            if (!result.Success)
            {
                TempData["Error"] = result.Message;
                return View(new List<Kategori>());
            }

            if ((result.Data == null || result.Data.Count == 0) && !string.IsNullOrWhiteSpace(result.Message))
            {
                TempData["Info"] = result.Message;
            }
            return View(result.Data ?? new List<Kategori>());
            
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(KategoriCreateVm model) 
        {
            if (!ModelState.IsValid) 
            {
                TempData["Error"] = "Lütfen form alanlarını kontrol ediniz."; 
                return View(model); 
            }

            var ad = model.Ad.Trim(); 
            var result = _service.Add(ad);

            if (!result.Success)
            {
                _logger.LogWarning("Kategori ekleme başarısız. Ad: {Ad}. Mesaj: {Message}", ad, result.Message); 
                TempData["Error"] = result.Message;
                return View(model); 
            }

            TempData["Success"] = result.Message;
            return RedirectToAction("Index");
        }


        [HttpGet]
        public IActionResult Edit(int id)
        {
            var result = _service.GetById(id);

            if (!result.Success)
            {
                TempData["Error"] = result.Message;
                return RedirectToAction("Index");
            }

            return View(new KategoriEditVm { Id = result.Data!.Id, Ad = result.Data.Ad }); 

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, KategoriEditVm model) 
        {
            if (id != model.Id) 
            {
                TempData["Error"] = "İstek geçersiz (Id uyuşmazlığı)."; 
                return RedirectToAction("Index"); 
            }

            if (!ModelState.IsValid) 
            {
                TempData["Error"] = "Lütfen form alanlarını kontrol ediniz."; 
                return View(model); 
            }

            var ad = model.Ad.Trim(); 
            var result = _service.Update(id, ad);

            if (!result.Success)
            {
                _logger.LogWarning("Kategori güncelleme başarısız. Id: {Id}, Ad: {Ad}. Mesaj: {Message}", id, ad, result.Message); 
                TempData["Error"] = result.Message;
                return View(model); 
            }

            TempData["Success"] = result.Message;
            return RedirectToAction("Index");
        }

        // Silme önizleme ekranı (bağlı ürünleri gösterir)
        [HttpGet]
        public IActionResult Delete(int id)
        {
            var vmRes = _service.GetDeleteVm(id, previewLimit: 20); 
            if (!vmRes.Success || vmRes.Data == null) 
            {
                TempData["Error"] = vmRes.Message;
                return RedirectToAction("Index");
            }

            return View(vmRes.Data); 
        }

        // Silme (PRG) - ürün varsa silme engellenir ve Delete ekranında kalınır
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Delete")]
        public IActionResult DeleteConfirmed(int id)
        {
            var delRes = _service.DeleteIfNoProducts(id); 

            if (!delRes.Success)  
            {
                // Kullanıcıyı Index'e fırlatma; Delete ekranında kal 
                ModelState.AddModelError("", delRes.Message); 

                var vmRes = _service.GetDeleteVm(id, previewLimit: 20); 
                if (!vmRes.Success || vmRes.Data == null)
                {
                    TempData["Error"] = vmRes.Message;
                    return RedirectToAction("Index");
                }

                return View("Delete", vmRes.Data); 
            }

            TempData["Success"] = delRes.Message;
            return RedirectToAction("Index");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleAktif(int id)
        {
            var result = _service.ToggleAktif(id);

            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction("Index");
        }


    }


}
