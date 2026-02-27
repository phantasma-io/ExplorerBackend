using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Commons;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Serilog;

namespace Database.Main;

public static class EventMethods
{
    // Checks if "Events" table has entry with given name,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.


    public static void DeleteByNftId(MainDbContext databaseContext, int nftId)
    {
        var tokenEvents = databaseContext.Events.Where(x => x.NftId == nftId);
        foreach (var tokenEvent in tokenEvents) databaseContext.Entry(tokenEvent).State = EntityState.Deleted;
    }


    private static void ProcessBurnedNft(MainDbContext databaseContext, Nft nft)
    {
        // Burn must detach any infused children from the burned parent NFT.
        // Query persisted children by FK and merge unsaved tracked children as a fallback.
        var persistedInfusedNfts = nft.ID > 0
            ? databaseContext.Nfts.Where(x => x.InfusedIntoId == nft.ID).ToList()
            : new List<Nft>();
        var trackedInfusedNfts = DbHelper.GetTracked<Nft>(databaseContext)
            .Where(x => x.InfusedInto == nft)
            .ToList();

        // Detach is idempotent, so duplicated references are safe if an entity is both
        // persisted and currently tracked in the DbContext.
        if (trackedInfusedNfts.Count > 0)
            persistedInfusedNfts.AddRange(trackedInfusedNfts);

        Log.Verbose("Got {Count} Ntfs to defuse", persistedInfusedNfts.Count);

        foreach (var item in persistedInfusedNfts)
        {
            item.InfusedInto = null;
            Log.Information("NFT defused: {DefusedNft} from NFT {Nft}", item.TOKEN_ID, nft.TOKEN_ID);
        }
    }


    public static Event GetNextId(MainDbContext dbContext, int skip)
    {
        return dbContext.Events.OrderByDescending(x => x.ID).Skip(skip).FirstOrDefault();
    }

    public static Event Upsert(MainDbContext databaseContext,
        out bool newEventCreated,
        long timestampUnixSeconds,
        int index,
        Chain chain,
        Transaction transaction,
        int contractId,
        int eventKindId,
        Address address,
        bool skipAddressTransactionExistsCheck = false,
        bool createAddressTransactionLink = true,
        bool trackInContext = true)
    {
        newEventCreated = false;

        var eventEntry = new Event
        {
            DM_UNIX_SECONDS = UnixSeconds.Now(),
            TIMESTAMP_UNIX_SECONDS = timestampUnixSeconds,
            DATE_UNIX_SECONDS = UnixSeconds.GetDate(timestampUnixSeconds),
            INDEX = index,
            ChainId = chain?.ID ?? 0,
            Chain = chain,
            TransactionId = transaction?.ID ?? 0,
            Transaction = transaction,
            ContractId = contractId,
            EventKindId = eventKindId,
            AddressId = address?.ID ?? 0,
            Address = address
        };

        if (trackInContext)
            databaseContext.Events.Add(eventEntry);

        newEventCreated = true;

        if (createAddressTransactionLink)
        {
            AddressTransactionMethods.UpsertAsync(databaseContext, address, transaction, !skipAddressTransactionExistsCheck)
                .GetAwaiter().GetResult();
        }

        return eventEntry;
    }


    public static async Task<bool> UpdateValuesAsync(MainDbContext databaseContext, Event eventItem, Nft nft,
        string tokenId, Chain chain, PhantasmaPhoenix.Protocol.EventKind eventKind, int eventKindId, int contractId)
    {
        var eventUpdated = false;

        if (eventItem == null) return eventUpdated;

        eventItem.ChainId = chain?.ID ?? eventItem.ChainId;
        eventItem.Chain = chain;
        eventItem.ContractId = contractId;
        eventItem.EventKindId = eventKindId;
        eventItem.TOKEN_ID = tokenId;
        eventItem.NftId = nft?.ID > 0 ? nft.ID : null;
        eventItem.Nft = nft;

        eventUpdated = true;

        if (eventKind != PhantasmaPhoenix.Protocol.EventKind.TokenBurn || nft == null)
            return eventUpdated;

        //TODO check if always needed
        // For burns we must release all infused nfts.

        var startTime = DateTime.Now;
        ProcessBurnedNft(databaseContext, nft);
        var updateTime = DateTime.Now - startTime;
        Log.Verbose("Process Burned, processed in {Time} sec", Math.Round(updateTime.TotalSeconds, 3));

        return eventUpdated;
    }

