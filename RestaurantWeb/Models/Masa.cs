namespace RestaurantWeb.Models
{
    public class Masa
    {
        public int Id { get; set; }
        public int MasaNo { get; set; }
        public int Kapasite { get; set; }
        public bool AktifMi { get; set; }
        public MasaDurumu Durum { get; set; } = MasaDurumu.Bos; // *

    }
}
