namespace RestaurantWeb.Models
{
    public class PersonelLog
    {
        public int Id { get; set; } 
        public int? ActorPersonelId { get; set; } 
        public string? ActorKullaniciAdi { get; set; } 

        public int? TargetPersonelId { get; set; }  
        public string? TargetKullaniciAdi { get; set; } 

        public string Aksiyon { get; set; } = ""; 
        public int? OldRol { get; set; } 
        public int? NewRol { get; set; } 
        public bool? OldAktifMi { get; set; } 
        public bool? NewAktifMi { get; set; } 

        public string? Aciklama { get; set; } 
        public string? Ip { get; set; } 
        public DateTime CreatedAt { get; set; } 
    }
}
