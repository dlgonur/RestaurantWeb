using System.ComponentModel.DataAnnotations;
using RestaurantWeb.Models;

namespace RestaurantWeb.Models.ViewModels
{
    public class PersonelCreateVm
    {
        [Required(ErrorMessage = "Ad Soyad boş bırakılamaz.")]
        [StringLength(150, ErrorMessage = "Ad Soyad en fazla 150 karakter olabilir.")]
        public string AdSoyad { get; set; } = "";

        [Required(ErrorMessage = "Kullanıcı adı boş bırakılamaz.")]
        [StringLength(100, ErrorMessage = "Kullanıcı adı en fazla 100 karakter olabilir.")]
        public string KullaniciAdi { get; set; } = "";

        [Required(ErrorMessage = "Şifre boş bırakılamaz.")]
        [StringLength(100, MinimumLength = 4, ErrorMessage = "Şifre en az 4 karakter olmalıdır.")]
        public string Sifre { get; set; } = "";
        public int RolMask { get; set; } 

    }
}
