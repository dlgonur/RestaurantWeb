
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

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var result = _service.GetById(id);
            if (!result.Success || result.Data == null)
            {
                TempData["Error"] = result.Message;
                return RedirectToAction(nameof(Index));
            }

            var vm = new PersonelEditVm
            {
                Id = result.Data.Id,
                AdSoyad = result.Data.AdSoyad,
                KullaniciAdi = result.Data.KullaniciAdi,
                RolMask = (int)result.Data.Rol, 
                AktifMi = result.Data.AktifMi
            };

            FillRolCheckboxes(vm.RolMask); 
            return View(vm);

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, PersonelEditVm model)
        {
            if (id != model.Id)
            {
                TempData["Error"] = "İstek geçersiz (Id uyuşmazlığı).";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Lütfen form alanlarını kontrol ediniz.";
                FillRolCheckboxes(model.RolMask);
                return View(model);
            }

            var updateResult = _service.Update(
    id: model.Id,
    adSoyad: model.AdSoyad,
    kullaniciAdi: model.KullaniciAdi,
    rolMask: model.RolMask,
    actorPersonelId: CurrentPersonelId(),
    actorUsername: CurrentUsername(),
    ip: ClientIp()
); // ★


            if (!updateResult.Success)
            {
                _logger.LogWarning("Personel güncelleme başarısız. Id: {Id}. Mesaj: {Message}",
                    model.Id, updateResult.Message);

                TempData["Error"] = updateResult.Message;
                FillRolCheckboxes(model.RolMask);
                return View(model);
            }

            TempData["Success"] = updateResult.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleAktif(int id)
        {
            var actorId = CurrentPersonelId(); // ★
            if (actorId.HasValue && actorId.Value == id) // ★
            {
                TempData["Error"] = "Kendi hesabınızı pasif edemezsiniz."; // ★
                return RedirectToAction(nameof(Index)); // ★
            }

                var result = _service.ToggleAktif(id, CurrentPersonelId(), CurrentUsername(), ClientIp()); 

            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpGet] 
        public IActionResult SetPassword(int id) 
        {
            var res = _service.GetById(id);
            if (!res.Success || res.Data == null)
            {
                TempData["Error"] = res.Message;
                return RedirectToAction(nameof(Index));
            }

            var vm = new PersonelSetPasswordVm
            {
                Id = res.Data.Id,
                KullaniciAdi = res.Data.KullaniciAdi
            };

            return View(vm);
        }

        [HttpPost] 
        [ValidateAntiForgeryToken] 
        public IActionResult SetPassword(PersonelSetPasswordVm vm) 
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Lütfen form alanlarını kontrol ediniz.";
                return View(vm);
            }

            var result = _service.SetPassword(
    id: vm.Id,
    kullaniciAdi: vm.KullaniciAdi,
    newPassword: vm.NewPassword,
    actorPersonelId: CurrentPersonelId(),
    actorUsername: CurrentUsername(),
    ip: ClientIp()
); // ★


            if (!result.Success)
            {
                TempData["Error"] = result.Message;
                return View(vm);
            }

            TempData["Success"] = $"'{vm.KullaniciAdi}' kullanıcısının şifresi güncellendi."; 
            return RedirectToAction(nameof(Index));
        }

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
