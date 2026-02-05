// Veritabanı yedekleme (backup) işlemlerini yönetir.
// Sadece Admin rolüne açıktır; listeleme, oluşturma, indirme ve silme akışlarını içerir.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantWeb.Models.ViewModels;
using RestaurantWeb.Services;

namespace RestaurantWeb.Controllers
{
    [Authorize(Roles = "Admin")] 
    public class BackupController : Controller
    {
        private readonly IBackupService _service;
        private readonly ILogger<BackupController> _logger;

        public BackupController(IBackupService service, ILogger<BackupController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // Mevcut backup listesini gösterir
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

        // Yeni backup oluşturur (PRG pattern)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create() // * PRG
        {
            try
            {
                // İşlemi yapan kullanıcı adı (audit amaçlı)
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

        // Backup dosyasını indirir
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

        // Backup silme işlemi (PRG pattern)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(string fileName) 
        {
            var res = _service.DeleteBackup(fileName);
            TempData[res.Success ? "Success" : "Error"] = res.Message;
            return RedirectToAction("Index");
        }
    }
}
