using System;
using System.Collections.Generic;

namespace RestaurantWeb.Models.ViewModels
{
    public class DashboardVm
    {
        public int SiparisSayisi { get; set; }
        public decimal ToplamCiro { get; set; }
        public decimal OrtalamaSepet { get; set; }
        public Dictionary<string, decimal> OdemeDagilimi { get; set; } = new();
        public DateTime Baslangic { get; set; }
        public DateTime Bitis { get; set; }    
        public List<(DateTime Gun, decimal Ciro)> GunlukCiro { get; set; } = new();
        public List<(string UrunAd, int Adet, decimal Ciro)> TopUrunler { get; set; } = new();
        public List<KategoriCiroVm> KategoriCiro { get; set; } = new();
        public string Mode { get; set; } = "payment";
        public List<PersonelPerfVm> PersonelPerformans { get; set; } = new();
        public int ToplamAktifMasa { get; set; }
        public int DoluAktifMasa { get; set; }
        public decimal MasaDolulukOraniPct { get; set; } 



    }
}
