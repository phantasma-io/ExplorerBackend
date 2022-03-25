using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GhostDevs.Commons;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// Here we have all tables, fields and their relations for backend database.
// Also public method GetConnectionString() available, allowing to get database connection string,
// which can be used by PostgreSQLConnector module to connect to database and execute raw SQL queries.

//since we build our db struct here, we have a different naming as well
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Database.Main;

public class MainDbContext : DbContext
{
    // Keeping DB configs on same level as "bin" folder.
    // If path contains "Database.Main" - it means we are running database update.
    private static readonly string ConfigDirectory = AppDomain.CurrentDomain.BaseDirectory.Contains("Database.Main")
        ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../..")
        : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..");

    private static string ConfigFile => Path.Combine(ConfigDirectory, "explorer-backend-config.json");

    public DbSet<Chain> Chains { get; set; }
    public DbSet<Contract> Contracts { get; set; }
    public DbSet<Block> Blocks { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<EventKind> EventKinds { get; set; }
    public DbSet<Address> Addresses { get; set; }
    public DbSet<Event> Events { get; set; }
    public DbSet<Token> Tokens { get; set; }
    public DbSet<TokenDailyPrice> TokenDailyPrices { get; set; }
    public DbSet<NftOwnership> NftOwnerships { get; set; }
    public DbSet<Nft> Nfts { get; set; }
    public DbSet<SeriesMode> SeriesModes { get; set; }
    public DbSet<Series> Serieses { get; set; }
    public DbSet<Infusion> Infusions { get; set; }
    public DbSet<FiatExchangeRate> FiatExchangeRates { get; set; }
    public DbSet<Platform> Platforms { get; set; }
    public DbSet<PlatformToken> PlatformTokens { get; set; }
    public DbSet<PlatformInterop> PlatformInterops { get; set; }
    public DbSet<External> Externals { get; set; }
    public DbSet<Organization> Organizations { get; set; }
    public DbSet<OrganizationEvent> OrganizationEvents { get; set; }
    public DbSet<StringEvent> StringEvents { get; set; }
    public DbSet<AddressEvent> AddressEvents { get; set; }
    public DbSet<TransactionSettleEvent> TransactionSettleEvents { get; set; }
    public DbSet<HashEvent> HashEvents { get; set; }
    public DbSet<GasEvent> GasEvents { get; set; }
    public DbSet<SaleEvent> SaleEvents { get; set; }
    public DbSet<SaleEventKind> SaleEventKinds { get; set; }
    public DbSet<ChainEvent> ChainEvents { get; set; }
    public DbSet<TokenEvent> TokenEvents { get; set; }
    public DbSet<InfusionEvent> InfusionEvents { get; set; }
    public DbSet<MarketEventKind> MarketEventKinds { get; set; }
    public DbSet<MarketEvent> MarketEvents { get; set; }
    public DbSet<BlockOracle> BlockOracles { get; set; }
    public DbSet<Oracle> Oracles { get; set; }
    public DbSet<SignatureKind> SignatureKinds { get; set; }
    public DbSet<Signature> Signatures { get; set; }
    public DbSet<OrganizationAddress> OrganizationAddresses { get; set; }
    public DbSet<MarketEventFiatPrice> MarketEventFiatPrices { get; set; }
    public DbSet<TokenPriceState> TokenPriceStates { get; set; }
    public DbSet<AddressTransaction> AddressTransactions { get; set; }
    public DbSet<AddressStake> AddressStakes { get; set; }
    public DbSet<AddressStorage> AddressStorages { get; set; }
    public DbSet<AddressBalance> AddressBalances { get; set; }
    public DbSet<AddressValidatorKind> AddressValidatorKinds { get; set; }


    public static string GetConnectionString()
    {
        Settings.Load(new ConfigurationBuilder().AddJsonFile(ConfigFile, false).Build()
            .GetSection("DatabaseConfiguration"));
        return Settings.Default.ConnectionString;
    }


    //for now...
    public static int GetConnectionMaxRetries()
    {
        Settings.Load(new ConfigurationBuilder().AddJsonFile(ConfigFile, false).Build()
            .GetSection("DatabaseConfiguration"));
        return Settings.Default.ConnectMaxRetries;
    }


    //for now...
    public static int GetConnectionRetryTimeout()
    {
        Settings.Load(new ConfigurationBuilder().AddJsonFile(ConfigFile, false).Build()
            .GetSection("DatabaseConfiguration"));
        return Settings.Default.ConnectRetryTimeout;
    }


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(GetConnectionString());
        optionsBuilder.EnableSensitiveDataLogging();
        optionsBuilder.UseLazyLoadingProxies();

        optionsBuilder.UseNpgsql(GetConnectionString(),
            optionsAction =>
            {
                // optionsAction.EnableRetryOnFailure();
                optionsAction.CommandTimeout(( int ) TimeSpan.FromMinutes(10).TotalSeconds);
            });
    }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Here we add relations between tables and indexes.


        //////////////////////
        // Chain
        //////////////////////

        // FKs

        // Indexes

        modelBuilder.Entity<Chain>()
            .HasIndex(x => x.NAME)
            .IsUnique();

        //////////////////////
        // Contract
        //////////////////////

        // FKs
        modelBuilder.Entity<Contract>()
            .HasOne(x => x.Chain)
            .WithMany(y => y.Contracts)
            .HasForeignKey(x => x.ChainId);

        // Indexes


        //////////////////////
        // Block
        //////////////////////

        // FKs

