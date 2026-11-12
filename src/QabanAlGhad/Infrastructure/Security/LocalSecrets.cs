using System;
using System.Text;
using System.Security.Cryptography;

namespace WeighbridgeBridge
{
    public static class LocalSecrets
    {
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("QabanAlGhad local API credential v1");

        public static string Protect(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";
            return Convert.ToBase64String(
                ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser));
        }

        public static bool TryUnprotect(string value, out string plaintext)
        {
            plaintext = "";
            if (string.IsNullOrWhiteSpace(value))
                return true;
            try
            {
                plaintext = Encoding.UTF8.GetString(
                    ProtectedData.Unprotect(Convert.FromBase64String(value), Entropy, DataProtectionScope.CurrentUser));
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        public static string Unprotect(string value)
        {
            string plaintext;
            return TryUnprotect(value, out plaintext) ? plaintext : "";
        }

        // A failed decryption must never turn a stored credential into an empty one.
        public static string UpdateProtected(string previous, string replacement)
        {
            string ignored;
            if (string.IsNullOrWhiteSpace(replacement) && !TryUnprotect(previous, out ignored))
                return previous;
            return Protect(replacement);
        }
    }
}
