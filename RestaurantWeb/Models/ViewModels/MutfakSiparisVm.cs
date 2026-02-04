namespace RestaurantWeb.Models.ViewModels
{
    public class MutfakSiparisVm
    {
        public int SiparisId { get; set; }
        public int MasaNo { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
        public List<MutfakKalemVm> Kalemler { get; set; } = new();
    }

    public class MutfakKalemVm
    {
        public int KalemId { get; set; }
        public string UrunAd { get; set; } = "";
        public int Adet { get; set; }
        public short Durum { get; set; } // 0 bekliyor, 1 hazirlaniyor, 2 hazir, 3 servise cikti 

    }
}
