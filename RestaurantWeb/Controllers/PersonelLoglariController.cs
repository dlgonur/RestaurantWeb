using Microsoft.AspNetCore.Authorization; 
using Microsoft.AspNetCore.Mvc; 
using RestaurantWeb.Models.ViewModels; 
using RestaurantWeb.Services; 

namespace RestaurantWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class PersonelLoglariController : Controller
    {
        private readonly IPersonelLogService _service; 

        public PersonelLoglariController(IPersonelLogService service) 
        {
            _service = service; 
        }

        [HttpGet]
        public IActionResult Index(DateTime? baslangic, DateTime? bitis, string? aksiyon, string? target)
        {

            if (baslangic.HasValue && bitis.HasValue && bitis.Value.Date < baslangic.Value.Date)
                (baslangic, bitis) = (bitis, baslangic);

            var vm = new PersonelLogListVm
            {
                Baslangic = baslangic, 
                Bitis = bitis,         
                Aksiyon = aksiyon,
                TargetKullaniciAdi = target
            };


            var res = _service.GetList(baslangic, bitis, aksiyon, target); 
            if (!res.Success || res.Data == null)
            {
                TempData["Error"] = res.Message;
                vm.Logs = new();
                return View(vm);
            }

            vm.Logs = res.Data;

            if (vm.Logs.Count == 0)
                TempData["Info"] = "Filtreye uygun log bulunamadı.";

            return View(vm);
        }
    }
}
