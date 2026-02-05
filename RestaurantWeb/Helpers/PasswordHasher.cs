// PBKDF2 tabanlı parola hashleme ve doğrulama yardımcı sınıfı.
// Salt + yüksek iterasyon + sabit süreli karşılaştırma kullanır.

using System.Security.Cryptography;
using System.Text;

namespace RestaurantWeb.Helpers
{
    public static class PasswordHasher
    {
        // Standart güvenlik değerleri
        private const int SaltSize = 16;      // 128-bit
        private const int KeySize = 32;       // 256-bit
        private const int Iterations = 100_000; // Brute-force maliyetini artırır
        private static readonly HashAlgorithmName _hashAlgorithm = HashAlgorithmName.SHA256;

        // Yeni parola için hash + salt üretir
        public static (string HashBase64, string SaltBase64) CreateHash(string password)
        {
            // Rastgele salt üret
            var salt = RandomNumberGenerator.GetBytes(SaltSize);

            // PBKDF2 ile hash oluştur
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                Iterations,
                _hashAlgorithm,
                KeySize
            );

            // DB saklaması için Base64 formatı
            return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
        }

        // Girilen parolayı, kayıtlı hash ile doğrular
        public static bool Verify(string password, string hashBase64, string saltBase64)
        {
            // DB’den gelen değerleri byte dizisine çevir
            var salt = Convert.FromBase64String(saltBase64);
            var expectedHash = Convert.FromBase64String(hashBase64);

            // Aynı parametrelerle tekrar hashle
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                Iterations,
                _hashAlgorithm,
                KeySize
            );

            // Sabit süreli karşılaştırma (timing attack önleme)
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
    }
}