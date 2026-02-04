namespace RestaurantWeb.Models
{
    public class Personel
    {
        public int Id { get; set; }
        public string AdSoyad { get; set; } = "";
        public string KullaniciAdi { get; set; } = "";
        public PersonelRol Rol { get; set; }
        public bool AktifMi { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
    }
}
