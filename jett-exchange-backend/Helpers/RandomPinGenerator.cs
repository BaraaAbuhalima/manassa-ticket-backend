using System.Security.Cryptography;

namespace jett_exchange_backend.Helpers;

public class RandomPinGenerator : IRandomPinGenerator
{
    private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public string Generate(int length = 16)
    {
        return string.Create(length, Chars, (span, chars) =>
        {
            Span<byte> bytes = stackalloc byte[length];
            RandomNumberGenerator.Fill(bytes);

            for (int i = 0; i < length; i++)
            {
                span[i] = chars[bytes[i] % chars.Length];
            }
        });
    }
}