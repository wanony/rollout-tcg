using System.Security.Cryptography;
using System.Text;

namespace TCGTrading.Pricing.Infrastructure.Ingestion;

/// <summary>
/// Derives a stable, deterministic Guid from an arbitrary string key (e.g. ProviderCardId),
/// so callers that don't have a real Guid can still get a consistent identity to key on.
/// Same input always produces the same Guid; different inputs are (practically) distinct.
/// </summary>
public static class DeterministicGuid
{
    public static Guid From(string key)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(hash[..16]);
    }
}
