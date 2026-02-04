namespace RestaurantWeb.Models.ViewModels
{
    public class BackupIndexVm
    {
        public List<BackupItemVm> Items { get; set; } = new();
        public string? Info { get; set; }
    }

    public class BackupItemVm
    {
        public string FileName { get; set; } = "";
        public long SizeBytes { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
