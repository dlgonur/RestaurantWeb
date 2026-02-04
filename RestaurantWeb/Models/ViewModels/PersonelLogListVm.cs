namespace RestaurantWeb.Models.ViewModels
{
    public class PersonelLogListVm
    {
        public DateTime? Baslangic { get; set; } // *
        public DateTime? Bitis { get; set; } // *
        public string? Aksiyon { get; set; } // *
        public string? TargetKullaniciAdi { get; set; } // *

        public List<PersonelLog> Logs { get; set; } = new(); // *
    }
}
