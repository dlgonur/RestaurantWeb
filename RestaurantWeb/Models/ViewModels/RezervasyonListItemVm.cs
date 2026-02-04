namespace RestaurantWeb.Models.ViewModels
{
    public class RezervasyonListItemVm // *
    {
        public int Id { get; set; } // *
        public int MasaId { get; set; } // *
        public int MasaNo { get; set; } // *
        public string MusteriAd { get; set; } = ""; // *
        public string? Telefon { get; set; } // *
        public DateTime RezervasyonTarihi { get; set; } // *
        public int? KisiSayisi { get; set; } // *
        public string? Notlar { get; set; } // *
        public short Durum { get; set; } // 0=Aktif,1=Iptal // *
        public DateTime OlusturmaTarihi { get; set; } // *
    }
}
