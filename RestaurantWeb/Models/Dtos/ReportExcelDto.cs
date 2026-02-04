namespace RestaurantWeb.Models.Dtos
{
    public class ReportExcelDto
    {
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        public string FileName { get; set; } = "report.xlsx";
        public string ContentType { get; set; } =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    }
}
