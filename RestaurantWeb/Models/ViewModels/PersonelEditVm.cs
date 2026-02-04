using System.ComponentModel.DataAnnotations;
using RestaurantWeb.Models;

namespace RestaurantWeb.Models.ViewModels
{
    public class PersonelEditVm
    {
        [Range(1, int.MaxValue, ErrorMessage = "Geçersiz personel id.")]
        public int Id { get; set; }

        [Required(ErrorMessage = "Ad Soyad boş bırakılamaz.")]
        [StringLength(150, ErrorMessage = "Ad Soyad en fazla 150 karakter olabilir.")]
        public string AdSoyad { get; set; } = "";

        [Required(ErrorMessage = "Kullanıcı adı boş bırakılamaz.")]
        [StringLength(100, ErrorMessage = "Kullanıcı adı en fazla 100 karakter olabilir.")]
        public string KullaniciAdi { get; set; } = "";

        public int RolMask { get; set; } // ★
        public bool AktifMi { get; set; }
    }
}
