using RestaurantWeb.Models;

namespace RestaurantWeb.Models.ViewModels
{
    public class KategoriDeleteVm
    {
        public Kategori Kategori { get; set; } = new();
        public bool HasProducts { get; set; }
        public List<Urun> BagliUrunler { get; set; } = new(); 

    }
}
