using System.ComponentModel.DataAnnotations;

namespace RestaurantWeb.Models.ViewModels
{
    public class RezervasyonCreateVm
    {
        public int MasaId { get; set; }

        [Required(ErrorMessage = "Müşteri adı zorunludur.")]
        [StringLength(150)]
        public string MusteriAd { get; set; } = "";

        [Required(ErrorMessage = "Telefon zorunlu.")]
        [RegularExpression(@"^5\d{2}\s\d{3}\s\d{2}\s\d{2}$", ErrorMessage = "Telefon formatı: 5XX XXX XX XX")]
        public string Telefon { get; set; } = ""; // ★ zorunlu yaptık


        [Required(ErrorMessage = "Rezervasyon tarihi zorunludur.")]
        public DateTime RezervasyonTarihi { get; set; }

        [Range(1, 50, ErrorMessage = "Kişi sayısı 1-50 aralığında olmalıdır.")]
        public int? KisiSayisi { get; set; }

        [StringLength(500)]
        public string? Notlar { get; set; }
    }
}
