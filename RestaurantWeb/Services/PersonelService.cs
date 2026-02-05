// Personel iş kuralları + audit log orkestrasyonu:
// - Rol mask doğrulama (Flags) + normalize
// - CRUD çağrılarını repo’ya delege eder
// - CREATE/UPDATE/TOGGLE_ACTIVE/SET_PASSWORD aksiyonlarını personel_loglari tablosuna yazar
// Not: Log yazımı TryLog ile “best-effort”; log hatası ana akışı bozmaz.

using RestaurantWeb.Data;
using RestaurantWeb.Models;

namespace RestaurantWeb.Services
{
    public class PersonelService : IPersonelService
    {
        private readonly PersonelRepository _repo; 
        private readonly PersonelLogRepository _logRepo; 
        private readonly ILogger<PersonelService> _logger; 

        public PersonelService(
            PersonelRepository repo,
            PersonelLogRepository logRepo,
            ILogger<PersonelService> logger)
        {
            _repo = repo; 
            _logRepo = logRepo; 
            _logger = logger; 
        }

        // Audit log yazımı: başarısız olursa sadece warning basar, akışı bozmaz
        private void TryLog(PersonelLog log) 
        {
            try
            {
                _logRepo.Add(log);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Personel audit log yazılamadı. Aksiyon: {Aksiyon}", log.Aksiyon);
            }
        }

        // Log nesnesini tek yerden üretmek için yardımcı
        private void LogAction( 
            string aksiyon,
            int? actorPersonelId,
            string? actorUsername,
            int? targetPersonelId,
            string? targetUsername,
            int? oldRol,
            int? newRol,
            bool? oldAktif,
            bool? newAktif,
            string? ip,
            string? aciklama)
        {
            TryLog(new PersonelLog
            {
                ActorPersonelId = actorPersonelId,
                ActorKullaniciAdi = actorUsername,
                TargetPersonelId = targetPersonelId,
                TargetKullaniciAdi = targetUsername,
                Aksiyon = aksiyon,
                OldRol = oldRol,
                NewRol = newRol,
                OldAktifMi = oldAktif,
                NewAktifMi = newAktif,
                Ip = ip,
                Aciklama = aciklama
            });
        }

        // UI’dan gelen rol mask’ini doğrular (0/None kabul edilmez)
        private static PersonelRol ParseAndValidateRolMask(int rolMask, out string? error) 
        {
            error = null;

            if (rolMask <= 0)
            {
                error = "En az bir rol seçiniz.";
                return PersonelRol.None;
            }

            var rol = (PersonelRol)rolMask;

            if ((int)rol <= 0)
            {
                error = "Geçerli bir rol seçiniz.";
                return PersonelRol.None;
            }

            return rol;
        }

        public OperationResult<List<Personel>> GetAllFiltered(bool? aktifMi, string? qAd, string? qUser) 
        {
            qAd = (qAd ?? "").Trim();
            qUser = (qUser ?? "").Trim();
            return _repo.GetAllFiltered(aktifMi, qAd, qUser);
        }

        public OperationResult<Personel> GetById(int id) => _repo.GetById(id);

        // Personel oluşturma: repo ekler; ardından CREATE log’u yazar
        public OperationResult<int> Create( 
            string adSoyad,
            string kullaniciAdi,
            string sifre,
            int rolMask,
            int? actorPersonelId,
            string? actorUsername,
            string? ip)
        {
            adSoyad = (adSoyad ?? "").Trim();
            kullaniciAdi = (kullaniciAdi ?? "").Trim();

            var rol = ParseAndValidateRolMask(rolMask, out var rolErr);
            if (rolErr != null)
                return OperationResult<int>.Fail(rolErr);

            var addRes = _repo.Add(adSoyad, kullaniciAdi, sifre, rol);
            if (!addRes.Success)
                return addRes;

            LogAction(
                aksiyon: "CREATE",
                actorPersonelId: actorPersonelId,
                actorUsername: actorUsername,
                targetPersonelId: addRes.Data,
                targetUsername: kullaniciAdi,
                oldRol: null,
                newRol: (int)rol,
                oldAktif: null,
                newAktif: true,
                ip: ip,
                aciklama: $"Personel eklendi: {adSoyad}"
            );

            return addRes;
        }

