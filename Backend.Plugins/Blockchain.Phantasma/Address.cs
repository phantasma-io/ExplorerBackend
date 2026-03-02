using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Backend.Api;
using Backend.Commons;
using Backend.PluginEngine;
using Database.Main;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using PhantasmaPhoenix.Core.Extensions;
using Serilog;

namespace Backend.Blockchain;

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    private static Task MarkAddressesDirty(MainDbContext databaseContext, Chain chain, List<string> addresses,
        long blockHeight)
    {
        return MarkAddressesDirty(databaseContext, chain, addresses, blockHeight, null);
    }

    private static async Task MarkAddressesDirty(MainDbContext databaseContext, Chain chain, List<string> addresses,
        long blockHeight, IReadOnlyDictionary<string, Address> cachedAddressMap)
    {
        if (addresses == null || addresses.Count == 0)
            return;

        var distinct = addresses.Distinct(StringComparer.Ordinal).ToList();
        var touchedAddresses = new Dictionary<string, Address>(StringComparer.Ordinal);
        var missingAddresses = new List<string>();

        foreach (var addressValue in distinct)
        {
            if (cachedAddressMap != null && cachedAddressMap.TryGetValue(addressValue, out var cachedAddress))
            {
                touchedAddresses[addressValue] = cachedAddress;
            }
            else
            {
                missingAddresses.Add(addressValue);
            }
        }

        // Fallback only for addresses not seen in the block-scoped cache.
        // This keeps behavior complete while removing a redundant full second pass.
        if (missingAddresses.Count > 0)
        {
            var inserted = AddressMethods.InsertIfNotExists(databaseContext, chain, missingAddresses);
            if (inserted != null)
            {
                foreach (var (addressValue, addressEntry) in inserted)
                    touchedAddresses[addressValue] = addressEntry;
            }
        }

        var persistedAddressIds = new HashSet<int>();
        foreach (var entry in touchedAddresses.Values)
        {
            if (entry == null)
                continue;

            if (entry.ID > 0)
            {
                persistedAddressIds.Add(entry.ID);
                continue;
            }

            // Keep legacy behavior for newly added addresses that do not have DB ids yet:
            // mark dirty on the tracked entity so the upcoming SaveChanges persists it.
            if (entry.BALANCE_DIRTY_BLOCK < blockHeight)
                entry.BALANCE_DIRTY_BLOCK = blockHeight;
        }

        if (persistedAddressIds.Count == 0)
            return;

        var addressIds = persistedAddressIds.ToArray();
        var dirtyBlocks = Enumerable.Repeat(blockHeight, addressIds.Length).ToArray();

        var dbConnection = (NpgsqlConnection)databaseContext.Database.GetDbConnection();
        if (dbConnection.State != System.Data.ConnectionState.Open)
            await dbConnection.OpenAsync();

        var dbTransaction = databaseContext.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction;
        await using var cmd = new NpgsqlCommand(@"
UPDATE ""Addresses"" AS address_row
SET ""BALANCE_DIRTY_BLOCK"" = src.""DirtyBlock""
FROM UNNEST(@address_ids, @dirty_blocks) AS src(""AddressId"", ""DirtyBlock"")
WHERE address_row.""ID"" = src.""AddressId""
  AND address_row.""BALANCE_DIRTY_BLOCK"" < src.""DirtyBlock"";
", dbConnection, dbTransaction);

        cmd.Parameters.Add("@address_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = addressIds;
        cmd.Parameters.Add("@dirty_blocks", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value = dirtyBlocks;
        await cmd.ExecuteNonQueryAsync();
    }

    private static Task MarkAllBalancesDirtyAsync(MainDbContext databaseContext, int chainId)
    {
        return databaseContext.Database.ExecuteSqlRawAsync(@"
UPDATE ""Addresses"" a
SET ""BALANCE_DIRTY_BLOCK"" = c.""CURRENT_HEIGHT""
FROM ""Chains"" c
WHERE a.""ChainId"" = c.""ID"" AND a.""ADDRESS"" <> 'NULL' AND c.""ID"" = {0};
", chainId);
    }

    private async Task UpdateAddressesBalancesAsync(MainDbContext databaseContext, Chain chain, List<string> addresses, int updateChunkSize)
    {
        var startTime = DateTime.Now;

        var processed = 0;

        var chainId = chain.ID;
        var normalizedAddresses = addresses
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var addressesToUpdate = await databaseContext.Addresses
            .Where(x => x.ChainId == chainId && normalizedAddresses.Contains(x.ADDRESS))
            .ToListAsync();
        var addressesToUpdateByValue = addressesToUpdate
            .ToDictionary(x => x.ADDRESS, x => x, StringComparer.Ordinal);
        Log.Verbose("[{Name}] got {Count} Addresses to check", Name, addressesToUpdate.Count);

        var soulDecimals = TokenMethods.GetSoulDecimals(databaseContext, chain);
        var kcalDecimals = TokenMethods.GetKcalDecimals(databaseContext, chain);

        var splitAddresses = addressesToUpdate.Chunk(updateChunkSize).ToList();
        for (var i = 0; i < splitAddresses.Count; i++)
        {
            var split = splitAddresses.ElementAt(i).Select(x => x.ADDRESS).ToList();
            var addressesComaSeparated = string.Join(",", split);
            var url = $"{Settings.Default.GetRest()}/api/v1/getAccounts?accountText={addressesComaSeparated}&extended=false&checkAddressReservedByte=false";

            try
            {
                var response = Client.ApiRequest<JsonDocument>(url, out var stringResponse, null, 1000);

                if (response == null)
                {
                    Log.Error("[{Name}] Balance sync: null result", Name);
                    if (updateChunkSize > 1)
                    {
                        // Temp solution for situation when batch of addresses crash API
                        await UpdateAddressesBalancesAsync(databaseContext, chain, split, 1);
                    }
                    continue;
                }

                if (response.RootElement.ValueKind == JsonValueKind.Object && response.RootElement.TryGetProperty("Error", out var errorProperty))
                {
                    if (updateChunkSize == 1)
                    {
                        Log.Error("[{Name}] Balance sync [{Address}]: Error: {errorProperty} [{apiCallUrl}]", Name, split.First(), errorProperty, url);
                    }
                    else
                    {
                        Log.Error("[{Name}] Balance sync: Error: {errorProperty}", Name, errorProperty);
                    }
                    if (updateChunkSize > 1)
                    {
                        // Temp solution for situation when batch of addresses crash API
                        await UpdateAddressesBalancesAsync(databaseContext, chain, split, 1);
                    }
                    continue;
                }

                var accounts = response.RootElement.EnumerateArray().ToList();
                var chunkBalancesByAddressId = new Dictionary<int, IReadOnlyList<(string Symbol, string AmountRaw)>>();
                foreach (var account in accounts)
                {
                    var accountAddress = account.GetProperty("address").GetString();
                    if (string.IsNullOrWhiteSpace(accountAddress))
                        continue;

                    if (!addressesToUpdateByValue.TryGetValue(accountAddress, out var address))
                        continue;

                    var name = account.GetProperty("name").GetString();
                    if (string.Equals(name, "anonymous", StringComparison.OrdinalIgnoreCase))
                    {
                        name = null;
                    }
                    address.ADDRESS_NAME = name;
                    address.NAME_LAST_UPDATED_UNIX_SECONDS = UnixSeconds.Now();

                    if (account.TryGetProperty("stakes", out var stakesProperty))
                    {
                        var amount = stakesProperty.GetProperty("amount").GetString();
                        var unclaimed = stakesProperty.GetProperty("unclaimed").GetString();

                        address.STAKE_TIMESTAMP = stakesProperty.GetProperty("time").GetInt32();
                        address.STAKED_AMOUNT = Commons.Utils.ToDecimal(amount, soulDecimals);
                        address.STAKED_AMOUNT_RAW = amount;
                        address.UNCLAIMED_AMOUNT = Commons.Utils.ToDecimal(unclaimed, kcalDecimals);
                        address.UNCLAIMED_AMOUNT_RAW = unclaimed;
                    }

                    BigInteger soulStakedBalance = BigInteger.TryParse(address.STAKED_AMOUNT_RAW, out var result) ? result : BigInteger.Zero;
                    BigInteger soulBalance = BigInteger.Zero;

                    if (account.TryGetProperty("balances", out var balancesProperty))
                    {
                        var parsedBalances = new List<(string Symbol, string AmountRaw)>();
                        foreach (var balance in balancesProperty.EnumerateArray())
                        {
                            var symbol = balance.GetProperty("symbol").GetString();
                            var amountRaw = balance.GetProperty("amount").GetString();
                            if (string.IsNullOrWhiteSpace(symbol) || string.IsNullOrWhiteSpace(amountRaw))
                                continue;

                            parsedBalances.Add((symbol, amountRaw));
                            if (symbol == "SOUL")
                                soulBalance = BigInteger.TryParse(amountRaw, out var parsedSoul) ? parsedSoul : BigInteger.Zero;
                        }

                        // Persist even explicit empty balance arrays.
                        // This lets AddressBalanceMethods interpret "account is present with no balances"
                        // as authoritative state and delete stale DB rows for that address.
                        chunkBalancesByAddressId[address.ID] = parsedBalances;
                    }

                    address.TOTAL_SOUL_AMOUNT = soulBalance + soulStakedBalance;

                    if (account.TryGetProperty("storage", out var storageProperty))
                    {
                        address.STORAGE_AVAILABLE = storageProperty.GetProperty("available").GetUInt32();
                        address.STORAGE_USED = storageProperty.GetProperty("used").GetUInt32();
                        address.AVATAR = storageProperty.GetProperty("avatar").GetString();
                    }

                    var validatorKind = AddressValidatorKindMethods.Upsert(databaseContext,
                        account.GetProperty("validator").GetString());
                    address.AddressValidatorKind = validatorKind;

                    // TODO some bs to fix later
                    //just to keep things up2date
                    address.Organization = null;
                    //var organization = OrganizationMethods.Get(databaseContext, address.ADDRESS_NAME);
                    //if ( organization != null ) address.Organization = organization;
                    //var organizations = OrganizationAddressMethods.GetOrganizationsByAddress(databaseContext, address.ADDRESS);
                    //if ( organizations.Any() ) address.Organizations = organizations.ToList();

                    processed++;
                }

                if (chunkBalancesByAddressId.Count > 0)
                    await AddressBalanceMethods.InsertOrUpdateBatchAsync(databaseContext, chainId,
                        chunkBalancesByAddressId);
            }
            catch
            {
                Log.Error("[{Name}] Crashed while querying url: {url}", Name, url);
                throw;
            }
        }

        var updateTime = DateTime.Now - startTime;
        Log.Information("[{Name}] Address sync took {Time} sec, Processed {Processed}", Name,
            Math.Round(updateTime.TotalSeconds, 3), processed);
    }

    private async Task<Address> SyncAddressByNameAsync(MainDbContext databaseContext, Chain chain, string addressName, Organization organization)
    {
        var startTime = DateTime.Now;

        if (string.IsNullOrEmpty(addressName)) return null;

        var addressEntry = AddressMethods.GetByName(databaseContext, chain, addressName);

        if (addressEntry == null)
        {
            var url = $"{Settings.Default.GetRest()}/api/v1/lookUpName?name={addressName}";
            var response = Client.ApiRequest<JsonDocument>(url, out _, null, 10);
            if (response == null)
            {
                Log.Error("[{Name}] lookUpName: null result", Name);
                return null;
            }

            var address = response.RootElement.GetString();
            Log.Verbose("[{Name}] Found Address {Address} for name {AddressName}", Name, address, addressName);

            if (string.IsNullOrEmpty(address)) return null;

            addressEntry = AddressMethods.Get(databaseContext, chain, address);

            if (addressEntry != null)
                addressEntry.ADDRESS_NAME = addressName;
            else
            {
                url = $"{Settings.Default.GetRest()}/api/v1/getAccount?account={address}&extended=false&checkAddressReservedByte=false";
                response = Client.ApiRequest<JsonDocument>(url, out _, null, 10);
                if (response == null)
                {
                    Log.Error("[{Name}] getAccount: null result", Name);
                    return null;
                }

                //do not process everything here, let the sync to that later, we just call it to make sure
                string name = "anonymous";
                if (response.RootElement.TryGetProperty("name", out JsonElement jsonName))
                {
                    name = jsonName.GetString();
                }
                addressEntry = await AddressMethods.UpsertAsync(databaseContext, chain, address);
                if (name == "anonymous") name = null;

                addressEntry.ADDRESS_NAME = name;
            }
        }

        if (organization != null)
        {
            Log.Verbose("[{Name}] setting Organization {Organization} for Address {Address}", Name,
                organization.ORGANIZATION_ID, addressEntry.ADDRESS);
            addressEntry.Organization = organization;
            organization.ADDRESS_NAME = addressName;
            organization.ADDRESS = addressEntry.ADDRESS;
        }

        var lookUpTime = DateTime.Now - startTime;
        Log.Information("[{Name}] Address lookup for {AddressName} took {Time} sec", Name,
            addressName, Math.Round(lookUpTime.TotalSeconds, 3));

        return addressEntry;
    }
}
