using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using RestaurantWeb.Models;
using RestaurantWeb.Models.ViewModels;
using RestaurantWeb.Services; 

namespace RestaurantWeb.Controllers
{
    [Authorize(Roles = "Admin,Kasa,Garson")]
    public class SiparislerController : Controller
    {
        private readonly ISiparisService _service;
        private readonly ILogger<SiparislerController> _logger;
        private readonly ICatalogService _catalog;

        public class SubmitOrderRequest
        {
            public int SiparisId { get; set; }
            public List<SubmitOrderItem> Items { get; set; } = new();
        }

        public class SubmitOrderItem
        {
            public int UrunId { get; set; }
            public int Adet { get; set; }
        }

        public class UpdateDiscountRequest
        {
            public int SiparisId { get; set; }
            public decimal IskontoOran { get; set; }
        }

        public class CloseOrderRequest
        {
            public int SiparisId { get; set; }
            public short Yontem { get; set; }
        }

        public SiparislerController(
            ISiparisService service,
            ICatalogService catalog, 
            ILogger<SiparislerController> logger)
        {
            _service = service; 
            _catalog = catalog; 
            _logger = logger;
        }


        [HttpGet]
        public IActionResult Take(int masaId)
        {
            if (masaId <= 0)
            {
                TempData["Error"] = "Geçersiz masa.";
                return RedirectToAction("Board", "Masalar");
            }

            var masaNoRes = _service.GetMasaNoById(masaId); 
            if (!masaNoRes.Success)
            {
                TempData["Error"] = masaNoRes.Message;
                return RedirectToAction("Board", "Masalar");
            }

            var open = _service.GetOpenOrderId(masaId); 
            if (!open.Success)
            {
                TempData["Error"] = open.Message;
                return RedirectToAction("Board", "Masalar");
            }

            if (open.Data == null)
            {
                TempData["Error"] = "Bu masada açık sipariş yok. Önce masayı açın.";
                return RedirectToAction("Board", "Masalar");
            }

            ViewBag.MasaId = masaId;
            ViewBag.MasaNo = masaNoRes.Data;
            ViewBag.SiparisId = open.Data.Value;

            var adisyonRes = _service.GetSiparisAdisyon(open.Data.Value); 
            if (!adisyonRes.Success || adisyonRes.Data == null)
            {
                TempData["Error"] = adisyonRes.Message;
                return RedirectToAction("Board", "Masalar");
            }

            ViewBag.Adisyon = adisyonRes.Data;
            return View();
        }


        [HttpGet]
        public IActionResult Products(int? kategoriId = null)
        {
            var res = _catalog.GetActiveProducts(kategoriId); 
            if (!res.Success)
                return StatusCode(500, new { message = res.Message });

            // JS contract korunuyor: { id, ad, fiyat, stok, kategori } 
            var list = (res.Data ?? new()).Select(p => new
            {
                id = p.Id,
                ad = p.Ad,
                fiyat = p.Fiyat,
                stok = p.Stok,
                kategori = p.Kategori
            });

            return Json(list);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Submit([FromBody] SubmitOrderRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "İstek boş." });

            if (req.SiparisId <= 0)
                return BadRequest(new { message = "Geçersiz sipariş id." });

            if (req.Items == null || req.Items.Count == 0)
                return BadRequest(new { message = "Sepet boş." });

            var items = req.Items.Select(x => (x.UrunId, x.Adet)).ToList();
            var result = _service.SubmitOrder(req.SiparisId, items); 

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message, siparisId = req.SiparisId });
        }

        [HttpGet]
        public IActionResult GetOrderTable(int siparisId)
        {
            var res = _service.GetSiparisAdisyon(siparisId); 
            if (!res.Success || res.Data == null)
                return StatusCode(500, res.Message);

            return PartialView("_OrderTablePartial", res.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateDiscount([FromBody] UpdateDiscountRequest req)
        {
            var actor = User?.Identity?.Name; // aynı
            var res = _service.UpdateDiscountRate(req.SiparisId, req.IskontoOran, actor); 
            if (!res.Success)
                return BadRequest(new { message = res.Message });

            return Ok(new { message = res.Message });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Close([FromBody] CloseOrderRequest req)
        {
            var actor = User?.Identity?.Name;

            if (!Enum.IsDefined(typeof(OdemeYontemi), req.Yontem)) 
                return BadRequest(new { message = "Geçersiz ödeme yöntemi." }); 


            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "Kullanıcı bulunamadı." });

            var res = _service.CloseOrderWithPayment(req.SiparisId, (OdemeYontemi)req.Yontem, actor, userId);

            if (!res.Success)
                return BadRequest(new { message = res.Message });

            return Ok(new { message = res.Message });
        }


        public IActionResult Index(DateTime? baslangic, DateTime? bitis, int? masaNo)
        {
            var start = (baslangic ?? DateTime.Today.AddDays(-6)).Date;
            var end = (bitis ?? DateTime.Today).Date;

            if (end < start)
            {
                TempData["Error"] = "Bitiş tarihi başlangıçtan küçük olamaz.";
                end = start;
            }

            var res = _service.GetOrders(start, end, masaNo); 
            if (!res.Success)
            {
                TempData["Error"] = res.Message;
                return View(new SiparisListVm { Baslangic = start, Bitis = end, MasaNo = masaNo });
            }

            return View(new SiparisListVm
            {
                Baslangic = start,
                Bitis = end,
                MasaNo = masaNo,
                Items = res.Data ?? new()
            });
        }

        [HttpGet]
        public IActionResult Detay(int id)
        {
            var res = _service.GetOrderDetail(id); 
            if (!res.Success || res.Data == null)
            {
                TempData["Error"] = res.Message;
                return RedirectToAction("Index");
            }

            return View(res.Data);
        }

        [HttpGet]
        public IActionResult Categories()
        {
            var res = _catalog.GetActiveCategories(); 
            if (!res.Success)
                return StatusCode(500, new { message = res.Message });

            var list = (res.Data ?? new()).Select(c => new
            {
                id = c.Id,
                ad = c.Ad
            });

            return Json(list);
        }

        [HttpGet]
        public IActionResult LogPartial(int siparisId)
        {
            var res = _service.GetLogs(siparisId); 
            if (!res.Success)
                return StatusCode(500, res.Message);

            return PartialView("_SiparisLogPartial", res.Data ?? new());
        }
    }
}
