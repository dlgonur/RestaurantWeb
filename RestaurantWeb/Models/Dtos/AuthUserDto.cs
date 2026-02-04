using RestaurantWeb.Models;

namespace RestaurantWeb.Models.Dtos
{
    public class AuthUserDto
    {
        public int Id { get; set; }
        public string KullaniciAdi { get; set; } = "";
        public string Hash { get; set; } = "";
        public string Salt { get; set; } = "";
        public PersonelRol Rol { get; set; }
        public bool AktifMi { get; set; }
    }
}
