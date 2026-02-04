namespace RestaurantWeb.Models.Dtos
{
    public class CatalogProductDto
    {
        public int Id { get; set; }
        public string Ad { get; set; } = "";
        public decimal Fiyat { get; set; }
        public int Stok { get; set; }
        public string Kategori { get; set; } = "";
    }
}
