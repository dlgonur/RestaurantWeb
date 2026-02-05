// Mutfak ekranı: mutfakta “bekleyen/hazırlanan/hazır” kalemleri listeleyen ve kalem durumunu güncelleyen controller.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantWeb.Services;

namespace RestaurantWeb.Controllers
{
    [Authorize(Roles = "Admin,Mutfak")]

    public class MutfakController : Controller
    {
        private readonly IMutfakService _service;

        public MutfakController(IMutfakService service)
        {
            _service = service;
        }

        // Sayfayı render eder(liste AJAX ile Partial’dan gelir)
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // Mutfak kuyruğunu partial olarak döner
        [HttpGet]
        public IActionResult PendingPartial()
        {
            var res = _service.GetPendingOrders(); 
            if (!res.Success)
                return StatusCode(500, res.Message);

            return PartialView("_PendingOrdersPartial", res.Data ?? new());
        }

        // Kalem durumunu (POST + CSRF) günceller, JSON mesaj döner
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetItemStatus([FromBody] SetItemStatusRequest req)
        {
            // Mutfağın “kim yaptı” bilgisi için actor username log’a yazılır.
            var actor = User?.Identity?.Name;
            var res = _service.SetItemStatus(req.KalemId, req.Durum, actor); 
            if (!res.Success) return BadRequest(new { message = res.Message });
            return Ok(new { message = res.Message });
        }

        // JSON request contract (fetch/AJAX)
        public class SetItemStatusRequest
        {
            public int KalemId { get; set; }
            public short Durum { get; set; }
        }
    }
}
