namespace RestaurantWeb.Models.ViewModels
{
    public class SiparisLogItemVm
    {
        public int Id { get; set; }
        public int SiparisId { get; set; }
        public string Action { get; set; } = "";
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? ActorUsername { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
