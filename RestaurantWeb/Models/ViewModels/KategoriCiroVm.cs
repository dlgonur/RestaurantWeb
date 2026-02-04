namespace RestaurantWeb.Models.ViewModels
{
    public class KategoriCiroVm
    {
        public string KategoriAd { get; set; } = "";
        public int ToplamAdet { get; set; }
        public decimal BrutCiro { get; set; }
        public decimal NetCiro { get; set; }
    }
}
