
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantWeb.Services;

namespace RestaurantWeb.Controllers
{
    [Authorize(Roles = "Admin,Kasa")]
    public class RaporlarController : Controller
    {
        private readonly IRaporService _service; 

        public RaporlarController(IRaporService service) 
        {
            _service = service; 
        }

        // Tarih aralığı + rapor modu (kasa/close vs) parametrelerini service’e iletir, ViewModel döndürür.
        public IActionResult Dashboard(DateTime? baslangic, DateTime? bitis, string? mode)
        {
            var res = _service.GetDashboard(baslangic, bitis, mode); 
            if (!res.Success || res.Data == null) 
            {
                TempData["Error"] = res.Message; 
                return View(new RestaurantWeb.Models.ViewModels.DashboardVm()); 
            }

            return View(res.Data); 
        }

        // Aynı filtrelerle raporu Excel’e export eder; dosya üretimi tamamen service katmanındadır.
        [HttpGet]
        public IActionResult ExportExcel(DateTime? baslangic, DateTime? bitis, string? mode)
        {
            var res = _service.ExportDashboardExcel(baslangic, bitis, mode); 
            if (!res.Success || res.Data == null) 
            {
                TempData["Error"] = res.Message; 
                return RedirectToAction("Dashboard", new { baslangic, bitis, mode }); 
            }

            return File(res.Data.Bytes, res.Data.ContentType, res.Data.FileName); 
        }
    }
}
