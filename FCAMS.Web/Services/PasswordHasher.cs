using System;
using System.Security.Cryptography;
using System.Text;

namespace FCAMS.Web.Services
{
    /// <summary>
    /// Salted SHA-256 password hashing.
    /// The encoding matches the SQL Server seed data, which hashes
    /// NVARCHAR password bytes followed by the salt.
    /// </summary>
    public static class PasswordHasher
    {
        private const int SaltSizeBytes = 32;

        public static byte[] GenerateSalt()
        {
            var salt = new byte[SaltSizeBytes];

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            return salt;
        }

        public static byte[] ComputeHash(string password, byte[] salt)
        {
            if (password == null)
                throw new ArgumentNullException(nameof(password));

            if (salt == null)
                throw new ArgumentNullException(nameof(salt));

            // SQL Server CAST(N'password' AS VARBINARY(...)) uses
            // UTF-16 little-endian bytes, matching Encoding.Unicode.
            var passwordBytes = Encoding.Unicode.GetBytes(password);

            var combined = new byte[passwordBytes.Length + salt.Length];

            Buffer.BlockCopy(
                passwordBytes,
                0,
                combined,
                0,
                passwordBytes.Length);

            Buffer.BlockCopy(
                salt,
                0,
                combined,
                passwordBytes.Length,
                salt.Length);

            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(combined);
            }
        }

        public static bool VerifyPassword(
            string password,
            byte[] salt,
            byte[] expectedHash)
        {
            if (password == null ||
                salt == null ||
                expectedHash == null)
            {
                return false;
            }

            var actualHash = ComputeHash(password, salt);

            if (actualHash.Length != expectedHash.Length)
                return false;

            int diff = 0;

            for (int i = 0; i < actualHash.Length; i++)
            {
                diff |= actualHash[i] ^ expectedHash[i];
            }

            return diff == 0;
        }
    }
}
