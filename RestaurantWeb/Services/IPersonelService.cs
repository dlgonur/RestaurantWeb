using RestaurantWeb.Models;

namespace RestaurantWeb.Services
{
    public interface IPersonelService
    {
        OperationResult<List<Personel>> GetAllFiltered(bool? aktifMi, string? qAd, string? qUser); 
        OperationResult<Personel> GetById(int id); 

        OperationResult<int> Create(
            string adSoyad,
            string kullaniciAdi,
            string sifre,
            int rolMask,
            int? actorPersonelId,
            string? actorUsername,
            string? ip
        ); 

        OperationResult Update(
            int id,
            string adSoyad,
            string kullaniciAdi,
            int rolMask,
            int? actorPersonelId,
            string? actorUsername,
            string? ip
        ); 

        OperationResult<bool> ToggleAktif(int id, int? actorPersonelId, string? actorUsername, string? ip); 

        OperationResult SetPassword(
            int id,
            string kullaniciAdi,
            string newPassword,
            int? actorPersonelId,
            string? actorUsername,
            string? ip
        ); 
    }
}
