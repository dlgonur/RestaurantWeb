namespace RestaurantWeb.Models.ViewModels
{
    public class SiparisDetayVm
    {
        public int SiparisId { get; set; }
        public int MasaId { get; set; }
        public int MasaNo { get; set; }

        public DateTime AcildiTarihi { get; set; }
        public DateTime? KapandiTarihi { get; set; }

        public short Durum { get; set; } // 0 açık, 1 kapalı (senin enum'a göre)
        public decimal AraToplam { get; set; }
        public decimal IskontoOrani { get; set; } // % (0-100)
        public decimal IskontoTutar { get; set; }
        public decimal Toplam { get; set; }

        public string OdemeYontemi { get; set; } = "";
        public decimal OdemeTutar { get; set; }
        public DateTime? OdemeTarihi { get; set; }

        public List<SiparisKalemDetayVm> Kalemler { get; set; } = new();
    }
}
