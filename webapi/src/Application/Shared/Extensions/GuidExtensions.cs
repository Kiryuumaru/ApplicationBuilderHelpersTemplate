using System.Text;

namespace Application.Shared.Extensions;

public static class GuidExtensions
{
    public static string Encode(this Guid guid)
    {
        string enc = Convert.ToBase64String(guid.ToByteArray());
        enc = enc.Replace("/", "_");
        enc = enc.Replace("+", "-");
        return enc[..22];
    }
}
