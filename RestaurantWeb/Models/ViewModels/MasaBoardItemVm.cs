using RestaurantWeb.Models;

namespace RestaurantWeb.Models.ViewModels
{
    public class MasaBoardItemVm
    {
        public int Id { get; set; }
        public int MasaNo { get; set; }
        public int Kapasite { get; set; }
        public bool AktifMi { get; set; }

        public MasaDurumu DurumEfektif { get; set; } 
        public bool Blokeli { get; set; }           
        public string? RezMusteriAd { get; set; }    
        public DateTime? RezTarih { get; set; }

        public int? RezervasyonId { get; set; }
    }
}
