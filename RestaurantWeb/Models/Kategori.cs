namespace RestaurantWeb.Models
{
    public class Kategori
    {
        public int Id { get; set; }
        public string Ad { get; set; } = "";
        public bool AktifMi { get; set; }
        public DateTime OlusturmaTarihi { get; set; }

    }
}
