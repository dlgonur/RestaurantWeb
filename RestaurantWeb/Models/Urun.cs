namespace RestaurantWeb.Models
{
    public class Urun
    {
        public int Id { get; set; }
        public int KategoriId { get; set; }
        public string Ad { get; set; } = "";
        public decimal Fiyat { get; set; }
        public bool AktifMi { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
        public string KategoriAd { get; set; } = "";
        public int Stok { get; set; }

        public byte[]? Resim { get; set; } 
        public string? ResimMime { get; set; } 
        public string? ResimAdi { get; set; }
    }
}
