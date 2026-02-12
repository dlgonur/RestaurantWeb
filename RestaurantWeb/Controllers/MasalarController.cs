// Masa yönetimi (CRUD + aktif/pasif) ve operasyonel “Board” akışını yönetir.
// Garson/Kasa/Admin erişebilir; masa açma, rezervasyon oluşturma ve iptal akışları burada.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantWeb.Models;
using RestaurantWeb.Models.ViewModels;
using RestaurantWeb.Services; 

namespace RestaurantWeb.Controllers
{
    [Authorize(Roles = "Admin,Kasa,Garson")]
    public class MasalarController : Controller
    {
        private readonly IMasaService _service;
        private readonly IRezervasyonService _rezService;
        private readonly ILogger<MasalarController> _logger;

        public MasalarController(IMasaService service, IRezervasyonService rezService, ILogger<MasalarController> logger) 
        {
            _service = service;
            _rezService = rezService;
            _logger = logger;
        }

        // Masa CRUD listeleme (admin ağırlıklı ekran)
        public IActionResult Index()
        {
            var result = _service.GetAll(); 

            if (!result.Success)
            {
                TempData["Error"] = result.Message;
                return View(new List<Masa>());
            }

            if ((result.Data == null || result.Data.Count == 0) && !string.IsNullOrWhiteSpace(result.Message))
                TempData["Info"] = result.Message;

            return View(result.Data ?? new List<Masa>());
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new MasaCreateVm());
        }

        // Masa ekleme (PRG)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(MasaCreateVm model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Lütfen form alanlarını kontrol ediniz.";
                return View(model);
            }

            var result = _service.Add(model.MasaNo, model.Kapasite); 

            if (!result.Success)
            {
                _logger.LogWarning("Masa ekleme başarısız. MasaNo: {MasaNo}, Kapasite: {Kapasite}. Mesaj: {Message}",
                    model.MasaNo, model.Kapasite, result.Message);

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

            var vm = new MasaEditVm
            {
                Id = result.Data.Id,
                MasaNo = result.Data.MasaNo,
                Kapasite = result.Data.Kapasite,
                AktifMi = result.Data.AktifMi
            };

            return PartialView("_EditModalPartial", vm);
        }

        // Modal Edit submit (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditModal(MasaEditVm model)
        {
            if (!ModelState.IsValid)
                return BadRequest("Lütfen form alanlarını kontrol ediniz.");

            var result = _service.Update(model.Id, model.MasaNo, model.Kapasite);

            if (!result.Success)
                return BadRequest(result.Message);

            TempData["Success"] = result.Message;
            return Json(new { success = true });
        }

        // Modal için Delete içeriği (Partial)
        [HttpGet]
        public IActionResult DeleteModal(int id)
        {
            var result = _service.GetById(id);
            if (!result.Success || result.Data == null)
                return BadRequest(result.Message);

            return PartialView("_DeleteModalPartial", result.Data);
        }

        // Modal Delete submit (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteModalConfirmed(int id)
        {
            var result = _service.Delete(id);

            if (!result.Success)
                return BadRequest(result.Message);

            TempData["Success"] = result.Message;
            return Json(new { success = true });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Toggle(int id)
        {
            var result = _service.ToggleAktif(id);

            // AJAX ise JSON dön (sayfa yenilenmesin)
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                if (!result.Success)
                    return BadRequest(result.Message);

                return Json(new { success = true, message = result.Message, aktifMi = result.Data });
            }

            // Normal post (fallback)
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction("Index");
        }



        // Operasyon ekranı: masaların canlı durumu + doluluk metrikleri
        [HttpGet]
        public IActionResult Board()
        {
            var now = DateTime.Now;
            var res = _service.GetBoard(now); 

            if (!res.Success || res.Data == null)
            {
                TempData["Error"] = res.Message;
                return View(new List<MasaBoardItemVm>());
            }

            // View tarafında gösterilen özet metrikler (UI kırılmasın diye ViewBag)
            ViewBag.AktifMasa = res.Data.AktifMasa;
            ViewBag.DoluMasa = res.Data.DoluMasa;
            ViewBag.BlokeliBosMasa = res.Data.BlokeliBosMasa;
            ViewBag.WalkinBosMasa = res.Data.WalkinBosMasa;
            ViewBag.FizikselDoluluk = res.Data.FizikselDoluluk;
            ViewBag.EfektifDoluluk = res.Data.EfektifDoluluk;

            return View(res.Data.Items); 
        }

        // Masayı operasyonel olarak açar: açık sipariş yoksa oluşturur ve sipariş ekranına yönlendirir
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Open(int id)
        {
            var now = DateTime.Now;
            var result = _service.EnsureOpenTable(id, now); 

            if (!result.Success)
            {
                TempData["Error"] = result.Message;
                return RedirectToAction("Board");
            }

            TempData["Success"] = "Masa açıldı / açık sipariş hazır.";
            return RedirectToAction("Take", "Siparisler", new { masaId = id });
        }

        // Rezervasyon ekranı (varsayılan saat: bugün 20:00)
        [HttpGet]
        public IActionResult Reserve(int id)
        {
            var masaRes = _service.GetById(id); 
            if (!masaRes.Success || masaRes.Data == null)
            {
                TempData["Error"] = masaRes.Message;
                return RedirectToAction("Board");
            }

            var vm = new RezervasyonCreateVm
            {
                MasaId = id,
                RezervasyonTarihi = DateTime.Now.Date.AddHours(20)
            };

            ViewBag.MasaNo = masaRes.Data.MasaNo;
            return View(vm);
        }

        // Rezervasyon oluşturma (PRG)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reserve(RezervasyonCreateVm model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Lütfen form alanlarını kontrol ediniz.";

                var masaRes = _service.GetById(model.MasaId);
                ViewBag.MasaNo = masaRes.Success && masaRes.Data != null ? masaRes.Data.MasaNo : 0;

                return View(model);
            }

            var res = _rezService.Create(model); // ★
            if (!res.Success)
            {
                TempData["Error"] = res.Message;

                var masaRes = _service.GetById(model.MasaId);
                ViewBag.MasaNo = masaRes.Success && masaRes.Data != null ? masaRes.Data.MasaNo : 0;

                return View(model);
            }

            TempData["Success"] = res.Message;
            return RedirectToAction("Board");
        }

        // Rezervasyon iptal (PRG)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelReservation(int rezervasyonId) 
        {
            var res = _rezService.Cancel(rezervasyonId); 
            TempData[res.Success ? "Success" : "Error"] = res.Message;
            return RedirectToAction("Board");
        }

    }
}
