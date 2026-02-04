namespace RestaurantWeb.Models.ViewModels
{
    public class RezervasyonListVm // *
    {
        public DateTime? Baslangic { get; set; } // *
        public DateTime? Bitis { get; set; } // *

        public short? Durum { get; set; } // null=tümü, 0=aktif,1=iptal // *
        public int? MasaNo { get; set; } // *
        public string? Q { get; set; } // müşteri ad / telefon araması // *

        public int Limit { get; set; } = 200; // *
        public List<RezervasyonListItemVm> Items { get; set; } = new(); // *
    }
}
