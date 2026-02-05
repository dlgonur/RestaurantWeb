// Rezervasyonları listeleme ve durum aksiyonlarını (iptal / kullanıldı işaretleme) yönetir.
// Listeleme tarafında filtre/limit normalizasyonu yapar; iş kuralları RezervasyonService katmanındadır.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantWeb.Models.ViewModels;
using RestaurantWeb.Services;

namespace RestaurantWeb.Controllers
{
    [Authorize(Roles = "Admin,Kasa")] 
    public class RezervasyonlarController : Controller 
    {
        private readonly IRezervasyonService _service; 
        private readonly ILogger<RezervasyonlarController> _logger; 

        public RezervasyonlarController(IRezervasyonService service, ILogger<RezervasyonlarController> logger) 
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index(DateTime? baslangic, DateTime? bitis, short? durum, int? masaNo, string? q, int? limit)
        {
            // limit normalize 
            var lim = limit ?? 200; 
            if (lim <= 0) lim = 200; 
            if (lim > 1000) lim = 1000; 

            // UI swap (filtre alanları düzgün dönsün) 
            if (baslangic.HasValue && bitis.HasValue && bitis.Value.Date < baslangic.Value.Date)
                (baslangic, bitis) = (bitis, baslangic);

            var vm = new RezervasyonListVm
            {
                Baslangic = baslangic,
                Bitis = bitis,
                Durum = durum,
                MasaNo = masaNo,
                Q = q,
                Limit = lim
            };

            var res = _service.GetList(baslangic, bitis, masaNo, durum, q, lim); 
            if (!res.Success || res.Data == null)
            {
                TempData["Error"] = res.Message;
                vm.Items = new();
                return View(vm);
            }

            vm.Items = res.Data;

            if (vm.Items.Count == 0)
                TempData["Info"] = "Filtreye uygun rezervasyon bulunamadı.";

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Cancel(int id, string? returnUrl = null) 
        {
            try
            {
                var res = _service.Cancel(id);
                TempData[res.Success ? "Success" : "Error"] = res.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rezervasyon iptal edilirken hata. Id={Id}", id);
                TempData["Error"] = "Teknik bir hata oluştu.";
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) 
                return Redirect(returnUrl); 

            return RedirectToAction("Index"); 
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkUsed(int id, string? returnUrl = null) 
        {
            var res = _service.MarkUsed(id); 
            TempData[res.Success ? "Success" : "Error"] = res.Message;

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index");
        }

    }
}
