// Kimlik doğrulama (login/logout) ve yetkisiz erişim akışlarını yönetir.
// Cookie tabanlı auth, rol bazlı yönlendirme ve returnUrl kontrolü burada yapılır.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantWeb.Models;
using RestaurantWeb.Models.ViewModels;
using RestaurantWeb.Services;
using System.Security.Claims;

namespace RestaurantWeb.Controllers
{
    public class AuthController : Controller
    {
        // Kimlik doğrulama ve rol bazlı yönlendirme servisi
        private readonly IAuthService _auth; 

        public AuthController(IAuthService auth) 
        {
            _auth = auth; 
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            // Yetkisiz yönlendirmeler için dönüş adresi saklanır
            ViewBag.ReturnUrl = returnUrl;
            return View(new LoginVm());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginVm vm, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
                return View(vm);

            // Kullanıcı adı / şifre kontrolü
            var res = _auth.ValidateCredentials(vm.KullaniciAdi, vm.Sifre); 
            if (!res.Success || res.Data == null) 
            {
                ModelState.AddModelError("", res.Message); 
                return View(vm);
            }

            var user = res.Data;
            var roles = user.Rol;

            // --- Claims oluşturma ---
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.KullaniciAdi),
            };

            // Kullanıcının rol flag’lerinden Claim üretimi
            foreach (PersonelRol r in Enum.GetValues(typeof(PersonelRol)))
            {
                if (r == PersonelRol.None) continue;
                if (roles.HasFlag(r))
                    claims.Add(new Claim(ClaimTypes.Role, r.ToString()));
            }

            // Cookie identity oluşturulur
            var identity = new ClaimsIdentity(
                claims, 
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            // Kullanıcı sisteme giriş yapar
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme, 
                principal);

            // 1) returnUrl varsa ve local ise önceliklidir
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) 
                return Redirect(returnUrl);

            // 2) Aksi halde role göre varsayılan landing sayfası
            var controller = _auth.GetLandingController(roles); 
            var action = _auth.GetLandingAction(roles); 
            return RedirectToAction(action, controller); 
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Auth cookie temizlenir
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // Yetkisiz erişim ekranı
        [HttpGet]
        public IActionResult Denied() => View();
    }
}