    private static int ResolveForeignKeyId(int explicitId, int navigationId, string columnName)
    {
        var resolvedId = explicitId > 0 ? explicitId : navigationId;
        if (resolvedId <= 0)
            throw new InvalidOperationException($"Cannot insert event batch: missing FK `{columnName}`.");

        return resolvedId;
    }

    private static async Task<int[]> ReserveEventIdsAsync(NpgsqlConnection dbConnection,
        NpgsqlTransaction dbTransaction, int rowCount)
    {
        await using var reserveIdsCmd = new NpgsqlCommand(@"
SELECT nextval(pg_get_serial_sequence('""Events""', 'ID'))::integer
FROM generate_series(1, @row_count);
", dbConnection, dbTransaction);

        reserveIdsCmd.Parameters.Add("@row_count", NpgsqlDbType.Integer).Value = rowCount;

        var reservedIds = new int[rowCount];
        var index = 0;

        await using var reader = await reserveIdsCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            reservedIds[index] = reader.GetInt32(0);
            index++;
        }

        if (index != rowCount)
            throw new InvalidOperationException(
                $"Failed to reserve event IDs for batch insert: expected {rowCount}, got {index}.");

        return reservedIds;
    }

    // Event ingest can produce hundreds of rows per block.
    // We insert them set-based to avoid per-entity EF tracking and per-row INSERT overhead.
    public static async Task InsertBatchAsync(NpgsqlConnection dbConnection, NpgsqlTransaction dbTransaction,
        IReadOnlyList<Event> events)
    {
        if (events == null || events.Count == 0)
            return;

        var rowCount = events.Count;
        var reservedIds = await ReserveEventIdsAsync(dbConnection, dbTransaction, rowCount);

        var ids = new int[rowCount];
        var dmUnixSeconds = new long[rowCount];
        var timestampUnixSeconds = new long[rowCount];
        var dateUnixSeconds = new long[rowCount];
        var indexes = new int[rowCount];
        var tokenIds = new string[rowCount];
        var burned = new bool?[rowCount];
        var nsfw = new bool[rowCount];
        var blacklisted = new bool[rowCount];
        var payloadFormats = new string[rowCount];
        var payloadJson = new string[rowCount];
        var rawData = new string[rowCount];
        var addressIds = new int[rowCount];
        var chainIds = new int[rowCount];
        var contractIds = new int[rowCount];
        var transactionIds = new int[rowCount];
        var eventKindIds = new int[rowCount];
        var nftIds = new int?[rowCount];
        var targetAddressIds = new int?[rowCount];

        for (var i = 0; i < rowCount; i++)
        {
            var eventEntry = events[i];
            var id = reservedIds[i];

            eventEntry.ID = id;

            ids[i] = id;
            dmUnixSeconds[i] = eventEntry.DM_UNIX_SECONDS;
            timestampUnixSeconds[i] = eventEntry.TIMESTAMP_UNIX_SECONDS;
            dateUnixSeconds[i] = eventEntry.DATE_UNIX_SECONDS;
            indexes[i] = eventEntry.INDEX;
            tokenIds[i] = eventEntry.TOKEN_ID;
            burned[i] = eventEntry.BURNED;
            nsfw[i] = eventEntry.NSFW;
            blacklisted[i] = eventEntry.BLACKLISTED;
            payloadFormats[i] = eventEntry.PAYLOAD_FORMAT;
            payloadJson[i] = eventEntry.PAYLOAD_JSON;
            rawData[i] = eventEntry.RAW_DATA;
            addressIds[i] = ResolveForeignKeyId(eventEntry.AddressId, eventEntry.Address?.ID ?? 0, "AddressId");
            chainIds[i] = ResolveForeignKeyId(eventEntry.ChainId, eventEntry.Chain?.ID ?? 0, "ChainId");
            contractIds[i] = eventEntry.ContractId;
            transactionIds[i] =
                ResolveForeignKeyId(eventEntry.TransactionId, eventEntry.Transaction?.ID ?? 0, "TransactionId");
            eventKindIds[i] = eventEntry.EventKindId;

            var resolvedNftId = eventEntry.NftId.GetValueOrDefault() > 0
                ? eventEntry.NftId
                : eventEntry.Nft?.ID > 0
                    ? eventEntry.Nft.ID
                    : null;
            var resolvedTargetAddressId = eventEntry.TargetAddressId.GetValueOrDefault() > 0
                ? eventEntry.TargetAddressId
                : eventEntry.TargetAddress?.ID > 0
                    ? eventEntry.TargetAddress.ID
                    : null;

            nftIds[i] = resolvedNftId;
            targetAddressIds[i] = resolvedTargetAddressId;

            eventEntry.AddressId = addressIds[i];
            eventEntry.ChainId = chainIds[i];
            eventEntry.TransactionId = transactionIds[i];
            eventEntry.NftId = resolvedNftId;
            eventEntry.TargetAddressId = resolvedTargetAddressId;
        }

        await using var insertCmd = new NpgsqlCommand(@"
INSERT INTO ""Events"" (
    ""ID"",
    ""DM_UNIX_SECONDS"",
    ""TIMESTAMP_UNIX_SECONDS"",
    ""DATE_UNIX_SECONDS"",
    ""INDEX"",
    ""TOKEN_ID"",
    ""BURNED"",
    ""NSFW"",
    ""BLACKLISTED"",
    ""PAYLOAD_FORMAT"",
    ""PAYLOAD_JSON"",
    ""RAW_DATA"",
    ""AddressId"",
    ""ChainId"",
    ""ContractId"",
    ""TransactionId"",
    ""EventKindId"",
    ""NftId"",
    ""TargetAddressId""
)
SELECT
    row.""ID"",
    row.""DM_UNIX_SECONDS"",
    row.""TIMESTAMP_UNIX_SECONDS"",
    row.""DATE_UNIX_SECONDS"",
    row.""INDEX"",
    row.""TOKEN_ID"",
    row.""BURNED"",
    row.""NSFW"",
    row.""BLACKLISTED"",
    row.""PAYLOAD_FORMAT"",
    row.""PAYLOAD_JSON""::jsonb,
    row.""RAW_DATA"",
    row.""AddressId"",
    row.""ChainId"",
    row.""ContractId"",
    row.""TransactionId"",
    row.""EventKindId"",
    row.""NftId"",
    row.""TargetAddressId""
FROM UNNEST(
    @ids,
    @dm_unix_seconds,
    @timestamp_unix_seconds,
    @date_unix_seconds,
    @indexes,
    @token_ids,
    @burned,
    @nsfw,
    @blacklisted,
    @payload_formats,
    @payload_json,
    @raw_data,
    @address_ids,
    @chain_ids,
    @contract_ids,
    @transaction_ids,
    @event_kind_ids,
    @nft_ids,
    @target_address_ids
) AS row(
    ""ID"",
    ""DM_UNIX_SECONDS"",
    ""TIMESTAMP_UNIX_SECONDS"",
    ""DATE_UNIX_SECONDS"",
    ""INDEX"",
    ""TOKEN_ID"",
    ""BURNED"",
    ""NSFW"",
    ""BLACKLISTED"",
    ""PAYLOAD_FORMAT"",
    ""PAYLOAD_JSON"",
    ""RAW_DATA"",
    ""AddressId"",
    ""ChainId"",
    ""ContractId"",
    ""TransactionId"",
    ""EventKindId"",
    ""NftId"",
    ""TargetAddressId""
);
", dbConnection, dbTransaction);

        insertCmd.Parameters.Add("@ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = ids;
        insertCmd.Parameters.Add("@dm_unix_seconds", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value = dmUnixSeconds;
        insertCmd.Parameters.Add("@timestamp_unix_seconds", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value =
            timestampUnixSeconds;
        insertCmd.Parameters.Add("@date_unix_seconds", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value = dateUnixSeconds;
        insertCmd.Parameters.Add("@indexes", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = indexes;
        insertCmd.Parameters.Add("@token_ids", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = tokenIds;
        insertCmd.Parameters.Add("@burned", NpgsqlDbType.Array | NpgsqlDbType.Boolean).Value = burned;
        insertCmd.Parameters.Add("@nsfw", NpgsqlDbType.Array | NpgsqlDbType.Boolean).Value = nsfw;
        insertCmd.Parameters.Add("@blacklisted", NpgsqlDbType.Array | NpgsqlDbType.Boolean).Value = blacklisted;
        insertCmd.Parameters.Add("@payload_formats", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = payloadFormats;
        insertCmd.Parameters.Add("@payload_json", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = payloadJson;
        insertCmd.Parameters.Add("@raw_data", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = rawData;
        insertCmd.Parameters.Add("@address_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = addressIds;
        insertCmd.Parameters.Add("@chain_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = chainIds;
        insertCmd.Parameters.Add("@contract_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = contractIds;
        insertCmd.Parameters.Add("@transaction_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = transactionIds;
        insertCmd.Parameters.Add("@event_kind_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = eventKindIds;
        insertCmd.Parameters.Add("@nft_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = nftIds;
        insertCmd.Parameters.Add("@target_address_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value =
            targetAddressIds;

        await insertCmd.ExecuteNonQueryAsync();
    }

    private static async Task UpdateCreateEventLinksAsync(NpgsqlConnection dbConnection, NpgsqlTransaction dbTransaction,
        IReadOnlyDictionary<int, int> linksByEntityId, string sql)
    {
        if (linksByEntityId == null || linksByEntityId.Count == 0)
            return;

        var entityIds = new int[linksByEntityId.Count];
        var eventIds = new int[linksByEntityId.Count];
        var index = 0;

        foreach (var (entityId, eventId) in linksByEntityId)
        {
            if (entityId <= 0 || eventId <= 0)
                continue;

            entityIds[index] = entityId;
            eventIds[index] = eventId;
            index++;
        }

        if (index == 0)
            return;

        if (index != linksByEntityId.Count)
        {
            Array.Resize(ref entityIds, index);
            Array.Resize(ref eventIds, index);
        }

        await using var cmd = new NpgsqlCommand(sql, dbConnection, dbTransaction);
        cmd.Parameters.Add("@entity_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = entityIds;
        cmd.Parameters.Add("@event_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = eventIds;
        await cmd.ExecuteNonQueryAsync();
    }

    // Create-event relations are sparse compared to the full events stream.
    // Still keep them set-based so block commit does not fall back to per-row EF UPDATE calls.
    public static async Task ApplyCreateEventLinksAsync(NpgsqlConnection dbConnection, NpgsqlTransaction dbTransaction,
        IReadOnlyDictionary<int, int> tokenCreateEventByTokenId,
        IReadOnlyDictionary<int, int> platformCreateEventByPlatformId,
        IReadOnlyDictionary<int, int> contractCreateEventByContractId,
        IReadOnlyDictionary<int, int> organizationCreateEventByOrganizationId)
    {
        await UpdateCreateEventLinksAsync(dbConnection, dbTransaction, tokenCreateEventByTokenId, @"
UPDATE ""Tokens"" AS target
SET ""CreateEventId"" = src.""CreateEventId""
FROM UNNEST(@entity_ids, @event_ids) AS src(""ID"", ""CreateEventId"")
WHERE target.""ID"" = src.""ID"";");

        await UpdateCreateEventLinksAsync(dbConnection, dbTransaction, platformCreateEventByPlatformId, @"
UPDATE ""Platforms"" AS target
SET ""CreateEventId"" = src.""CreateEventId""
FROM UNNEST(@entity_ids, @event_ids) AS src(""ID"", ""CreateEventId"")
WHERE target.""ID"" = src.""ID"";");

        await UpdateCreateEventLinksAsync(dbConnection, dbTransaction, contractCreateEventByContractId, @"
UPDATE ""Contracts"" AS target
SET ""CreateEventId"" = src.""CreateEventId""
FROM UNNEST(@entity_ids, @event_ids) AS src(""ID"", ""CreateEventId"")
WHERE target.""ID"" = src.""ID"";");

        await UpdateCreateEventLinksAsync(dbConnection, dbTransaction, organizationCreateEventByOrganizationId, @"
UPDATE ""Organizations"" AS target
SET ""CreateEventId"" = src.""CreateEventId""
FROM UNNEST(@entity_ids, @event_ids) AS src(""ID"", ""CreateEventId"")
WHERE target.""ID"" = src.""ID"";");
    }
}
