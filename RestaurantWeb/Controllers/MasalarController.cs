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

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var result = _service.GetById(id); 
            if (!result.Success || result.Data == null)
            {
                TempData["Error"] = result.Message;
                return RedirectToAction("Index");
            }

            return View(new MasaEditVm
            {
                Id = result.Data.Id,
                MasaNo = result.Data.MasaNo,
                Kapasite = result.Data.Kapasite,
                AktifMi = result.Data.AktifMi
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, MasaEditVm model)
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

            var result = _service.Update(model.Id, model.MasaNo, model.Kapasite); 

            if (!result.Success)
            {
                _logger.LogWarning("Masa güncelleme başarısız. Id: {Id}, MasaNo: {MasaNo}, Kapasite: {Kapasite}. Mesaj: {Message}",
                    model.Id, model.MasaNo, model.Kapasite, result.Message);

                TempData["Error"] = result.Message;
                return View(model);
            }

            TempData["Success"] = result.Message;
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Toggle(int id)
        {
            var result = _service.ToggleAktif(id); 
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult Delete(int id)
        {
            var result = _service.GetById(id); 
            if (!result.Success || result.Data == null)
            {
                TempData["Error"] = result.Message;
                return RedirectToAction("Index");
            }
            return View(result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Delete")]
        public IActionResult DeleteConfirmed(int id)
        {
            var result = _service.Delete(id); 
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction("Index");
        }

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

            // ViewBag set etmeye devam: View’i kırmıyoruz 
            ViewBag.AktifMasa = res.Data.AktifMasa;
            ViewBag.DoluMasa = res.Data.DoluMasa;
            ViewBag.BlokeliBosMasa = res.Data.BlokeliBosMasa;
            ViewBag.WalkinBosMasa = res.Data.WalkinBosMasa;
            ViewBag.FizikselDoluluk = res.Data.FizikselDoluluk;
            ViewBag.EfektifDoluluk = res.Data.EfektifDoluluk;

            return View(res.Data.Items); 
        }


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
