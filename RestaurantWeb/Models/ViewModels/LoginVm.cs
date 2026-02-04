using System.ComponentModel.DataAnnotations;

namespace RestaurantWeb.Models.ViewModels
{
    public class LoginVm
    {
        [Required]
        [Display(Name = "Kullanıcı Adı")]
        public string KullaniciAdi { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Şifre")]
        public string Sifre { get; set; } = "";
    }
}
