// Personel yönetimi (Admin):
// - Listeleme + filtreleme (aktiflik/ad/username)
// - Personel oluşturma / güncelleme
// - Aktif/pasif toggle (kendi hesabını pasif etmeyi engeller)
// - Şifre sıfırlama
// Not: Audit amaçlı actor bilgileri (personelId/username/ip) service’e iletilir.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantWeb.Models;
using RestaurantWeb.Models.ViewModels;
using System.Security.Claims;
using RestaurantWeb.Services;



namespace RestaurantWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class PersonellerController : Controller
    {
        private readonly ILogger<PersonellerController> _logger;
        private readonly IPersonelService _service;

        public PersonellerController(IPersonelService service, ILogger<PersonellerController> logger)
        {
            _service = service;
            _logger = logger;
        }

        private int? CurrentPersonelId() 
        {
            var s = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(s, out var id) ? id : null;
        }

        private string? CurrentUsername() 
        {
            return User.Identity?.Name;
        }

        private string? ClientIp() 
        {
            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }

        // Listeleme: aktiflik ve metin filtreleri ile personelleri getirir
        public IActionResult Index(string? aktif, string? qAd, string? qUser)
        {
            // aktif: "all" | "1" | "0"
            bool? aktifMi = null;
            if (!string.IsNullOrWhiteSpace(aktif) && !aktif.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                if (aktif == "1") aktifMi = true;
                else if (aktif == "0") aktifMi = false;
            }

            // View'e mevcut filtreleri geri basmak için
            ViewBag.Aktif = string.IsNullOrWhiteSpace(aktif) ? "all" : aktif;
            ViewBag.QAd = qAd ?? "";
            ViewBag.QUser = qUser ?? "";

            var result = _service.GetAllFiltered(aktifMi, qAd, qUser);

            if (!result.Success)
            {
                TempData["Error"] = result.Message;
                return View(new List<Personel>());
            }

            if ((result.Data == null || result.Data.Count == 0) && !string.IsNullOrWhiteSpace(result.Message))
                TempData["Info"] = result.Message;

            return View(result.Data ?? new List<Personel>());
        }

        [HttpGet]
        public IActionResult Create()
        {
            FillRolCheckboxes(0); 
            return View(new PersonelCreateVm { RolMask = 0 }); 
        }

        // Personel oluşturma: rol mask + şifre set + audit (actor/ip)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(PersonelCreateVm model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Lütfen form alanlarını kontrol ediniz.";
                FillRolCheckboxes(model.RolMask);
                return View(model);
            }

            var addResult = _service.Create(
    adSoyad: model.AdSoyad,
    kullaniciAdi: model.KullaniciAdi,
    sifre: model.Sifre,
    rolMask: model.RolMask,
    actorPersonelId: CurrentPersonelId(),
    actorUsername: CurrentUsername(),
    ip: ClientIp()
);

            if (!addResult.Success)
            {
                _logger.LogWarning("Personel ekleme başarısız. KullaniciAdi: {KullaniciAdi}. Mesaj: {Message}",
                    model.KullaniciAdi, addResult.Message);

                TempData["Error"] = addResult.Message;
                FillRolCheckboxes(model.RolMask);
                return View(model);
            }

            TempData["Success"] = addResult.Message;
            return RedirectToAction(nameof(Index));
        }

        // Aktif/pasif: admin kendi hesabını pasif edemez
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleAktif(int id)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            var actorId = CurrentPersonelId();
            if (actorId.HasValue && actorId.Value == id)
            {
                var msg = "Kendi hesabınızı pasif edemezsiniz.";

                if (isAjax)
                    return BadRequest(msg); // JS tarafında alert(text) ile göster

                TempData["Error"] = msg;
                return RedirectToAction(nameof(Index));
            }

            var result = _service.ToggleAktif(id, actorId, CurrentUsername(), ClientIp());

            if (isAjax)
            {
                if (!result.Success)
                    return BadRequest(result.Message);

                // Artık yeni aktif durumu client'a dönüyoruz
                return Json(new { success = true, message = result.Message, aktifMi = result.Data });
            }

            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // Modal için Edit içeriği (Partial)
        [HttpGet]
        public IActionResult EditModal(int id)
        {
            var result = _service.GetById(id);
            if (!result.Success || result.Data == null)
                return BadRequest(result.Message);

            var vm = new PersonelEditVm
            {
                Id = result.Data.Id,
                AdSoyad = result.Data.AdSoyad,
                KullaniciAdi = result.Data.KullaniciAdi,
                RolMask = (int)result.Data.Rol,
                AktifMi = result.Data.AktifMi
            };

            FillRolCheckboxes(vm.RolMask);
            ViewBag.CurrentPersonelId = CurrentPersonelId();
            return PartialView("_EditModalPartial", vm);
        }

        // Modal Edit submit (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditModal(PersonelEditVm model)
        {
            if (!ModelState.IsValid)
                return BadRequest("Lütfen form alanlarını kontrol ediniz.");

            var updateResult = _service.Update(
                id: model.Id,
                adSoyad: model.AdSoyad,
                kullaniciAdi: model.KullaniciAdi,
                rolMask: model.RolMask,
                actorPersonelId: CurrentPersonelId(),
                actorUsername: CurrentUsername(),
                ip: ClientIp()
            );

            if (!updateResult.Success)
                return BadRequest(updateResult.Message);

            TempData["Success"] = updateResult.Message;
            return Json(new { success = true });
        }

        // Modal için Şifre içeriği (Partial)
        [HttpGet]
        public IActionResult SetPasswordModal(int id)
        {
            var res = _service.GetById(id);
            if (!res.Success || res.Data == null)
                return BadRequest(res.Message);

            var vm = new PersonelSetPasswordVm
            {
                Id = res.Data.Id,
                KullaniciAdi = res.Data.KullaniciAdi
            };

            return PartialView("_SetPasswordModalPartial", vm);
        }

        // Modal Şifre submit (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetPasswordModal(PersonelSetPasswordVm vm)
        {
            if (!ModelState.IsValid)
                return BadRequest("Lütfen form alanlarını kontrol ediniz.");

            var result = _service.SetPassword(
                id: vm.Id,
                kullaniciAdi: vm.KullaniciAdi,
                newPassword: vm.NewPassword,
                actorPersonelId: CurrentPersonelId(),
                actorUsername: CurrentUsername(),
                ip: ClientIp()
            );

            if (!result.Success)
                return BadRequest(result.Message);

            TempData["Success"] = $"'{vm.KullaniciAdi}' kullanıcısının şifresi güncellendi.";
            return Json(new { success = true });
        }

        // UI: PersonelRol enum’undan checkbox listesi üretir (bitmask)
        private void FillRolCheckboxes(int selectedMask) 
        {
            // None’ı listelemiyoruz
            var roles = Enum.GetValues(typeof(PersonelRol))
                .Cast<PersonelRol>()
                .Where(r => r != PersonelRol.None)
                .Select(r => new
                {
                    Value = (int)r,
                    Text = r.ToString(),
                    Checked = (selectedMask & (int)r) == (int)r
                })
                .ToList();

            ViewBag.RoleOptions = roles; 
        }

    }
}
