using System.Security.Cryptography;
using System.Text;

namespace RestaurantWeb.Helpers
{
    public static class PasswordHasher
    {
        // Sabitler: Standart güvenlik değerleri
        private const int SaltSize = 16;      // 128-bit
        private const int KeySize = 32;       // 256-bit
        private const int Iterations = 100_000; // Deneme sayısı (Yüksek olması iyidir)
        private static readonly HashAlgorithmName _hashAlgorithm = HashAlgorithmName.SHA256;

        public static (string HashBase64, string SaltBase64) CreateHash(string password)
        {
            // 1. Rastgele Salt oluştur
            var salt = RandomNumberGenerator.GetBytes(SaltSize);

            // 2. Hash'i oluştur (.NET 8 ile gelen modern yöntem)
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                Iterations,
                _hashAlgorithm,
                KeySize
            );

            // 3. Veritabanına kaydedilecek string formatına çevir
            return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
        }

        public static bool Verify(string password, string hashBase64, string saltBase64)
        {
            // 1. Veritabanından gelen stringleri byte dizisine çevir
            var salt = Convert.FromBase64String(saltBase64);
            var expectedHash = Convert.FromBase64String(hashBase64);

            // 2. Kullanıcının girdiği şifreyi, aynı salt ile tekrar hashle
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                Iterations,
                _hashAlgorithm,
                KeySize
            );

            // 3. İki hash'i güvenli bir şekilde karşılaştır
            // (FixedTimeEquals: Saldırganın işlem süresinden şifreyi tahmin etmesini engeller)
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
    }
}