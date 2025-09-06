using System.Security.Cryptography;
using System.Text;

namespace FlowerShop.GeneratedCode
{
    public static class Generated
    {
        private readonly static char[] _numbers = "0123456789".ToCharArray();

        public static string GenerateRandomCode()
        {
            var result = new StringBuilder();

            using (var crypto = RandomNumberGenerator.Create())
            {
                for (int i = 0; i < 6; i++)
                {
                    var data = new byte[1];
                    crypto.GetBytes(data);

                    int rnd = data[0] % _numbers.Length;
                    result.Append(_numbers[rnd]);

                    if (i == 2)
                        result.Append('-');
                }
            }

            return result.ToString();
        }
    }
}
