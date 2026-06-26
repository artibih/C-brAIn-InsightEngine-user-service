using System.Security.Cryptography;
using UsersService.Interfaces;

namespace UsersService.Services
{
    public class UtilityService : IUtilityService
    {
        public string GenerateSecurePassword(int length = 12)
        {
            if (length < 4)
                throw new ArgumentException("Password length must be at least 4 to include all character types.");

            const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lower = "abcdefghijklmnopqrstuvwxyz";
            const string digits = "0123456789";
            const string symbols = "!@#$%^&*()-_=+[]{}|;:,.<>?";

            using var rng = RandomNumberGenerator.Create();

            static int Next(RandomNumberGenerator rng, int max)
            {
                Span<byte> bytes = stackalloc byte[4];
                rng.GetBytes(bytes);
                var val = BitConverter.ToUInt32(bytes);
                return (int)(val % (uint)max);
            }

            var passwordChars = new List<char>
            {
                upper[Next(rng, upper.Length)],
                lower[Next(rng, lower.Length)],
                digits[Next(rng, digits.Length)],
                symbols[Next(rng, symbols.Length)]
            };

            string allChars = upper + lower + digits + symbols;
            while (passwordChars.Count < length)
            {
                passwordChars.Add(allChars[Next(rng, allChars.Length)]);
            }

            for (int i = passwordChars.Count - 1; i > 0; i--)
            {
                int j = Next(rng, i + 1);
                (passwordChars[i], passwordChars[j]) = (passwordChars[j], passwordChars[i]);
            }

            return new string(passwordChars.ToArray());
        }
    }
}