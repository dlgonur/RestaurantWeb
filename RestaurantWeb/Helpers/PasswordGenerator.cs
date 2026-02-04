using System.Security.Cryptography; // ★

namespace RestaurantWeb.Helpers
{
    public static class PasswordGenerator
    {
        // ★ URL-safe, kopyalaması kolay, sadece [A-Za-z0-9]
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789"; // ★ (0,O,1,l yok)

        public static string Generate(int length = 12) // ★
        {
            if (length < 8) length = 8; // ★

            var bytes = RandomNumberGenerator.GetBytes(length); // ★
            var chars = new char[length];

            for (int i = 0; i < length; i++)
            {
                chars[i] = Alphabet[bytes[i] % Alphabet.Length];
            }

            return new string(chars);
        }
    }
}
