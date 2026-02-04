using System.ComponentModel.DataAnnotations;

namespace RestaurantWeb.Models.ViewModels
{
    public class MasaCreateVm
    {
        [Required(ErrorMessage = "Masa numarası zorunludur.")]
        [Range(1, int.MaxValue, ErrorMessage = "Masa numarası 1'den büyük olmalıdır.")]
        public int MasaNo { get; set; }

        [Required(ErrorMessage = "Kapasite zorunludur.")]
        [Range(1, int.MaxValue, ErrorMessage = "Kapasite 1'den büyük olmalıdır.")]
        public int Kapasite { get; set; } = 4;
    }
}
