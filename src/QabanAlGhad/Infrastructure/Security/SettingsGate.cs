using System;
using System.Security.Cryptography;
namespace WeighbridgeBridge
{
    public static class SettingsGate
    {
        public static bool Verify(string password)
        {
            if (password == null || password.Length > 128)
                return false;
            byte[] expected = Convert.FromBase64String("CY+CRxfNhn8svokzVJzUY0+PHiwZwTaZgL4/WuAbkKI=");
            using (var derive =
                       new Rfc2898DeriveBytes(password, Convert.FromBase64String("E7Y6+HLNSWrQ6Zn8TlNNNg=="), 100000))
            {
                byte[] actual = derive.GetBytes(32);
                int difference = 0;
                for (int i = 0; i < 32; i++)
                    difference |= actual[i] ^ expected[i];
                return difference == 0;
            }
        }
    }
}
