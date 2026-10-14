using System.Security.Cryptography;
using System.Text;

namespace RailwayTicketing.Api.Services;

/// <summary>
/// HMAC-SHA256 helper used to verify payment provider webhook callbacks.
/// </summary>
public static class PaymentSignature
{
    public static string Compute(string rawBody, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret ?? string.Empty));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody ?? string.Empty));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool Verify(string? signature, string rawBody, string secret)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var expected = Compute(rawBody, secret);
        var provided = signature.Trim().ToLowerInvariant();

        if (provided.Length != expected.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(provided));
    }
}
