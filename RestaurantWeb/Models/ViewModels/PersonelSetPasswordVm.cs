using System.ComponentModel.DataAnnotations; // ★

namespace RestaurantWeb.Models.ViewModels
{
    public class PersonelSetPasswordVm
    {
        [Range(1, int.MaxValue)]
        public int Id { get; set; } // ★

        public string KullaniciAdi { get; set; } = ""; // ★

        [Required]
        [StringLength(100, MinimumLength = 4)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = ""; // ★

        [Required]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Şifreler uyuşmuyor.")]
        public string NewPasswordConfirm { get; set; } = ""; // ★
    }
}
