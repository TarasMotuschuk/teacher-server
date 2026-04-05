using System.Security.Cryptography;
using System.Text;

namespace Teacher.Common;

public static class VncPasswordHelper
{
    public static string Derive(string sharedSecret)
    {
        var secret = string.IsNullOrWhiteSpace(sharedSecret)
            ? "change-this-secret"
            : sharedSecret.Trim();

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hash)[..8];
    }
}
