using System.Collections.Generic;
using RestaurantWeb.Models;

namespace RestaurantWeb.Models.ViewModels
{
    public class SiparisAdisyonVm
    {
        public List<SiparisDetayDto> Items { get; set; } = new();
        public decimal AraToplam { get; set; }
        public decimal IskontoOran { get; set; }
        public decimal IskontoTutar { get; set; }
        public decimal Toplam { get; set; }
    }
}
