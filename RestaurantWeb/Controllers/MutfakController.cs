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

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult PendingPartial()
        {
            var res = _service.GetPendingOrders(); // *
            if (!res.Success)
                return StatusCode(500, res.Message);

            return PartialView("_PendingOrdersPartial", res.Data ?? new());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetItemStatus([FromBody] SetItemStatusRequest req)
        {
            var actor = User?.Identity?.Name;
            var res = _service.SetItemStatus(req.KalemId, req.Durum, actor); // *
            if (!res.Success) return BadRequest(new { message = res.Message });
            return Ok(new { message = res.Message });
        }


        public class SetItemStatusRequest
        {
            public int KalemId { get; set; }
            public short Durum { get; set; }
        }
    }
}
