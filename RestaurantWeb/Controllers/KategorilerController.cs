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

        // Modal için Edit içeriği (Partial)
        [HttpGet]
        public IActionResult EditModal(int id)
        {
            var result = _service.GetById(id);

            if (!result.Success || result.Data == null)
                return BadRequest(result.Message);

            var vm = new KategoriEditVm
            {
                Id = result.Data.Id,
                Ad = result.Data.Ad
            };

            return PartialView("_EditModalPartial", vm);
        }

        // Modal Edit submit (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditModal(KategoriEditVm model)
        {
            if (!ModelState.IsValid)
                return BadRequest("Lütfen form alanlarını kontrol ediniz.");

            var ad = model.Ad.Trim();
            var result = _service.Update(model.Id, ad);

            if (!result.Success)
                return BadRequest(result.Message);

            TempData["Success"] = result.Message;
            return Json(new { success = true });
        }

        // Modal için Delete içeriği (Partial)
        [HttpGet]
        public IActionResult DeleteModal(int id)
        {
            var vmRes = _service.GetDeleteVm(id, previewLimit: 20);
            if (!vmRes.Success || vmRes.Data == null)
                return BadRequest(vmRes.Message);

            return PartialView("_DeleteModalPartial", vmRes.Data);
        }

        // Modal Delete submit (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteModalConfirmed(int id)
        {
            var delRes = _service.DeleteIfNoProducts(id);

            if (!delRes.Success)
                return BadRequest(delRes.Message);

            TempData["Success"] = delRes.Message;
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleAktif(int id)
        {
            var result = _service.ToggleAktif(id);

            // AJAX (fetch) isteği ise redirect değil JSON dön
            var isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase); 
            if (isAjax) 
            {
                return Json(new { success = result.Success, message = result.Message }); 
            }

            // Normal form submit fallback (JS kapalıysa da çalışsın)
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction("Index");
        }


    }


}
