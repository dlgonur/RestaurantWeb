using System.ComponentModel.DataAnnotations;

namespace RestaurantWeb.Models.ViewModels
{
    public class SiparisListVm
    {
        [DataType(DataType.Date)]
        public DateTime Baslangic { get; set; }

        [DataType(DataType.Date)]
        public DateTime Bitis { get; set; }

        public int? MasaNo { get; set; } // opsiyonel filtre

        public List<SiparisListItemVm> Items { get; set; } = new();
    }
}
