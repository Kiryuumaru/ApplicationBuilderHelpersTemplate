using System.Security.Cryptography;
using System.Text;
using Application.Shared.Extensions;

namespace Application.Shared.Utilities;

public static class GuidHelpers
{
    public static string Encode(string guidText)
    {
        Guid guid = new(guidText);
        return guid.Encode();
    }

    public static Guid Decode(string encoded)
    {
        encoded = encoded.Replace("_", "/");
        encoded = encoded.Replace("-", "+");
        byte[] buffer = Convert.FromBase64String(encoded + "==");
        return new Guid(buffer);
    }

    public static Guid GenerateSeeded(string seed)
    {
        byte[] hashBytes = SHA1.HashData(Encoding.UTF8.GetBytes(seed));

        byte[] guidBytes = new byte[16];
        Array.Copy(hashBytes, guidBytes, 16);

        guidBytes[7] = (byte)((guidBytes[7] & 0x0F) | 0x40);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);

        return new Guid(guidBytes);
    }
}
