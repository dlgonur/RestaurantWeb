// Ürün yönetimi (CRUD + aktif/pasif) ve ürün görseli (upload/serve) akışlarını yönetir.
// İş kuralları servis katmanında; controller form doğrulama, PRG ve UI mesajlarını yönetir.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using RestaurantWeb.Models;
using RestaurantWeb.Models.ViewModels;
using RestaurantWeb.Services;

namespace RestaurantWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UrunlerController : Controller
    {
        private readonly ILogger<UrunlerController> _logger;
        private readonly IUrunService _service; 
        private readonly IKategoriService _kategoriService;
        public UrunlerController(IUrunService service, IKategoriService kategoriService, ILogger<UrunlerController> logger)
        {
            _service = service;
            _kategoriService = kategoriService;
            _logger = logger;
        }


        // Ürünleri kategori bilgisiyle listeler
        public IActionResult Index()
        {
            var result = _service.GetAllWithKategori();

            if (!result.Success)
            {
                TempData["Error"] = result.Message;
                return View(new List<Urun>());
            }

            if ((result.Data == null || result.Data.Count == 0) && !string.IsNullOrWhiteSpace(result.Message))
            {
                TempData["Info"] = result.Message;
            }
            return View(result.Data ?? new List<Urun>());

        }

        [HttpGet]
        public IActionResult Create() 
        {
            FillKategoriDropdown();
            return View(new UrunCreateVm());
        }

        // Ürün ekleme (PRG)
        [HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult Create(UrunCreateVm model)
		{
			if (!ModelState.IsValid)
			{
				TempData["Error"] = "Lütfen form alanlarını kontrol ediniz.";
				FillKategoriDropdown(model.KategoriId);
				return View(model);
			}

            // --- Resim okuma + temel validasyon ---
            byte[]? resimBytes = null;
			string? resimMime = null;
			string? resimAdi = null;

			if (model.Resim != null && model.Resim.Length > 0) 
			{
				const long MaxImageBytes = 5 * 1024 * 1024; // 5 MB limit

                if (model.Resim.Length > MaxImageBytes) 
				{
					TempData["Error"] = "Resim boyutu en fazla 5 MB olabilir.";
					FillKategoriDropdown(model.KategoriId);
					return View(model);
				}

				if (!model.Resim.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) 
				{
					TempData["Error"] = "Sadece resim dosyaları yüklenebilir.";
					FillKategoriDropdown(model.KategoriId);
					return View(model);
				}

				using (var ms = new MemoryStream()) 
				{
					model.Resim.CopyTo(ms); 
					resimBytes = ms.ToArray(); 
				}


				resimMime = model.Resim.ContentType; 
				resimAdi = Path.GetFileName(model.Resim.FileName); 
			}

            var ad = model.Ad;
			var addResult = _service.Add(model.KategoriId, ad, model.Fiyat, model.Stok, resimBytes, resimMime, resimAdi); 

			if (!addResult.Success)
			{
				_logger.LogWarning(
					"Ürün ekleme başarısız. KategoriId: {KategoriId}, Ad: {Ad}, Fiyat: {Fiyat}, Stok: {Stok}. Mesaj: {Message}",
					model.KategoriId, ad, model.Fiyat, model.Stok, addResult.Message
				);

				TempData["Error"] = addResult.Message;
				FillKategoriDropdown(model.KategoriId);
				return View(model);
			}

			TempData["Success"] = addResult.Message;
			return RedirectToAction("Index");
		}

        [HttpGet]
        public IActionResult EditModal(int id)
        {
            var result = _service.GetByIdWithKategori(id);
            if (!result.Success || result.Data == null)
                return BadRequest(result.Message);

            FillKategoriDropdown(result.Data.KategoriId);

            var vm = new UrunEditVm
            {
                Id = result.Data.Id,
                KategoriId = result.Data.KategoriId,
                Ad = result.Data.Ad,
                Fiyat = result.Data.Fiyat,
                Stok = result.Data.Stok,
                ResimVar = result.Data.Resim != null && result.Data.Resim.Length > 0
            };

            return PartialView("_EditModalPartial", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditModal(UrunEditVm model)
        {
            if (!ModelState.IsValid)
                return BadRequest("Lütfen form alanlarını kontrol ediniz.");

            if (model.ResmiKaldir && model.Resim != null && model.Resim.Length > 0)
                return BadRequest("Lütfen ya yeni resim seçin ya da mevcut resmi kaldırın (ikisi aynı anda olamaz).");

            byte[]? resimBytes = null;
            string? resimMime = null;
            string? resimAdi = null;
            bool resimGuncellensin = false;

            if (model.ResmiKaldir)
            {
                resimGuncellensin = true;
            }
            else if (model.Resim != null && model.Resim.Length > 0)
            {
                const long MaxImageBytes = 5 * 1024 * 1024;

                if (model.Resim.Length > MaxImageBytes)
                    return BadRequest("Resim boyutu en fazla 5 MB olabilir.");

                if (!model.Resim.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    return BadRequest("Sadece resim dosyaları yüklenebilir.");

                using (var ms = new MemoryStream())
                {
                    model.Resim.CopyTo(ms);
                    resimBytes = ms.ToArray();
                }

                resimMime = model.Resim.ContentType;
                resimAdi = Path.GetFileName(model.Resim.FileName);
                resimGuncellensin = true;
            }

            var ad = model.Ad;
            var updateResult = _service.Update(
                model.Id,
                model.KategoriId,
                ad,
                model.Fiyat,
                model.Stok,
                resimBytes,
                resimMime,
                resimAdi,
                resimGuncellensin
            );

            if (!updateResult.Success)
                return BadRequest(updateResult.Message);

            TempData["Success"] = updateResult.Message;
            return Json(new { success = true });
        }

        [HttpGet]
        public IActionResult DeleteModal(int id)
        {
            var result = _service.GetByIdWithKategori(id);
            if (!result.Success || result.Data == null)
                return BadRequest(result.Message);

            return PartialView("_DeleteModalPartial", result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteModalConfirmed(int id)
        {
            var result = _service.Delete(id);
            if (!result.Success)
                return BadRequest(result.Message);

            TempData["Success"] = result.Message;
            return Json(new { success = true });
        }

        [HttpGet]
        public IActionResult Resim(int id)
        {
            var result = _service.GetResim(id);
            if (!result.Success || result.Data.Bytes == null || result.Data.Bytes.Length == 0)
                return NotFound();

            Response.Headers["Cache-Control"] = "public,max-age=600";
            return File(result.Data.Bytes, result.Data.Mime);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleAktif(int id)
        {
            var result = _service.ToggleAktif(id);

            // AJAX isteği mi kontrolü
            var isAjax = string.Equals(
                Request.Headers["X-Requested-With"],
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase); 

            if (isAjax) 
            {
                return Json(new
                {
                    success = result.Success,
                    message = result.Message
                }); 
            }

            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction("Index");
        }

        // Kategori dropdown’ını ViewBag üzerinden view’a hazırlar
        private void FillKategoriDropdown(int? selectedKategoriId = null) 
        {
            var catResult = _kategoriService.GetAll(); 
            if (!catResult.Success) 
            {
                TempData["Error"] = catResult.Message; 
                ViewBag.Kategoriler = new List<SelectListItem>(); 
                return; 
            } 

            ViewBag.Kategoriler = (catResult.Data ?? new List<Kategori>()) 
                .Select(k => new SelectListItem 
                {
                    Value = k.Id.ToString(), 
                    Text = k.Ad, 
                    Selected = selectedKategoriId.HasValue && k.Id == selectedKategoriId.Value 
                }) 
                .ToList(); 
        } 

    }
}
