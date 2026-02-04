namespace RestaurantWeb.Models.ViewModels
{
    public class MasaBoardVm
    {
        public List<MasaBoardItemVm> Items { get; set; } = new();

        public int AktifMasa { get; set; }
        public int DoluMasa { get; set; }
        public int BlokeliBosMasa { get; set; }
        public int WalkinBosMasa { get; set; }
        public decimal FizikselDoluluk { get; set; }
        public decimal EfektifDoluluk { get; set; }
    }
}
