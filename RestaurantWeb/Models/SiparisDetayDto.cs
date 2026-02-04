namespace RestaurantWeb.Models
{
    public class SiparisDetayDto
    {
        public int UrunId { get; set; }
        public string UrunAd { get; set; } = "";
        public int Adet { get; set; }
        public decimal BirimFiyat { get; set; }
        public decimal SatirToplam { get; set; }
    }
}

