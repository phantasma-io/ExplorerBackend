using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Database.Main;

public static class RejectedTransactionCandidateMethods
{
    public static async Task<RejectedTransactionCandidate> UpsertAsync(MainDbContext databaseContext,
        RejectedTransactionCandidate candidate)
    {
        if (candidate == null)
            throw new ArgumentNullException(nameof(candidate));

        var existing = await databaseContext.RejectedTransactionCandidates.FirstOrDefaultAsync(x =>
            x.NEXUS == candidate.NEXUS &&
            x.CHAIN == candidate.CHAIN &&
            x.HASH == candidate.HASH);

        if (existing == null)
        {
            await databaseContext.RejectedTransactionCandidates.AddAsync(candidate);
            return candidate;
        }

        existing.BLOCK_HEIGHT = candidate.BLOCK_HEIGHT;
        existing.BLOCK_HASH = candidate.BLOCK_HASH;
        existing.TIMESTAMP_UNIX_SECONDS = candidate.TIMESTAMP_UNIX_SECONDS;
        existing.STATE = candidate.STATE;
        existing.RESULT = candidate.RESULT;
        existing.DEBUG_COMMENT = candidate.DEBUG_COMMENT;
        existing.PAYLOAD = candidate.PAYLOAD;
        existing.SCRIPT_RAW = candidate.SCRIPT_RAW;
        existing.FEE_RAW = candidate.FEE_RAW;
        existing.EXPIRATION = candidate.EXPIRATION;
        existing.GAS_PRICE_RAW = candidate.GAS_PRICE_RAW;
        existing.GAS_LIMIT_RAW = candidate.GAS_LIMIT_RAW;
        existing.SENDER = candidate.SENDER;
        existing.GAS_PAYER = candidate.GAS_PAYER;
        existing.GAS_TARGET = candidate.GAS_TARGET;
        existing.CANONICAL_STATUS = candidate.CANONICAL_STATUS;
        existing.RPC_RESPONSE_JSON = candidate.RPC_RESPONSE_JSON;
        existing.BLOCK_RESPONSE_JSON = candidate.BLOCK_RESPONSE_JSON;
        existing.UPDATED_AT_UNIX_SECONDS = candidate.UPDATED_AT_UNIX_SECONDS;
        existing.LAST_SEEN_AT_UNIX_SECONDS = candidate.LAST_SEEN_AT_UNIX_SECONDS;

        return existing;
    }
}
