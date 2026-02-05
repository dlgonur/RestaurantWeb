// Kimlik doğrulama ve rol bazlı yönlendirme kurallarını içerir.
// Şifre doğrulama, aktiflik kontrolü ve landing kararları bu servistedir.

using Npgsql;
using RestaurantWeb.Data;
using RestaurantWeb.Helpers;
using RestaurantWeb.Models;
using RestaurantWeb.Models.Dtos;

namespace RestaurantWeb.Services
{
    public class AuthService : IAuthService
    {
        // DB bağlantı fabrikası
        private readonly INpgsqlConnectionFactory _cf;

        public AuthService(INpgsqlConnectionFactory cf)
        {
            _cf = cf;
        }

        // Kullanıcı adı / şifre doğrulaması yapar
        public OperationResult<AuthUserDto> ValidateCredentials(string kullaniciAdi, string sifrePlain)
        {
            kullaniciAdi = (kullaniciAdi ?? "").Trim();
            if (string.IsNullOrWhiteSpace(kullaniciAdi))
                return OperationResult<AuthUserDto>.Fail("Kullanıcı adı boş olamaz.");

            if (string.IsNullOrWhiteSpace(sifrePlain))
                return OperationResult<AuthUserDto>.Fail("Şifre boş olamaz.");

            const string sql = @"
SELECT id, kullanici_adi, sifre_hash, sifre_salt, rol, aktif_mi
FROM personeller
WHERE kullanici_adi = @u
LIMIT 1;
";

            try
            {
                using var conn = _cf.Create();
                conn.Open();

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@u", kullaniciAdi);

                using var r = cmd.ExecuteReader();
                if (!r.Read())
                    return OperationResult<AuthUserDto>.Fail("Kullanıcı adı veya şifre hatalı.");

                // DB -> DTO dönüşümü
                var dto = new AuthUserDto
                {
                    Id = r.GetInt32(0),
                    KullaniciAdi = r.GetString(1),
                    Hash = r.GetString(2),
                    Salt = r.GetString(3),
                    Rol = (PersonelRol)r.GetInt32(4),
                    AktifMi = r.GetBoolean(5)
                };

                if (!dto.AktifMi)
                    return OperationResult<AuthUserDto>.Fail("Hesap pasif.");

                var ok = PasswordHasher.Verify(sifrePlain, dto.Hash, dto.Salt);
                if (!ok)
                    return OperationResult<AuthUserDto>.Fail("Kullanıcı adı veya şifre hatalı.");

                return OperationResult<AuthUserDto>.Ok(dto);
            }
            catch (PostgresException ex)
            {
                return OperationResult<AuthUserDto>.Fail($"DB hatası. (Kod: {ex.SqlState})");
            }
            catch
            {
                return OperationResult<AuthUserDto>.Fail("Giriş sırasında beklenmeyen hata.");
            }
        }

        // Rol setine göre varsayılan controller belirler
        public string GetLandingController(PersonelRol roles)
        {
            if (roles.HasFlag(PersonelRol.Mutfak)) return "Mutfak";
            if (roles.HasFlag(PersonelRol.Garson)) return "Masalar";
            if (roles.HasFlag(PersonelRol.Kasa)) return "Raporlar";
            if (roles.HasFlag(PersonelRol.Admin)) return "Raporlar";
            return "Home";
        }

        // Rol setine göre varsayılan action belirler
        public string GetLandingAction(PersonelRol roles)
        {
            if (roles.HasFlag(PersonelRol.Mutfak)) return "Index";
            if (roles.HasFlag(PersonelRol.Garson)) return "Board";
            if (roles.HasFlag(PersonelRol.Kasa)) return "Dashboard";
            if (roles.HasFlag(PersonelRol.Admin)) return "Dashboard";
            return "Index";
        }
    }
}
