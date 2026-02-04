
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace RestaurantWeb.Models.ViewModels
{
    public class UrunEditVm
    {
        [Range(1, int.MaxValue, ErrorMessage = "Geçersiz ürün id.")]
        public int Id { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir kategori seçiniz.")]
        public int KategoriId { get; set; }

        [Required(ErrorMessage = "Ürün adı boş bırakılamaz.")]
        [StringLength(150, ErrorMessage = "Ürün adı en fazla 150 karakter olabilir.")]
        public string Ad { get; set; } = "";

        [Range(0, double.MaxValue, ErrorMessage = "Fiyat 0'dan küçük olamaz.")]
        public decimal Fiyat { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Stok 0'dan küçük olamaz.")]
        public int Stok { get; set; } = 0;

	    public IFormFile? Resim { get; set; }

		public bool ResimVar { get; set; }

        public bool ResmiKaldir { get; set; }
    }
}