        modelBuilder.Entity<Block>()
            .HasOne(x => x.Chain)
            .WithMany(y => y.Blocks)
            .HasForeignKey(x => x.ChainId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Block>()
            .HasOne(x => x.ChainAddress)
            .WithMany(y => y.ChainAddressBlocks)
            .HasForeignKey(x => x.ChainAddressId);

        modelBuilder.Entity<Block>()
            .HasOne(x => x.ValidatorAddress)
            .WithMany(y => y.ValidatorAddressBlocks)
            .HasForeignKey(x => x.ValidatorAddressId);

        // Indexes

        modelBuilder.Entity<Block>()
            .HasIndex(x => new {x.TIMESTAMP_UNIX_SECONDS});

        modelBuilder.Entity<Block>()
            .HasIndex(x => new {x.ChainId, x.HEIGHT});

        modelBuilder.Entity<Block>()
            .HasIndex(x => x.HASH)
            .IsUnique();

        //////////////////////
        // Transaction
        //////////////////////

        // FKs

        modelBuilder.Entity<Transaction>()
            .HasOne(x => x.Block)
            .WithMany(y => y.Transactions)
            .HasForeignKey(x => x.BlockId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes

        // We should not make it unique to allow mixing mainnet and testnet in testnet DB.
        modelBuilder.Entity<Transaction>()
            .HasIndex(x => new {x.BlockId, x.INDEX});

        modelBuilder.Entity<Transaction>()
            .HasIndex(x => new {x.HASH});

        modelBuilder.Entity<Transaction>()
            .HasIndex(x => new {x.TIMESTAMP_UNIX_SECONDS});

        //////////////////////
        // EventKind
        //////////////////////

        // FKs

        modelBuilder.Entity<EventKind>()
            .HasOne(x => x.Chain)
            .WithMany(y => y.EventKinds)
            .HasForeignKey(x => x.ChainId);

        // Indexes

        modelBuilder.Entity<EventKind>()
            .HasIndex(x => new {x.ChainId, x.NAME})
            .IsUnique();

        //////////////////////
        // Address
        //////////////////////

        // FKs

        modelBuilder.Entity<Address>()
            .HasOne(x => x.Chain)
            .WithMany(y => y.Addresses)
            .HasForeignKey(x => x.ChainId);

        modelBuilder.Entity<Address>()
            .HasOne(x => x.AddressStake)
            .WithOne(y => y.Address)
            .HasForeignKey<AddressStake>(x => x.AddressId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Address>()
            .HasOne(x => x.AddressStorage)
            .WithOne(y => y.Address)
            .HasForeignKey<AddressStorage>(x => x.AddressId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Address>()
            .HasOne(x => x.AddressValidatorKind)
            .WithMany(y => y.Addresses)
            .HasForeignKey(x => x.AddressValidatorKindId);

        // Indexes

        modelBuilder.Entity<Address>()
            .HasIndex(x => new {x.ChainId, x.ADDRESS})
            .IsUnique();

        modelBuilder.Entity<Address>()
            .HasIndex(x => new {x.ChainId, x.NAME_LAST_UPDATED_UNIX_SECONDS});

        modelBuilder.Entity<Address>()
            .HasIndex(x => new {x.ADDRESS, x.ADDRESS_NAME});

        //////////////////////
        // Event
        //////////////////////

        // FKs

        modelBuilder.Entity<Event>()
            .HasOne(x => x.Chain)
            .WithMany(y => y.Events)
            .HasForeignKey(x => x.ChainId);

        modelBuilder.Entity<Event>()
            .HasOne(x => x.Transaction)
            .WithMany(y => y.Events)
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Event>()
            .HasOne(x => x.EventKind)
            .WithMany(y => y.Events)
            .HasForeignKey(x => x.EventKindId);

        modelBuilder.Entity<Event>()
            .HasOne(x => x.Contract)
            .WithMany(y => y.Events)
            .HasForeignKey(x => x.ContractId);

        modelBuilder.Entity<Event>()
            .HasOne(x => x.Address)
            .WithMany(y => y.Events)
            .HasForeignKey(x => x.AddressId);

        modelBuilder.Entity<Event>()
            .HasOne(x => x.OrganizationEvent)
            .WithOne(y => y.Event)
            .HasForeignKey<OrganizationEvent>(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Event>()
            .HasOne(x => x.StringEvent)
            .WithOne(y => y.Event)
            .HasForeignKey<StringEvent>(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Event>()
            .HasOne(x => x.AddressEvent)
            .WithOne(y => y.Event)
            .HasForeignKey<AddressEvent>(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Event>()
            .HasOne(x => x.TransactionSettleEvent)
            .WithOne(y => y.Event)
            .HasForeignKey<TransactionSettleEvent>(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Event>()
            .HasOne(x => x.HashEvent)
            .WithOne(y => y.Event)
            .HasForeignKey<HashEvent>(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Event>()
            .HasOne(x => x.GasEvent)
            .WithOne(y => y.Event)
            .HasForeignKey<GasEvent>(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Event>()
            .HasOne(x => x.SaleEvent)
            .WithOne(y => y.Event)
            .HasForeignKey<SaleEvent>(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Event>()
            .HasOne(x => x.ChainEvent)
            .WithOne(y => y.Event)
            .HasForeignKey<ChainEvent>(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Event>()
            .HasOne(x => x.TokenEvent)
            .WithOne(y => y.Event)
            .HasForeignKey<TokenEvent>(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Event>()
            .HasOne(x => x.InfusionEvent)
            .WithOne(y => y.Event)
            .HasForeignKey<InfusionEvent>(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Event>()
            .HasOne(x => x.MarketEvent)
            .WithOne(y => y.Event)
            .HasForeignKey<MarketEvent>(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Event>()
            .HasOne(x => x.Nft)
            .WithMany(y => y.Events)
            .HasForeignKey(x => x.NftId);

        // Indexes

        modelBuilder.Entity<Event>()
            .HasIndex(x => x.TIMESTAMP_UNIX_SECONDS);
        modelBuilder.Entity<Event>()
            .HasIndex(x => x.DATE_UNIX_SECONDS);

        modelBuilder.Entity<Event>()
            .HasIndex(x => new {x.ContractId, x.TOKEN_ID});

        modelBuilder.Entity<Event>()
            .HasIndex(x => new {x.BURNED, x.EventKindId});

        //////////////////////
        // Token
        //////////////////////

        // FKs

        modelBuilder.Entity<Token>()
            .HasOne(x => x.Chain)
            .WithMany(y => y.Tokens)
            .HasForeignKey(x => x.ChainId);

        modelBuilder.Entity<Token>()
            .HasOne(x => x.Contract)
            .WithOne(y => y.Token)
            .HasForeignKey<Token>(x => x.ContractId);

        modelBuilder.Entity<Token>()
            .HasOne(x => x.Address)
            .WithMany(y => y.Tokens)
            .HasForeignKey(x => x.AddressId);

        modelBuilder.Entity<Token>()
            .HasOne(x => x.Owner)
            .WithMany(y => y.TokenOwners)
            .HasForeignKey(x => x.OwnerId);

        modelBuilder.Entity<Token>()
            .HasOne(x => x.TokenPriceState)
            .WithOne(y => y.Token)
            .HasForeignKey<TokenPriceState>(x => x.TokenId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes

        modelBuilder.Entity<Token>()
            .HasIndex(x => new {x.ChainId, x.ContractId, x.SYMBOL})
            .IsUnique();

        modelBuilder.Entity<Token>()
            .HasIndex(x => new {x.SYMBOL, x.ChainId});


        //////////////////////
        // TokenDailyPrice
        //////////////////////

        // FKs

        modelBuilder.Entity<TokenDailyPrice>()
            .HasOne(x => x.Token)
            .WithMany(y => y.TokenDailyPrices)
            .HasForeignKey(x => x.TokenId);

        // Indexes

        modelBuilder.Entity<TokenDailyPrice>()
            .HasIndex(x => new {x.DATE_UNIX_SECONDS});

        //////////////////////
        // NftOwnership
        //////////////////////

        // FKs

        modelBuilder.Entity<NftOwnership>()
            .HasOne(x => x.Address)
            .WithMany(y => y.NftOwnerships)
            .HasForeignKey(x => x.AddressId);

        modelBuilder.Entity<NftOwnership>()
            .HasOne(x => x.Nft)
            .WithMany(y => y.NftOwnerships)
            .HasForeignKey(x => x.NftId);

        // Indexes

        modelBuilder.Entity<NftOwnership>()
            .HasIndex(x => new {x.AddressId, x.NftId})
            .IsUnique();

        modelBuilder.Entity<NftOwnership>()
            .HasIndex(x => x.LAST_CHANGE_UNIX_SECONDS);

        //////////////////////
        // Nft
        //////////////////////

        // FKs

        modelBuilder.Entity<Nft>()
            .HasMany(x => x.InfusedNfts)
            .WithOne(y => y.InfusedInto)
            .HasForeignKey(x => x.InfusedIntoId);

        modelBuilder.Entity<Nft>()
            .HasOne(x => x.Series)
            .WithMany(y => y.Nfts)
            .HasForeignKey(x => x.SeriesId);

        modelBuilder.Entity<Nft>()
            .HasOne(x => x.CreatorAddress)
            .WithMany(y => y.Nfts)
            .HasForeignKey(x => x.CreatorAddressId);

        modelBuilder.Entity<Nft>()
            .HasOne(x => x.Chain)
            .WithMany(y => y.Nfts)
            .HasForeignKey(x => x.ChainId);

        modelBuilder.Entity<Nft>()
            .HasOne(x => x.Contract)
            .WithMany(y => y.Nfts)
            .HasForeignKey(x => x.ContractId);

        // Indexes
        modelBuilder.Entity<Nft>()
            .HasIndex(x => x.DM_UNIX_SECONDS);

        modelBuilder.Entity<Nft>()
            .HasIndex(x => new {x.ContractId, x.TOKEN_ID})
            .IsUnique();

        // Can't make it unique, AirNFT (BSC) has some duplicates.
        modelBuilder.Entity<Nft>()
            .HasIndex(x => new {x.ContractId, x.TOKEN_URI});

        modelBuilder.Entity<Nft>()
            .HasIndex(x => x.BURNED);

        modelBuilder.Entity<Nft>()
            .HasIndex(x => x.TOKEN_ID);

        //////////////////////
        // SeriesMode
        //////////////////////

        // FKs

        // Indexes

        modelBuilder.Entity<SeriesMode>()
            .HasIndex(x => x.MODE_NAME);

        //////////////////////
        // Series
        //////////////////////

        // FKs

        modelBuilder.Entity<Series>()
            .HasOne(x => x.Contract)
            .WithMany(y => y.Series)
            .HasForeignKey(x => x.ContractId);

        modelBuilder.Entity<Series>()
            .HasOne(x => x.SeriesMode)
            .WithMany(y => y.Series)
            .HasForeignKey(x => x.SeriesModeId);

        modelBuilder.Entity<Series>()
            .HasOne(x => x.CreatorAddress)
            .WithMany(y => y.Serieses)
            .HasForeignKey(x => x.CreatorAddressId);

        // Indexes

        modelBuilder.Entity<Series>()
            .HasIndex(x => new {x.SERIES_ID});

        modelBuilder.Entity<Series>()
            .HasIndex(x => new {x.ContractId, x.SERIES_ID})
            .IsUnique();


        //////////////////////
        // Infusion
        //////////////////////

        // FKs

        modelBuilder.Entity<Infusion>()
            .HasOne(x => x.Token)
            .WithMany(y => y.Infusions)
            .HasForeignKey(x => x.TokenId);

        modelBuilder.Entity<Infusion>()
            .HasOne(x => x.Nft)
            .WithMany(y => y.Infusions)
            .HasForeignKey(x => x.NftId);

        // Indexes

        modelBuilder.Entity<Infusion>()
            .HasIndex(x => x.KEY);

        //////////////////////
        // FiatExchangeRate
        //////////////////////

        // Indexes

        modelBuilder.Entity<FiatExchangeRate>()
            .HasIndex(x => x.SYMBOL)
            .IsUnique();

        modelBuilder.Entity<FiatExchangeRate>()
            .HasIndex(x => new {x.SYMBOL, x.USD_PRICE});

        //////////////////////
        // Platform
        //////////////////////

        // Indexes
        modelBuilder.Entity<Platform>()
            .HasIndex(x => x.NAME)
            .IsUnique();


        //////////////////////
        // PlatformToken
        //////////////////////

        // FKs
        modelBuilder.Entity<PlatformToken>()
            .HasOne(x => x.Platform)
            .WithMany(y => y.PlatformTokens)
            .HasForeignKey(x => x.PlatformId);

        // Indexes
        modelBuilder.Entity<PlatformToken>()
            .HasIndex(x => x.NAME);

        //////////////////////
        // PlatformInterop
        //////////////////////

        // FKs
        modelBuilder.Entity<PlatformInterop>()
            .HasOne(x => x.Platform)
            .WithMany(y => y.PlatformInterops)
            .HasForeignKey(x => x.PlatformId);

        modelBuilder.Entity<PlatformInterop>()
            .HasOne(x => x.LocalAddress)
            .WithMany(y => y.PlatformInterops)
            .HasForeignKey(x => x.LocalAddressId);

        // Indexes


        //////////////////////
        // External
        //////////////////////

        // FKs
        modelBuilder.Entity<External>()
            .HasOne(x => x.Platform)
            .WithMany(y => y.Externals)
            .HasForeignKey(x => x.PlatformId);

        modelBuilder.Entity<External>()
            .HasOne(x => x.Token)
            .WithMany(y => y.Externals)
            .HasForeignKey(x => x.TokenId);


        // Indexes


        //////////////////////
        // Organization
        //////////////////////

        // FKs

        // Indexes
        modelBuilder.Entity<Organization>()
            .HasIndex(x => x.NAME)
            .IsUnique();


        //////////////////////
        // OrganizationEvent
        //////////////////////

        // FKs
        modelBuilder.Entity<OrganizationEvent>()
            .HasOne(x => x.Organization)
            .WithMany(y => y.OrganizationEvents)
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OrganizationEvent>()
            .HasOne(x => x.Address)
            .WithMany(y => y.OrganizationEvents)
            .HasForeignKey(x => x.AddressId);

        // Indexes


        //////////////////////
        // StringEvent
        //////////////////////

        // FKs

        // Indexes

        //////////////////////
        // AddressEvent
        //////////////////////

        // FKs
        modelBuilder.Entity<AddressEvent>()
            .HasOne(x => x.Address)
            .WithMany(y => y.AddressEvents)
            .HasForeignKey(x => x.AddressId);

        // Indexes


        //////////////////////
        // TransactionSettleEvent
        //////////////////////

        // FKs
        modelBuilder.Entity<TransactionSettleEvent>()
            .HasOne(x => x.Platform)
            .WithMany(y => y.TransactionSettleEvents)
            .HasForeignKey(x => x.PlatformId);

        // Indexes


        //////////////////////
        // HashEvent
        //////////////////////

        // FKs

        // Indexes


        //////////////////////
        // GasEvent
        //////////////////////

        // FKs
        modelBuilder.Entity<GasEvent>()
            .HasOne(x => x.Address)
            .WithMany(y => y.GasEvents)
            .HasForeignKey(x => x.AddressId);

        // Indexes


        //////////////////////
        // SaleEventKind
        //////////////////////

        // FKs
        modelBuilder.Entity<SaleEventKind>()
            .HasOne(x => x.Chain)
            .WithMany(y => y.SaleEventKinds)
            .HasForeignKey(x => x.ChainId);

        // Indexes
        modelBuilder.Entity<SaleEventKind>()
            .HasIndex(x => new {x.ChainId, x.NAME})
            .IsUnique();

        modelBuilder.Entity<SaleEventKind>()
            .HasIndex(x => x.NAME);

        //////////////////////
        // SaleEvent
        //////////////////////

        // FKs
        modelBuilder.Entity<SaleEvent>()
            .HasOne(x => x.SaleEventKind)
            .WithMany(y => y.SaleEvents)
            .HasForeignKey(x => x.SaleEventKindId);

        // Indexes


        //////////////////////
        // ChainEvent
        //////////////////////

        // FKs
        modelBuilder.Entity<ChainEvent>()
            .HasOne(x => x.Chain)
            .WithMany(y => y.ChainEvents)
            .HasForeignKey(x => x.ChainId);

        // Indexes


        //////////////////////
        // TokenEvent
        //////////////////////

        // FKs
        modelBuilder.Entity<TokenEvent>()
            .HasOne(x => x.Token)
            .WithMany(y => y.TokenEvents)
            .HasForeignKey(x => x.TokenId);

        // Indexes


        //////////////////////
        // InfusionEvent
        //////////////////////

        // FKs
        modelBuilder.Entity<InfusionEvent>()
            .HasOne(x => x.BaseToken)
            .WithMany(y => y.BaseSymbolInfusionEvents)
            .HasForeignKey(x => x.BaseTokenId);

        modelBuilder.Entity<InfusionEvent>()
            .HasOne(x => x.InfusedToken)
            .WithMany(y => y.InfusedSymbolInfusionEvents)
            .HasForeignKey(x => x.InfusedTokenId);

        modelBuilder.Entity<InfusionEvent>()
            .HasOne(x => x.Infusion)
            .WithMany(y => y.InfusionEvents)
            .HasForeignKey(x => x.InfusionId);


        // Indexes
        modelBuilder.Entity<InfusionEvent>()
            .HasIndex(x => x.TOKEN_ID);


        //////////////////////
        // MarketEventKind
        //////////////////////

        // FKs
        modelBuilder.Entity<MarketEventKind>()
            .HasOne(x => x.Chain)
            .WithMany(y => y.MarketEventKinds)
            .HasForeignKey(x => x.ChainId);

        // Indexes
        modelBuilder.Entity<MarketEventKind>()
            .HasIndex(x => new {x.ChainId, x.NAME})
            .IsUnique();

        modelBuilder.Entity<MarketEventKind>()
            .HasIndex(x => x.NAME);


        //////////////////////
        // MarketEvent
        //////////////////////

        // FKs
        modelBuilder.Entity<MarketEvent>()
            .HasOne(x => x.MarketEventKind)
            .WithMany(y => y.MarketEvents)
            .HasForeignKey(x => x.MarketEventKindId);

        modelBuilder.Entity<MarketEvent>()
            .HasOne(x => x.BaseToken)
            .WithMany(y => y.BaseSymbolMarketEvents)
            .HasForeignKey(x => x.BaseTokenId);

        modelBuilder.Entity<MarketEvent>()
            .HasOne(x => x.QuoteToken)
            .WithMany(y => y.QuoteSymbolMarketEvents)
            .HasForeignKey(x => x.QuoteTokenId);

        // Indexes


        //////////////////////
        // BlockOracle
        //////////////////////

        // FKs
        modelBuilder.Entity<BlockOracle>()
            .HasOne(x => x.Block)
            .WithMany(y => y.BlockOracles)
            .HasForeignKey(x => x.BlockId);

        modelBuilder.Entity<BlockOracle>()
            .HasOne(x => x.Oracle)
            .WithMany(y => y.BlockOracles)
            .HasForeignKey(x => x.OracleId);

        //////////////////////
        // Oracle
        //////////////////////

        // FKs

        // Indexes
        modelBuilder.Entity<Oracle>()
            .HasIndex(x => new {x.URL, x.CONTENT})
            .IsUnique();

        //////////////////////
        // SignatureKind
        //////////////////////

        // FKs

        // Indexes
        modelBuilder.Entity<SignatureKind>()
            .HasIndex(x => x.NAME);

        //////////////////////
        // Signature
        //////////////////////

        // FKs
        modelBuilder.Entity<Signature>()
            .HasOne(x => x.SignatureKind)
            .WithMany(y => y.Signatures)
            .HasForeignKey(x => x.SignatureKindId);

        modelBuilder.Entity<Signature>()
            .HasOne(x => x.Transaction)
            .WithMany(y => y.Signatures)
            .HasForeignKey(x => x.TransactionId);

        // Indexes

        //////////////////////
        // OrganizationAddress
        //////////////////////

        // FKs
        modelBuilder.Entity<OrganizationAddress>()
            .HasOne(x => x.Address)
            .WithMany(y => y.OrganizationAddresses)
            .HasForeignKey(x => x.AddressId);

        modelBuilder.Entity<OrganizationAddress>()
            .HasOne(x => x.Organization)
            .WithMany(y => y.OrganizationAddresses)
            .HasForeignKey(x => x.OrganizationId);

        // Indexes


        //////////////////////
        // MarketEventFiatPrice
        //////////////////////

        // FKs
        modelBuilder.Entity<MarketEventFiatPrice>()
            .HasOne(x => x.MarketEvent)
            .WithOne(y => y.MarketEventFiatPrice)
            .HasForeignKey<MarketEventFiatPrice>(x => x.MarketEventId);

        // Indexes

        modelBuilder.Entity<MarketEventFiatPrice>()
            .HasIndex(x => new {x.PRICE_END_USD, x.PRICE_USD});

        //////////////////////
        // TokenPriceStates
        //////////////////////

        // FKs

        // Indexes
        modelBuilder.Entity<TokenPriceState>()
            .HasIndex(x => new {x.LAST_CHECK_DATE_UNIX_SECONDS});

        //////////////////////
        // AddressTransaction
        //////////////////////

        // FKs
        modelBuilder.Entity<AddressTransaction>()
            .HasOne(x => x.Address)
            .WithMany(y => y.AddressTransactions)
            .HasForeignKey(x => x.AddressId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AddressTransaction>()
            .HasOne(x => x.Transaction)
            .WithMany(y => y.AddressTransactions)
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes


        //////////////////////
        // AddressStake
        //////////////////////

        // FKs

        // Indexes

        //////////////////////
        // AddressStorage
        //////////////////////

        // FKs

        // Indexes

        //////////////////////
        // AddressBalance
        //////////////////////

        // FKs
        modelBuilder.Entity<AddressBalance>()
            .HasOne(x => x.Chain)
            .WithMany(y => y.AddressBalances)
            .HasForeignKey(x => x.ChainId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AddressBalance>()
            .HasOne(x => x.Address)
            .WithMany(y => y.AddressBalances)
            .HasForeignKey(x => x.AddressId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AddressBalance>()
            .HasOne(x => x.Token)
            .WithMany(y => y.AddressBalances)
            .HasForeignKey(x => x.TokenId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes


        //////////////////////
        // AddressValidatorKind
        //////////////////////

        // FKs

        // Indexes
        modelBuilder.Entity<AddressValidatorKind>()
            .HasIndex(x => x.NAME);
    }
}

public class Chain
{
    public int ID { get; set; }
    public string NAME { get; set; }
    public string CURRENT_HEIGHT { get; set; }
    public virtual List<Nft> Nfts { get; set; }
    public virtual List<Contract> Contracts { get; set; }
    public virtual List<Token> Tokens { get; set; }
    public virtual List<Block> Blocks { get; set; }
    public virtual List<Address> Addresses { get; set; }
    public virtual List<EventKind> EventKinds { get; set; }
    public virtual List<Event> Events { get; set; }
    public virtual List<SaleEventKind> SaleEventKinds { get; set; }

    public virtual List<ChainEvent> ChainEvents { get; set; }

    //public virtual List<TokenEvent> TokenEvents { get; set; } currently not in use
    public virtual List<MarketEventKind> MarketEventKinds { get; set; }
    public virtual List<AddressBalance> AddressBalances { get; set; }
}

public class Contract
{
    public int ID { get; set; }

    public string NAME { get; set; }

    // We store string representation of contract hash without "0x".
    public string HASH { get; set; }
    public string SYMBOL { get; set; }
    public int ChainId { get; set; }
    public virtual Chain Chain { get; set; }
    public int? TokenId { get; set; } //TODO maybe fill data
    public virtual Token Token { get; set; }
    public virtual List<Event> Events { get; set; }
    public virtual List<Nft> Nfts { get; set; }
    public virtual List<Series> Series { get; set; }
}

public class Block
{
    public int ID { get; set; }
    public string HEIGHT { get; set; }
    public long TIMESTAMP_UNIX_SECONDS { get; set; }
    public int ChainId { get; set; }
    public virtual Chain Chain { get; set; }
    public string HASH { get; set; }
    public string PREVIOUS_HASH { get; set; }
    public int PROTOCOL { get; set; }
    public int ChainAddressId { get; set; }
    public virtual Address ChainAddress { get; set; }
    public int ValidatorAddressId { get; set; }
    public virtual Address ValidatorAddress { get; set; }
    public string REWARD { get; set; }
    public virtual List<Transaction> Transactions { get; set; }
    public virtual List<BlockOracle> BlockOracles { get; set; }
}

public class Transaction
{
    public int ID { get; set; }
    public string HASH { get; set; }
    public int INDEX { get; set; } // Index of tx in block
    public int BlockId { get; set; }
    public virtual Block Block { get; set; }
    public long TIMESTAMP_UNIX_SECONDS { get; set; }
    public string PAYLOAD { get; set; }
    public string SCRIPT_RAW { get; set; }
    public string RESULT { get; set; }
    public string FEE { get; set; }
    public long EXPIRATION { get; set; }
    public virtual List<Event> Events { get; set; }
    public virtual List<Signature> Signatures { get; set; }
    public virtual List<AddressTransaction> AddressTransactions { get; set; }
}

public class EventKind
{
    public int ID { get; set; }
    public string NAME { get; set; }
    public int ChainId { get; set; }
    public virtual Chain Chain { get; set; }
    public virtual List<Event> Events { get; set; }
}

public class Address
{
    public int ID { get; set; }
    public string ADDRESS { get; set; }
    public string ADDRESS_NAME { get; set; }
    public string USER_NAME { get; set; }
    public long NAME_LAST_UPDATED_UNIX_SECONDS { get; set; }
    public string STAKE { get; set; }
    public string UNCLAIMED { get; set; }
    public string RELAY { get; set; }
    public int ChainId { get; set; }
    public virtual Chain Chain { get; set; }
    public virtual List<Event> Events { get; set; }
    public virtual List<Nft> Nfts { get; set; }
    public virtual List<NftOwnership> NftOwnerships { get; set; }
    public virtual List<Series> Serieses { get; set; }
    public virtual List<GasEvent> GasEvents { get; set; }
    public virtual List<AddressEvent> AddressEvents { get; set; }
    public virtual List<OrganizationEvent> OrganizationEvents { get; set; }
    public virtual List<PlatformInterop> PlatformInterops { get; set; }
    public virtual List<Block> ChainAddressBlocks { get; set; }
    public virtual List<Block> ValidatorAddressBlocks { get; set; }
    public virtual List<OrganizationAddress> OrganizationAddresses { get; set; }
    public virtual List<AddressTransaction> AddressTransactions { get; set; }
    public virtual List<AddressBalance> AddressBalances { get; set; }
    public virtual AddressStake AddressStake { get; set; }
    public virtual AddressStorage AddressStorage { get; set; }
    public int? AddressValidatorKindId { get; set; }
    public virtual AddressValidatorKind AddressValidatorKind { get; set; }
    public virtual List<Token> Tokens { get; set; }
    public virtual List<Token> TokenOwners { get; set; }
}

public class Event
{
    public int ID { get; set; }
    public long DM_UNIX_SECONDS { get; set; } // Last modification date (in database).
    public long TIMESTAMP_UNIX_SECONDS { get; set; }
    public long DATE_UNIX_SECONDS { get; set; } // Same as TIMESTAMP, but without time.

    public int INDEX { get; set; } // Index of event in tx.

    // EF do not preserve insertion order for ID,
    // but we need to know exact order of events, so we add special field for this.
    public string TOKEN_ID { get; set; }
    public bool? BURNED { get; set; }
    public bool NSFW { get; set; }
    public bool BLACKLISTED { get; set; }
    public int AddressId { get; set; }
    public virtual Address Address { get; set; }
    public int ChainId { get; set; }
    public virtual Chain Chain { get; set; }
    public int ContractId { get; set; }
    public virtual Contract Contract { get; set; }
    public int TransactionId { get; set; }
    public virtual Transaction Transaction { get; set; }
    public int EventKindId { get; set; }
    public virtual EventKind EventKind { get; set; }
    public int? NftId { get; set; }
    public virtual Nft Nft { get; set; }
    public virtual OrganizationEvent OrganizationEvent { get; set; }
    public virtual StringEvent StringEvent { get; set; }
    public virtual AddressEvent AddressEvent { get; set; }
    public virtual TransactionSettleEvent TransactionSettleEvent { get; set; }
    public virtual HashEvent HashEvent { get; set; }
    public virtual GasEvent GasEvent { get; set; }
    public virtual SaleEvent SaleEvent { get; set; }
    public virtual ChainEvent ChainEvent { get; set; }
    public virtual TokenEvent TokenEvent { get; set; }
    public virtual InfusionEvent InfusionEvent { get; set; }
    public virtual MarketEvent MarketEvent { get; set; }
}

public class Token
{
    public int ID { get; set; }
    public string SYMBOL { get; set; }
    public bool FUNGIBLE { get; set; }
    public bool TRANSFERABLE { get; set; }
    public bool FINITE { get; set; }
    public bool DIVISIBLE { get; set; }
    public bool FUEL { get; set; }
    public bool STAKABLE { get; set; }
    public bool FIAT { get; set; }
    public bool SWAPPABLE { get; set; }
    public bool BURNABLE { get; set; }
    public int DECIMALS { get; set; }
    public string CURRENT_SUPPLY { get; set; }
    public string MAX_SUPPLY { get; set; }
    public string BURNED_SUPPLY { get; set; }
    public string SCRIPT_RAW { get; set; }
    public int AddressId { get; set; }
    public virtual Address Address { get; set; }
    public int OwnerId { get; set; }
    public virtual Address Owner { get; set; }
    public decimal PRICE_USD { get; set; }
    public decimal PRICE_EUR { get; set; }
    public decimal PRICE_GBP { get; set; }
    public decimal PRICE_JPY { get; set; }
    public decimal PRICE_CAD { get; set; }
    public decimal PRICE_AUD { get; set; }
    public decimal PRICE_CNY { get; set; }
    public decimal PRICE_RUB { get; set; }
    public int ChainId { get; set; }
    public virtual Chain Chain { get; set; }
    public int ContractId { get; set; }

    public virtual Contract Contract { get; set; }
    public virtual List<TokenDailyPrice> TokenDailyPrices { get; set; }
    public virtual List<Infusion> Infusions { get; set; }
    public virtual List<External> Externals { get; set; }
    public virtual List<TokenEvent> TokenEvents { get; set; }
    public virtual List<InfusionEvent> BaseSymbolInfusionEvents { get; set; }
    public virtual List<InfusionEvent> InfusedSymbolInfusionEvents { get; set; }
    public virtual List<MarketEvent> BaseSymbolMarketEvents { get; set; }
    public virtual List<MarketEvent> QuoteSymbolMarketEvents { get; set; }
    public virtual TokenPriceState TokenPriceState { get; set; }
    public virtual List<AddressBalance> AddressBalances { get; set; }
}

public class TokenDailyPrice
{
    public int ID { get; set; }
    public long DATE_UNIX_SECONDS { get; set; }
    public decimal PRICE_SOUL { get; set; }
    public decimal PRICE_NEO { get; set; }
    public decimal PRICE_ETH { get; set; }
    public decimal PRICE_USD { get; set; }
    public decimal PRICE_EUR { get; set; }
    public decimal PRICE_GBP { get; set; }
    public decimal PRICE_JPY { get; set; }
    public decimal PRICE_CAD { get; set; }
    public decimal PRICE_AUD { get; set; }
    public decimal PRICE_CNY { get; set; }
    public decimal PRICE_RUB { get; set; }
    public int TokenId { get; set; }
    public virtual Token Token { get; set; }


    public override string ToString()
    {
        return
            $"Token daily price '{Token.SYMBOL}' for {UnixSeconds.Log(DATE_UNIX_SECONDS)}: SOUL: {PRICE_SOUL}, NEO: {PRICE_NEO}, ETH: {PRICE_ETH}, USD: {PRICE_USD}, EUR: {PRICE_EUR}, GBP: {PRICE_GBP}, JPY: {PRICE_JPY}, CAD: {PRICE_CAD}, AUD: {PRICE_AUD}, CNY: {PRICE_CNY}, RUB: {PRICE_RUB}";
    }
}

public class NftOwnership
{
    public int ID { get; set; }

    // Timestamp of last ownership changing tx (to avoid older tx changing ownership of NFT during multithreaded events loading)
    public long LAST_CHANGE_UNIX_SECONDS { get; set; }
    public int AMOUNT { get; set; }
    public int NftId { get; set; }
    public virtual Nft Nft { get; set; }
    public int AddressId { get; set; }
    public virtual Address Address { get; set; }
}

public class Nft
{
    public int ID { get; set; }
    public long DM_UNIX_SECONDS { get; set; } // Last modification date (in database).
    public string TOKEN_ID { get; set; }
    public string TOKEN_URI { get; set; }
    // METADATA START
    public string DESCRIPTION { get; set; }
    public string NAME { get; set; }
    public string ROM { get; set; }
    public string RAM { get; set; }
    public string IMAGE { get; set; }
    public string VIDEO { get; set; }
    public string INFO_URL { get; set; }
    public long MINT_DATE_UNIX_SECONDS { get; set; } // Last modification date (in database).
    public int MINT_NUMBER { get; set; }
    public JsonDocument OFFCHAIN_API_RESPONSE { get; set; }
    public JsonDocument CHAIN_API_RESPONSE { get; set; }
    public bool? BURNED { get; set; }
    public bool NSFW { get; set; }
    public bool BLACKLISTED { get; set; }
    public bool? METADATA_UPDATE { get; set; }
    // METADATA END
    public int? SeriesId { get; set; }
    public virtual Series Series { get; set; }
    public int? CreatorAddressId { get; set; }
    public virtual Address CreatorAddress { get; set; }
    public virtual List<NftOwnership> NftOwnerships { get; set; }
    public int ChainId { get; set; }
    public virtual Chain Chain { get; set; }
    public int ContractId { get; set; }
    public virtual Contract Contract { get; set; }
    public virtual List<Event> Events { get; set; }
    public virtual List<Infusion> Infusions { get; set; }
    // Relation with infused NFTs
    public int? InfusedIntoId { get; set; }
    public virtual Nft InfusedInto { get; set; }
    public virtual List<Nft> InfusedNfts { get; set; }
}

public class SeriesMode
{
    public int ID { get; set; }
    public string MODE_NAME { get; set; }
    public virtual List<Series> Series { get; set; }
}

public class Series
{
    public int ID { get; set; }
    public int ContractId { get; set; }
    public virtual Contract Contract { get; set; }
    public string SERIES_ID { get; set; }
    public int CURRENT_SUPPLY { get; set; }
    public int MAX_SUPPLY { get; set; }
    public int? SeriesModeId { get; set; }
    public virtual SeriesMode SeriesMode { get; set; }
    public string NAME { get; set; }
    public string DESCRIPTION { get; set; }
    public string IMAGE { get; set; }
    public decimal ROYALTIES { get; set; }
    public int TYPE { get; set; }
    public string ATTR_TYPE_1 { get; set; }
    public string ATTR_VALUE_1 { get; set; }
    public string ATTR_TYPE_2 { get; set; }
    public string ATTR_VALUE_2 { get; set; }
    public string ATTR_TYPE_3 { get; set; }
    public string ATTR_VALUE_3 { get; set; }
    public bool HAS_LOCKED { get; set; }
    public bool? NSFW { get; set; }
    public bool? BLACKLISTED { get; set; }
    public long DM_UNIX_SECONDS { get; set; } // Date - Modified
    public int? CreatorAddressId { get; set; }
    public virtual Address CreatorAddress { get; set; }
    public virtual List<Nft> Nfts { get; set; }
}

public class Infusion
{
    public int ID { get; set; }
    public string KEY { get; set; }
    public string VALUE { get; set; }
    public int? TokenId { get; set; }
    public virtual Token Token { get; set; }
    public int NftId { get; set; }
    public virtual Nft Nft { get; set; }
    public virtual List<InfusionEvent> InfusionEvents { get; set; }
}

public class FiatExchangeRate
{
    public int ID { get; set; }
    public string SYMBOL { get; set; }
    public decimal USD_PRICE { get; set; }
}

public class Platform
{
    public int ID { get; set; }
    public string NAME { get; set; }
    public string CHAIN { get; set; }
    public string FUEL { get; set; }
    public bool HIDDEN { get; set; }
    public virtual List<TransactionSettleEvent> TransactionSettleEvents { get; set; }
    public virtual List<External> Externals { get; set; }
    public virtual List<PlatformInterop> PlatformInterops { get; set; }
    public virtual List<PlatformToken> PlatformTokens { get; set; }
}

public class PlatformToken
{
    public int ID { get; set; }
    public int PlatformId { get; set; }
    public virtual Platform Platform { get; set; }
    public string NAME { get; set; }
}

public class PlatformInterop
{
    public int ID { get; set; }
    public int PlatformId { get; set; }
    public virtual Platform Platform { get; set; }
    public int LocalAddressId { get; set; }
    public virtual Address LocalAddress { get; set; }
    public string EXTERNAL { get; set; }
}

public class External
{
    public int ID { get; set; }
    public int PlatformId { get; set; }
    public virtual Platform Platform { get; set; }
    public int TokenId { get; set; }
    public virtual Token Token { get; set; }
    public string HASH { get; set; }
}

public class Organization
{
    public int ID { get; set; }
    public string NAME { get; set; }
    public virtual List<OrganizationEvent> OrganizationEvents { get; set; }
    public virtual List<OrganizationAddress> OrganizationAddresses { get; set; }
}

public class OrganizationEvent
{
    public int ID { get; set; }
    public int OrganizationId { get; set; }
    public virtual Organization Organization { get; set; }
    public int AddressId { get; set; }
    public virtual Address Address { get; set; }
    public int EventId { get; set; }
    public virtual Event Event { get; set; }
}

public class StringEvent
{
    public int ID { get; set; }
    public string STRING_VALUE { get; set; }
    public int EventId { get; set; }
    public virtual Event Event { get; set; }
}

//used for validator event data, atm
public class AddressEvent
{
    public int ID { get; set; }
    public int AddressId { get; set; }
    public virtual Address Address { get; set; }
    public int EventId { get; set; }
    public virtual Event Event { get; set; }
}

public class TransactionSettleEvent
{
    public int ID { get; set; }
    public string HASH { get; set; }
    public int PlatformId { get; set; }

    public virtual Platform Platform { get; set; }

    //chain name should be in platform
    public int EventId { get; set; }
    public virtual Event Event { get; set; }
}

//used for file events, atm
public class HashEvent
{
    public int ID { get; set; }
    public string HASH { get; set; }
    public int EventId { get; set; }
    public virtual Event Event { get; set; }
}

public class GasEvent
{
    public int ID { get; set; }
    public string PRICE { get; set; }
    public string AMOUNT { get; set; }
    public int AddressId { get; set; }
    public virtual Address Address { get; set; }
    public int EventId { get; set; }
    public virtual Event Event { get; set; }
}

public class SaleEventKind
{
    public int ID { get; set; }
    public string NAME { get; set; }
    public int ChainId { get; set; }
    public virtual Chain Chain { get; set; }
    public virtual List<SaleEvent> SaleEvents { get; set; }
}

public class SaleEvent
{
    public int ID { get; set; }
    public string HASH { get; set; }
    public int SaleEventKindId { get; set; }
    public virtual SaleEventKind SaleEventKind { get; set; }
    public int EventId { get; set; }
    public virtual Event Event { get; set; }
}

public class ChainEvent
{
    public int ID { get; set; }
    public string NAME { get; set; }
    public string VALUE { get; set; }
    public int ChainId { get; set; }
    public virtual Chain Chain { get; set; }
    public int EventId { get; set; }
    public virtual Event Event { get; set; }
}

public class TokenEvent
{
    public int ID { get; set; }
    public int TokenId { get; set; }
    public virtual Token Token { get; set; }
    public string VALUE { get; set; }
    public string CHAIN_NAME { get; set; }
    public int EventId { get; set; }
    public virtual Event Event { get; set; }
}

public class InfusionEvent
{
    public int ID { get; set; }
    public string TOKEN_ID { get; set; }
    public int BaseTokenId { get; set; }
    public virtual Token BaseToken { get; set; }
    public int InfusedTokenId { get; set; }
    public virtual Token InfusedToken { get; set; }
    public string INFUSED_VALUE { get; set; }
    public int EventId { get; set; }
    public virtual Event Event { get; set; }
    public int? InfusionId { get; set; }
    public virtual Infusion Infusion { get; set; }
}

public class MarketEventKind
{
    public int ID { get; set; }
    public string NAME { get; set; }
    public int ChainId { get; set; }
    public virtual Chain Chain { get; set; }
    public virtual List<MarketEvent> MarketEvents { get; set; }
}

public class MarketEvent
{
    public int ID { get; set; }
    public int BaseTokenId { get; set; }
    public virtual Token BaseToken { get; set; }
    public int QuoteTokenId { get; set; }
    public virtual Token QuoteToken { get; set; }
    public int MarketEventKindId { get; set; }
    public virtual MarketEventKind MarketEventKind { get; set; }
    public string MARKET_ID { get; set; }
    public string PRICE { get; set; }
    public string END_PRICE { get; set; }
    public int EventId { get; set; }
    public virtual Event Event { get; set; }
    public virtual MarketEventFiatPrice MarketEventFiatPrice { get; set; }
}

//to avoid to double data, we create a table for oracle url and content, and just ref it here
public class BlockOracle
{
    public int ID { get; set; }
    public int OracleId { get; set; }
    public virtual Oracle Oracle { get; set; }
    public int BlockId { get; set; }
    public virtual Block Block { get; set; }
}

public class Oracle
{
    public int ID { get; set; }
    public string URL { get; set; }
    public string CONTENT { get; set; }
    public virtual List<BlockOracle> BlockOracles { get; set; }
}

public class SignatureKind
{
    public int ID { get; set; }
    public string NAME { get; set; }
    public virtual List<Signature> Signatures { get; set; }
}

public class Signature
{
    public int ID { get; set; }
    public int SignatureKindId { get; set; }
    public virtual SignatureKind SignatureKind { get; set; }
    public string DATA { get; set; }
    public int TransactionId { get; set; }
    public virtual Transaction Transaction { get; set; }
}

public class OrganizationAddress
{
    public int ID { get; set; }
    public int OrganizationId { get; set; }
    public virtual Organization Organization { get; set; }
    public int AddressId { get; set; }
    public virtual Address Address { get; set; }
}

public class MarketEventFiatPrice
{
    public int ID { get; set; }
    public decimal PRICE_USD { get; set; }
    public decimal PRICE_END_USD { get; set; }
    public string FIAT_NAME { get; set; }
    public int MarketEventId { get; set; }
    public virtual MarketEvent MarketEvent { get; set; }
}

public class TokenPriceState
{
    public int ID { get; set; }
    public int TokenId { get; set; }
    public virtual Token Token { get; set; }
    public long LAST_CHECK_DATE_UNIX_SECONDS { get; set; }
    public bool COIN_GECKO { get; set; }
}

//NEW
public class AddressTransaction
{
    public int ID { get; set; }
    public int AddressId { get; set; }
    public virtual Address Address { get; set; }
    public int TransactionId { get; set; }
    public virtual Transaction Transaction { get; set; }
}

public class AddressStake
{
    public int ID { get; set; }
    public int AddressId { get; set; }
    public virtual Address Address { get; set; }
    public string AMOUNT { get; set; }
    public long TIME { get; set; }
    public string UNCLAIMED { get; set; }
}

public class AddressStorage
{
    public int ID { get; set; }
    public int AddressId { get; set; }
    public virtual Address Address { get; set; }
    public long AVAILABLE { get; set; }
    public long USED { get; set; }
    public string AVATAR { get; set; }
}

public class AddressBalance
{
    public int ID { get; set; }
    public int TokenId { get; set; }
    public virtual Token Token { get; set; }
    public int ChainId { get; set; }
    public virtual Chain Chain { get; set; }
    public int AddressId { get; set; }
    public virtual Address Address { get; set; }
    public string AMOUNT { get; set; }
}

public class AddressValidatorKind
{
    public int ID { get; set; }
    public string NAME { get; set; }
    public virtual List<Address> Addresses { get; set; }
}
