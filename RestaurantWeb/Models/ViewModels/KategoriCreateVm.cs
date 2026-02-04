using System.ComponentModel.DataAnnotations;

namespace RestaurantWeb.Models.ViewModels
{
    public class KategoriCreateVm
    {
        [Required(ErrorMessage = "Kategori adı boş bırakılamaz.")]
        [StringLength(100, ErrorMessage = "Kategori adı en fazla 100 karakter olabilir.")]
        public string Ad { get; set; } = "";
    }
}
