using RestaurantWeb.Models;
using RestaurantWeb.Models.Dtos;

namespace RestaurantWeb.Services
{
    public interface IAuthService
    {
        OperationResult<AuthUserDto> ValidateCredentials(string kullaniciAdi, string sifrePlain); // ★
        string GetLandingController(PersonelRol roles); // ★
        string GetLandingAction(PersonelRol roles); // ★
    }
}
