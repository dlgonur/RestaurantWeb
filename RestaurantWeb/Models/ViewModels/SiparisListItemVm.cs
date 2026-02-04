namespace RestaurantWeb.Models.ViewModels
{
    public class SiparisListItemVm
    {
        public int SiparisId { get; set; }
        public int MasaId { get; set; }
        public int MasaNo { get; set; }
        public DateTime AcildiTarihi { get; set; }
        public DateTime? KapandiTarihi { get; set; }
        public decimal AraToplam { get; set; }
        public decimal IskontoTutar { get; set; }
        public decimal Toplam { get; set; }
        public string Durum { get; set; } = "";
        public string OdemeYontemi { get; set; } = ""; // boş olabilir
        public decimal OdemeTutar { get; set; }         // 0 olabilir
    }
}
