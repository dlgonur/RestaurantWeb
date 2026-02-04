using System.ComponentModel.DataAnnotations;

namespace RestaurantWeb.Models.ViewModels
{
    public class KategoriEditVm
    {
        [Range(1, int.MaxValue, ErrorMessage = "Geçersiz kategori id.")]
        public int Id { get; set; }

        [Required(ErrorMessage = "Kategori adı boş bırakılamaz.")]
        [StringLength(100, ErrorMessage = "Kategori adı en fazla 100 karakter olabilir.")]
        public string Ad { get; set; } = "";
    }
}