        // Güncelleme: eski state’i log için okur; repo update; UPDATE log’u yazar
        public OperationResult Update( 
            int id,
            string adSoyad,
            string kullaniciAdi,
            int rolMask,
            int? actorPersonelId,
            string? actorUsername,
            string? ip)
        {
            if (id <= 0)
                return OperationResult.Fail("Geçersiz personel id.");

            adSoyad = (adSoyad ?? "").Trim();
            kullaniciAdi = (kullaniciAdi ?? "").Trim();

            var rol = ParseAndValidateRolMask(rolMask, out var rolErr);
            if (rolErr != null)
                return OperationResult.Fail(rolErr);

            var oldRes = _repo.GetById(id);
            var old = oldRes.Success ? oldRes.Data : null;

            var updRes = _repo.Update(id, adSoyad, kullaniciAdi, rol);
            if (!updRes.Success)
                return updRes;

            LogAction(
                aksiyon: "UPDATE",
                actorPersonelId: actorPersonelId,
                actorUsername: actorUsername,
                targetPersonelId: id,
                targetUsername: kullaniciAdi,
                oldRol: old != null ? (int)old.Rol : null,
                newRol: (int)rol,
                oldAktif: old?.AktifMi,
                newAktif: old?.AktifMi, 
                ip: ip,
                aciklama: "Personel bilgileri güncellendi"
            );

            return updRes;
        }

        // Aktif/pasif: repo toggle döndürür; önceki state ile TOGGLE_ACTIVE log’u yazar
        public OperationResult<bool> ToggleAktif( 
            int id,
            int? actorPersonelId,
            string? actorUsername,
            string? ip)
        {
            if (id <= 0)
                return OperationResult<bool>.Fail("Geçersiz personel id.");

            var oldRes = _repo.GetById(id);
            var old = oldRes.Success ? oldRes.Data : null;

            var res = _repo.ToggleAktif(id);
            if (!res.Success)
                return res;

            LogAction(
                aksiyon: "TOGGLE_ACTIVE",
                actorPersonelId: actorPersonelId,
                actorUsername: actorUsername,
                targetPersonelId: id,
                targetUsername: old?.KullaniciAdi,
                oldRol: old != null ? (int)old.Rol : null,
                newRol: old != null ? (int)old.Rol : null,
                oldAktif: old?.AktifMi,
                newAktif: res.Data,
                ip: ip,
                aciklama: $"{res.Message} (TargetId={id})"
            );

            return res;
        }

        // Şifre reset: repo PBKDF2+salt ile günceller; SET_PASSWORD log’u yazar
        public OperationResult SetPassword( 
            int id,
            string kullaniciAdi,
            string newPassword,
            int? actorPersonelId,
            string? actorUsername,
            string? ip)
        {
            if (id <= 0)
                return OperationResult.Fail("Geçersiz personel id.");

            kullaniciAdi = (kullaniciAdi ?? "").Trim();
            newPassword = (newPassword ?? "").Trim();

            if (string.IsNullOrWhiteSpace(newPassword))
                return OperationResult.Fail("Yeni şifre boş olamaz.");

            var targetRes = _repo.GetById(id);
            var t = targetRes.Success ? targetRes.Data : null;

            var res = _repo.ResetPassword(id, newPassword);
            if (!res.Success)
                return res;

            LogAction(
                aksiyon: "SET_PASSWORD",
                actorPersonelId: actorPersonelId,
                actorUsername: actorUsername,
                targetPersonelId: id,
                targetUsername: t?.KullaniciAdi ?? kullaniciAdi,
                oldRol: t != null ? (int)t.Rol : null,
                newRol: t != null ? (int)t.Rol : null,
                oldAktif: t?.AktifMi,
                newAktif: t?.AktifMi,
                ip: ip,
                aciklama: "Admin tarafından şifre değiştirildi"
            );

            return OperationResult.Ok("Şifre güncellendi.");
        }
    }
}
