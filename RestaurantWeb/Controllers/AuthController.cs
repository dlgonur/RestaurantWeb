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
        private readonly IAuthService _auth; 

        public AuthController(IAuthService auth) 
        {
            _auth = auth; 
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
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

            var res = _auth.ValidateCredentials(vm.KullaniciAdi, vm.Sifre); 
            if (!res.Success || res.Data == null) 
            {
                ModelState.AddModelError("", res.Message); 
                return View(vm);
            }

            var user = res.Data;
            var roles = user.Rol;

            // Claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.KullaniciAdi),
            };

            foreach (PersonelRol r in Enum.GetValues(typeof(PersonelRol)))
            {
                if (r == PersonelRol.None) continue;
                if (roles.HasFlag(r))
                    claims.Add(new Claim(ClaimTypes.Role, r.ToString()));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            // 1) returnUrl varsa (ve local ise) her zaman ona dön 
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) 
                return Redirect(returnUrl); 

            // 2) returnUrl yoksa service-based landing 
            var controller = _auth.GetLandingController(roles); 
            var action = _auth.GetLandingAction(roles); 
            return RedirectToAction(action, controller); 
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult Denied() => View();
    }
}
