// Kriptografik olarak güvenli, okunabilir (ambiguous olmayan) rastgele parola üretir.

using System.Security.Cryptography; 

namespace RestaurantWeb.Helpers
{
    public static class PasswordGenerator
    {
        // 0/O, 1/I/l gibi karışan karakterler özellikle çıkarıldı
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789"; 

        public static string Generate(int length = 12) 
        {
            // Minimum uzunluk: zayıf parola üretimini engelle
            if (length < 8) length = 8; 

            var bytes = RandomNumberGenerator.GetBytes(length); 
            var chars = new char[length];

            for (int i = 0; i < length; i++)
            {
                chars[i] = Alphabet[bytes[i] % Alphabet.Length];
            }

            return new string(chars);
        }
    }
}
