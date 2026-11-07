using HoneyDrunk.Memory.Abstractions;
using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace HoneyDrunk.Memory;

/// <summary>
/// Derives an operational read's approval id from the request, so one decision binds exactly one
/// request: the same requester, keys, purpose and expiry.
/// </summary>
internal static class OperationalApprovalId
{
    private const string Prefix = "memory-read-";
    private const int HexLength = 32;

    /// <summary>
    /// Returns <c>memory-read-</c> plus the first 32 hex characters of SHA-256 over the canonical
    /// request: a JSON array of the requester, the ordinally sorted key texts, the purpose and the
    /// expiry in UTC round-trip (<c>"O"</c>) format.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The approval id.</returns>
    public static string For(MemoryOperationalReadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            writer.WriteStringValue(request.RequesterId);
            writer.WriteStartArray();
            foreach (var key in request.Scopes.Select(key => key.ToString()).Order(StringComparer.Ordinal))
            {
                writer.WriteStringValue(key);
            }

            writer.WriteEndArray();
            writer.WriteStringValue(request.Purpose);
            writer.WriteStringValue(request.Expiry.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            writer.WriteEndArray();
        }

        var hash = SHA256.HashData(buffer.WrittenSpan);
        return Prefix + Convert.ToHexStringLower(hash)[..HexLength];
    }
}
