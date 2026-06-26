using System.Security.Cryptography;
using System.Text;

namespace Aivora.Services.Utils;

public static class HashUtil
{
    /// <summary>
    /// Computes HMACSHA512 hash for the given data and key
    /// </summary>
    /// <param name="key">The secret key</param>
    /// <param name="data">The data to hash</param>
    /// <returns>Lowercase hexadecimal string of the hash</returns>
    public static string ComputeHmacSha512(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}