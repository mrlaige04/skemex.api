using System.Security.Cryptography;
using System.Text;

namespace Skemex.Application.Services;

public class GenerationService
{
    private const string AllowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%&";

    public static string GenerateRandomString(int length)
    {
        var key = new StringBuilder();
        using var rng = RandomNumberGenerator.Create();
        var buffer = new byte[sizeof(uint)];

        for (var i = 0; i < length; i++)
        {
            rng.GetBytes(buffer);
            var num = BitConverter.ToUInt32(buffer, 0);
            key.Append(AllowedChars[(int)(num % (uint)AllowedChars.Length)]);
        }

        return key.ToString();
    }
}