using System.Security.Cryptography;
using System.Text;

namespace FlowerShop.GeneratedCode
{
    public static class Generated
    {
        private static readonly string _key = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";

        public static string GenerateKey(int length = 7)
        {
            var result = new StringBuilder();
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] buffer = new byte[4];
                for (int i = 0; i < length; i++)
                {
                    rng.GetBytes(buffer);
                    int index = BitConverter.ToInt32(buffer, 0) & int.MaxValue;
                    result.Append(_key[index % _key.Length]);
                }
            }

            if (result.Length > 3)
                result.Insert(3, '-');

            return result.ToString();
        }
    }
}
