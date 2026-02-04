using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantWeb.Models.ViewModels;
using RestaurantWeb.Services;

namespace RestaurantWeb.Controllers
{
    [Authorize(Roles = "Admin")] // * sadece Admin
    public class BackupController : Controller
    {
        private readonly IBackupService _service;
        private readonly ILogger<BackupController> _logger;

        public BackupController(IBackupService service, ILogger<BackupController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var res = _service.ListBackups();
            var vm = new BackupIndexVm();

            if (!res.Success || res.Data == null)
            {
                TempData["Error"] = res.Message;
                vm.Items = new();
                return View(vm);
            }

            vm.Items = res.Data;

            if (vm.Items.Count == 0)
                TempData["Info"] = "Henüz backup alınmamış.";

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create() // * PRG
        {
            try
            {
                var actor = User?.Identity?.Name;
                var res = _service.CreateBackup(actor);
                TempData[res.Success ? "Success" : "Error"] = res.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup alınırken hata.");
                TempData["Error"] = "Teknik bir hata oluştu.";
            }

            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult Download(string fileName)
        {
            var res = _service.GetBackupFile(fileName);
            if (!res.Success || res.Data.Bytes == null)
            {
                TempData["Error"] = res.Message;
                return RedirectToAction("Index");
            }

            return File(res.Data.Bytes, res.Data.ContentType, res.Data.DownloadName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(string fileName) // * PRG
        {
            var res = _service.DeleteBackup(fileName);
            TempData[res.Success ? "Success" : "Error"] = res.Message;
            return RedirectToAction("Index");
        }
    }
}
