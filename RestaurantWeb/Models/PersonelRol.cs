namespace RestaurantWeb.Models
{
    [Flags]
    public enum PersonelRol
    {
        None = 0,
        Admin = 1 << 0, // 1
        Kasa = 1 << 1, // 2
        Garson = 1 << 2, // 4
        Mutfak = 1 << 3, // 8
    }
}
