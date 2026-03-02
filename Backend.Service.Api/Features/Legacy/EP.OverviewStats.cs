using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Backend.Commons;
using Database.Main;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Backend.Service.Api;

public static class GetOverviewStats
{
    [ProducesResponseType(typeof(OverviewStatsResult), (int)HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(OverviewStatsResult), "Returns aggregated overview counters.", false, 10, cacheTag: "overviewStats")]
    public static async Task<OverviewStatsResult> Execute(
        // ReSharper disable InconsistentNaming
        string chain = "main",
        int include_burned = 0,
        int include_legacy_transactions = 1
    // ReSharper enable InconsistentNaming
    )
    {
        try
        {
            if (!string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain))
                throw new ApiParameterException("Unsupported value for 'chain' parameter.");

            if (include_burned != 0 && include_burned != 1)
                throw new ApiParameterException("Unsupported value for 'include_burned' parameter.");

            if (include_legacy_transactions != 0 && include_legacy_transactions != 1)
                throw new ApiParameterException("Unsupported value for 'include_legacy_transactions' parameter.");

            var includeBurned = include_burned == 1;
            var includeLegacyTransactions = include_legacy_transactions == 1;

            var startTime = DateTime.Now;
            await using MainDbContext databaseContext = new();

            int? chainId = null;
            if (!string.IsNullOrEmpty(chain))
                chainId = await databaseContext.Chains.AsNoTracking()
                    .Where(x => x.NAME == chain)
                    .Select(x => (int?)x.ID)
                    .FirstOrDefaultAsync();

            int[] txChainIds = Array.Empty<int>();
            var useTransactionChainFilter = !string.IsNullOrEmpty(chain);
            if (useTransactionChainFilter)
            {
                if (includeLegacyTransactions && chain.Equals("main", StringComparison.OrdinalIgnoreCase))
                {
                    txChainIds = await databaseContext.Chains.AsNoTracking()
                        .Where(x => x.NAME == "main" || EF.Functions.Like(x.NAME, "main-generation-%"))
                        .Select(x => x.ID)
                        .ToArrayAsync();
                }
                else if (chainId.HasValue)
                {
                    txChainIds = new[] { chainId.Value };
                }
            }

            var transactionsQuery = databaseContext.Transactions.AsQueryable().AsNoTracking();
            if (useTransactionChainFilter)
                transactionsQuery = txChainIds.Length > 0
                    ? transactionsQuery.Where(x => txChainIds.Contains(x.Block.ChainId))
                    : transactionsQuery.Where(_ => false);

            var tokensQuery = databaseContext.Tokens.AsQueryable().AsNoTracking();
            var contractsQuery = databaseContext.Contracts.AsQueryable().AsNoTracking();
            var addressesQuery = databaseContext.Addresses.AsQueryable().AsNoTracking();
            var nftsQuery = databaseContext.Nfts.AsQueryable().AsNoTracking()
                .Where(x => x.NSFW == false && x.BLACKLISTED == false);

            if (!string.IsNullOrEmpty(chain))
            {
                if (chainId.HasValue)
                {
                    tokensQuery = tokensQuery.Where(x => x.ChainId == chainId.Value);
                    contractsQuery = contractsQuery.Where(x => x.ChainId == chainId.Value);
                    addressesQuery = addressesQuery.Where(x => x.ChainId == chainId.Value);
                    nftsQuery = nftsQuery.Where(x => x.ChainId == chainId.Value);
                }
                else
                {
                    tokensQuery = tokensQuery.Where(_ => false);
                    contractsQuery = contractsQuery.Where(_ => false);
                    addressesQuery = addressesQuery.Where(_ => false);
                    nftsQuery = nftsQuery.Where(_ => false);
                }
            }

            var transactionsTotal = await transactionsQuery.LongCountAsync();
            var tokensTotal = await tokensQuery.LongCountAsync();
            var contractsTotal = await contractsQuery.LongCountAsync();
            var addressesTotal = await addressesQuery.LongCountAsync();
            var soulMastersTotal = await addressesQuery
                .Where(x => x.OrganizationAddresses.Any(y => y.Organization.NAME == "masters"))
                .LongCountAsync();
            var nftOwnersQuery = databaseContext.NftOwnerships.AsQueryable().AsNoTracking()
                .Where(x => x.AMOUNT > 0)
                .Where(x => x.Nft.NSFW == false && x.Nft.BLACKLISTED == false);

            if (!string.IsNullOrEmpty(chain))
            {
                if (chainId.HasValue)
                    nftOwnersQuery = nftOwnersQuery.Where(x => x.Nft.ChainId == chainId.Value);
                else
                    nftOwnersQuery = nftOwnersQuery.Where(_ => false);
            }

            var nftOwnersTotal = await nftOwnersQuery
                .Select(x => x.AddressId)
                .Distinct()
                .LongCountAsync();

            var nftCounters = await nftsQuery
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Burned = g.LongCount(x => x.BURNED == true),
                    Unburned = g.LongCount(x => x.BURNED == null || x.BURNED == false)
                })
                .FirstOrDefaultAsync();

            var nftsBurnedTotal = nftCounters?.Burned ?? 0;
            var nftsUnburnedTotal = nftCounters?.Unburned ?? 0;
            var nftsTotal = includeBurned ? nftsBurnedTotal + nftsUnburnedTotal : nftsUnburnedTotal;

            var responseTime = DateTime.Now - startTime;
            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));

            return new OverviewStatsResult
            {
                chain = chain,
                include_burned = include_burned,
                include_legacy_transactions = include_legacy_transactions,
                transactions_total = transactionsTotal,
                tokens_total = tokensTotal,
                nfts_total = nftsTotal,
                nfts_unburned_total = nftsUnburnedTotal,
                nfts_burned_total = nftsBurnedTotal,
                contracts_total = contractsTotal,
                addresses_total = addressesTotal,
                nft_owners_total = nftOwnersTotal,
                soul_masters_total = soulMastersTotal
            };
        }
        catch (ApiParameterException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var logMessage = LogEx.Exception("OverviewStats()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }
    }
}
