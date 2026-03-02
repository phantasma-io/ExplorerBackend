using System;
using System.Numerics;

namespace Backend.Blockchain;

public partial class PhantasmaPlugin
{
    // Token IDs are 256-bit unsigned integers on-chain (typically derived from hashes).
    //
    // Legacy data issue:
    // Some decoding paths materialize them as *signed* BigInteger, producing negative values when the top bit is set.
    // This leaks into Explorer DB as negative TOKEN_ID strings, and the node RPC rejects such IDs with "invalid ID".
    //
    // This helper normalizes IDs to [0, 2^256) to keep Explorer compatible with the node.
    //
    // TODO(legacy): Remove this shim once:
    // - the upstream decoding always yields unsigned token IDs for NFTs, AND
    // - all legacy negative TOKEN_ID values are backfilled/normalized in the Explorer database.
    private static readonly BigInteger TokenIdModulus = BigInteger.One << 256;

    private static string NormalizeTokenId(BigInteger value)
    {
        if (value.Sign >= 0)
            return value.ToString();

        // Convert two's-complement signed value to unsigned 256-bit representation.
        var normalized = value % TokenIdModulus;
        if (normalized.Sign < 0)
            normalized += TokenIdModulus;

        return normalized.ToString();
    }

    private static bool TryNormalizeTokenIdText(string tokenIdText, out string normalized)
    {
        normalized = tokenIdText;

        if (string.IsNullOrWhiteSpace(tokenIdText) || tokenIdText[0] != '-')
            return false;

        if (!BigInteger.TryParse(tokenIdText, out var parsed))
            return false;

        normalized = NormalizeTokenId(parsed);
        return !string.Equals(normalized, tokenIdText, StringComparison.Ordinal);
    }
}
