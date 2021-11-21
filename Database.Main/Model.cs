using GhostDevs.Commons;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text.Json;

// Here we have all tables, fields and their relations for backend database.
// Also public method GetConnectionString() availabe, allowing to get database connection string,
// which can be used by PostgreSQLConnector module to connect to database and execute raw SQL queries.

namespace Database.Main
{
    public class MainDbContext : DbContext
    {
        // Keeping DB configs on same level as "bin" folder.
        // If path contains "Database.Main" - it means we are running database update.
        public static readonly string ConfigDirectory = AppDomain.CurrentDomain.BaseDirectory.Contains("Database.Main") ? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../..") : System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..");
        public static string ConfigFile =>  System.IO.Path.Combine(ConfigDirectory, "explorer-backend-config.json");

        public string GetConnectionString()
        {
            Settings.Load(new ConfigurationBuilder().AddJsonFile(ConfigFile, optional: false).Build().GetSection("DatabaseConfiguration"));
            return Settings.Default.ConnectionString;
        }
        // TODO Remove this hack method when not needed anymore.
        public string GetConnectionStringCustomDbName(string dbName)
        {
            Settings.Load(new ConfigurationBuilder().AddJsonFile(ConfigFile, optional: false).Build().GetSection("DatabaseConfiguration"));
            return Settings.Default.ConnectionStringNoDbName + dbName;
        }
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

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(GetConnectionString());
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.UseLazyLoadingProxies();

            optionsBuilder.UseNpgsql(GetConnectionString(),
                optionsAction =>
                {
                    // optionsAction.EnableRetryOnFailure();
                    optionsAction.CommandTimeout((int)TimeSpan.FromMinutes(10).TotalSeconds);
                });
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Here we add relations between tables and indexes.


            //////////////////////
            /// Chain
            //////////////////////

            // FKs

            modelBuilder.Entity<Chain>()
                .HasOne(x => x.MainToken)
                .WithOne(y => y.Chain2)
                .HasForeignKey<Chain>(x => x.MainTokenId);

            // Indexes

            modelBuilder.Entity<Chain>()
                .HasIndex(x => x.NAME)
                .IsUnique();

            //////////////////////
            /// Contract
            //////////////////////

            // FKs

            //////////////////////
            /// Block
            //////////////////////

            // FKs

            modelBuilder.Entity<Block>()
                .HasOne(x => x.Chain)
                .WithMany(y => y.Blocks)
                .HasForeignKey(x => x.ChainId);

            // Indexes

            modelBuilder.Entity<Block>()
                .HasIndex(x => new { x.TIMESTAMP_UNIX_SECONDS });

            modelBuilder.Entity<Block>()
                .HasIndex(x => new { x.ChainId, x.HEIGHT });

            //////////////////////
            /// Contract
            //////////////////////

            // FKs

            //////////////////////
            /// Transaction
            //////////////////////

            // FKs

            modelBuilder.Entity<Transaction>()
                .HasOne(x => x.Block)
                .WithMany(y => y.Transactions)
                .HasForeignKey(x => x.BlockId);

            // Indexes

            // We should not make it unique to allow mixing mainnet and testnet in testnet DB.
            modelBuilder.Entity<Transaction>()
                .HasIndex(x => new { x.BlockId, x.INDEX });

            modelBuilder.Entity<Transaction>()
                .HasIndex(x => new { x.HASH });

            //////////////////////
            /// EventKind
            //////////////////////

            // FKs

            modelBuilder.Entity<EventKind>()
                .HasOne(x => x.Chain)
                .WithMany(y => y.EventKinds)
                .HasForeignKey(x => x.ChainId);

            // Indexes

            modelBuilder.Entity<EventKind>()
                .HasIndex(x => x.NAME);

            modelBuilder.Entity<EventKind>()
                .HasIndex(x => new { x.ChainId, x.NAME })
                .IsUnique();

            //////////////////////
            /// Address
            //////////////////////

            // FKs

            modelBuilder.Entity<Address>()
                .HasOne(x => x.Chain)
                .WithMany(y => y.Addresses)
                .HasForeignKey(x => x.ChainId);

            // Indexes

            modelBuilder.Entity<Address>()
                .HasIndex(x => new { x.ChainId, x.ADDRESS })
                .IsUnique();

            modelBuilder.Entity<Address>()
                .HasIndex(x => new { x.ADDRESS_NAME });

            modelBuilder.Entity<Address>()
                .HasIndex(x => new { x.USER_NAME });

            modelBuilder.Entity<Address>()
                .HasIndex(x => new { x.NAME_LAST_UPDATED_UNIX_SECONDS });

            //////////////////////
            /// Event
            //////////////////////

            // FKs

            modelBuilder.Entity<Event>()
                .HasOne(x => x.Chain)
                .WithMany(y => y.Events)
                .HasForeignKey(x => x.ChainId);

            modelBuilder.Entity<Event>()
                .HasOne(x => x.Transaction)
                .WithMany(y => y.Events)
                .HasForeignKey(x => x.TransactionId);

            modelBuilder.Entity<Event>()
                .HasOne(x => x.EventKind)
                .WithMany(y => y.Events)
                .HasForeignKey(x => x.EventKindId);

            modelBuilder.Entity<Event>()
                .HasOne(x => x.Contract)
                .WithMany(y => y.Events)
                .HasForeignKey(x => x.ContractId);

            modelBuilder.Entity<Event>()
                .HasOne(x => x.QuoteSymbol)
                .WithMany(y => y.Events)
                .HasForeignKey(x => x.QuoteSymbolId);

            modelBuilder.Entity<Event>()
                .HasOne(x => x.InfusedSymbol)
                .WithMany(y => y.InfusionEvents)
                .HasForeignKey(x => x.InfusedSymbolId);

            modelBuilder.Entity<Event>()
                .HasOne(x => x.Infusion)
                .WithMany(y => y.Events)
                .HasForeignKey(x => x.InfusionId);

            modelBuilder.Entity<Event>()
                .HasOne(x => x.Address)
                .WithMany()
                .HasForeignKey(x => x.AddressId);

            modelBuilder.Entity<Event>()
                .HasOne(x => x.SourceAddress)
                .WithMany()
                .HasForeignKey(x => x.SourceAddressId);

            // Indexes

            modelBuilder.Entity<Event>()
                .HasIndex(x => x.DM_UNIX_SECONDS);
            modelBuilder.Entity<Event>()
                .HasIndex(x => x.TIMESTAMP_UNIX_SECONDS);
            modelBuilder.Entity<Event>()
                .HasIndex(x => x.DATE_UNIX_SECONDS);

            modelBuilder.Entity<Event>()
                .HasIndex(x => new { x.TransactionId, x.INDEX });

            modelBuilder.Entity<Event>()
                .HasIndex(x => new { x.INDEX });

            modelBuilder.Entity<Event>()
                .HasIndex(x => new { x.ContractId, x.TOKEN_ID });

            modelBuilder.Entity<Event>()
                .HasIndex(x => new { x.PRICE_USD });

            modelBuilder.Entity<Event>()
                .HasIndex(x => new { x.HIDDEN });

            modelBuilder.Entity<Event>()
                .HasIndex(x => new { x.BURNED });

            modelBuilder.Entity<Event>()
                .HasIndex(x => x.NSFW);
            modelBuilder.Entity<Event>()
                .HasIndex(x => x.BLACKLISTED);

            //////////////////////
            /// Token
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

            // Indexes

            modelBuilder.Entity<Token>()
                .HasIndex(x => new { x.SYMBOL });

            modelBuilder.Entity<Token>()
                .HasIndex(x => new { x.ChainId, x.ContractId, x.SYMBOL })
                .IsUnique();

            //////////////////////
            /// TokenDailyPrice
            //////////////////////

            // FKs

            modelBuilder.Entity<TokenDailyPrice>()
                .HasOne(x => x.Token)
                .WithMany(y => y.TokenDailyPrices)
                .HasForeignKey(x => x.TokenId);

            // Indexes

            modelBuilder.Entity<TokenDailyPrice>()
                .HasIndex(x => new { x.DATE_UNIX_SECONDS });

            //////////////////////
            /// NftOwnership
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
                .HasIndex(x => new { x.AddressId, x.NftId })
                .IsUnique();

            //////////////////////
            /// Nft
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
                .HasIndex(x => new { x.ContractId, x.TOKEN_ID })
                .IsUnique();

            // Can't make it unique, AirNFT (BSC) has some duplicates.
            modelBuilder.Entity<Nft>()
                .HasIndex(x => new { x.ContractId, x.TOKEN_URI });

            modelBuilder.Entity<Nft>()
                .HasIndex(x => x.NAME);

            modelBuilder.Entity<Nft>()
                .HasIndex(x => x.DESCRIPTION);

            modelBuilder.Entity<Nft>()
                .HasIndex(x => x.MINT_DATE_UNIX_SECONDS);

            modelBuilder.Entity<Nft>()
                .HasIndex(x => x.MINT_NUMBER);

            modelBuilder.Entity<Nft>()
                .HasIndex(x => x.BURNED);

            modelBuilder.Entity<Nft>()
                .HasIndex(x => x.NSFW);
            modelBuilder.Entity<Nft>()
                .HasIndex(x => x.BLACKLISTED);
            modelBuilder.Entity<Nft>()
                .HasIndex(x => x.METADATA_UPDATE);

            //////////////////////
            /// SeriesMode
            //////////////////////

            // Indexes

            modelBuilder.Entity<SeriesMode>()
                .HasIndex(x => x.MODE_NAME);

            //////////////////////
            /// Series
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
                .HasIndex(x => new { x.SERIES_ID });

            modelBuilder.Entity<Series>()
                .HasIndex(x => new { x.ContractId, x.SERIES_ID })
                .IsUnique();

            modelBuilder.Entity<Series>()
                .HasIndex(x => x.NAME);

            modelBuilder.Entity<Series>()
                .HasIndex(x => x.DESCRIPTION);

            modelBuilder.Entity<Series>()
                .HasIndex(x => x.TYPE);

            modelBuilder.Entity<Series>()
                .HasIndex(x => x.HAS_LOCKED);
            
            modelBuilder.Entity<Series>()
                .HasIndex(x => x.BLACKLISTED);

            modelBuilder.Entity<Series>()
                .HasIndex(x => x.NSFW);

            modelBuilder.Entity<Series>()
                .HasIndex(x => x.DM_UNIX_SECONDS);

            //////////////////////
            /// Infusion
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
                .HasIndex(x => x.KEY );

            //////////////////////
            /// FiatExchangeRate
            //////////////////////

            // Indexes

            modelBuilder.Entity<FiatExchangeRate>()
                .HasIndex(x => x.SYMBOL )
                .IsUnique();
        }
    }

    public class Chain
    {
        public int ID { get; set; }
        public string NAME { get; set; }
        public string CURRENT_HEIGHT { get; set; }
        public virtual List<Nft> Nfts { get; set; }
        public int? MainTokenId { get; set; } // Token to be used for crypto price calculations for this chain (ex. SOUL, NEO).
        public virtual Token MainToken { get; set; }
        public virtual List<Contract> Contracts { get; set; }
        public virtual List<Token> Tokens { get; set; }
        public virtual List<Block> Blocks { get; set; }
        public virtual List<Address> Addresses { get; set; }
        public virtual List<EventKind> EventKinds { get; set; }
        public virtual List<Event> Events { get; set; }
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
        public int? TokenId { get; set; }
        public virtual Token Token { get; set; }
        public virtual List<Event> Events { get; set; }
        public virtual List<Nft> Nfts { get; set; }
        public virtual List<Series> Series { get; set; }

    }
    public class Block
    {
        public int ID { get; set; }
        public string HEIGHT { get; set; }
        public Int64 TIMESTAMP_UNIX_SECONDS { get; set; }
        public int ChainId { get; set; }
        public virtual Chain Chain { get; set; }
        public virtual List<Transaction> Transactions { get; set; }
    }
    public class Transaction
    {
        public int ID { get; set; }
        public string HASH { get; set; }
        public int INDEX { get; set; } // Index of tx in block
        public int BlockId { get; set; }
        public virtual Block Block { get; set; }
        public virtual List<Event> Events { get; set; }
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
        public string USER_TITLE { get; set; }
        public Int64 NAME_LAST_UPDATED_UNIX_SECONDS { get; set; }
        public int ChainId { get; set; }
        public virtual Chain Chain { get; set; }
        public virtual List<Event> Events { get; set; }
        public virtual List<Nft> Nfts { get; set; }
        public virtual List<NftOwnership> NftOwnerships { get; set; }
        public virtual List<Series> Serieses { get; set; }
    }
    public class Event
    {
        public int ID { get; set; }
        public Int64 DM_UNIX_SECONDS { get; set; } // Last modification date (in database).
        public Int64 TIMESTAMP_UNIX_SECONDS { get; set; }
        public Int64 DATE_UNIX_SECONDS { get; set; } // Same as TIMESTAMP, but without time.
        public int INDEX { get; set; } // Index of event in tx.
                                       // EF do not preserve insertion order for ID,
                                       // but we need to know exact order of events, so we add special field for this.
        public string TOKEN_ID { get; set; }
        public int TOKEN_AMOUNT { get; set; } // Sadly only dotnet 6 supports default values
        public string CONTRACT_AUCTION_ID { get; set; }
        public string PRICE { get; set; }
        public decimal PRICE_USD { get; set; } // Historically correct USD price.
        public bool HIDDEN { get; set; }
        public bool? BURNED { get; set; }
        public bool NSFW { get; set; }
        public bool BLACKLISTED { get; set; }
        public int AddressId { get; set; }
        public virtual Address Address { get; set; }
        public int? SourceAddressId { get; set; }
        public virtual Address SourceAddress { get; set; }
        public int ChainId { get; set; }
        public virtual Chain Chain { get; set; }
        public int ContractId { get; set; }
        public virtual Contract Contract { get; set; }
        public int? TransactionId { get; set; }
        public virtual Transaction Transaction { get; set; }
        public int EventKindId { get; set; }
        public virtual EventKind EventKind { get; set; }
        public int? QuoteSymbolId { get; set; }
        public virtual Token QuoteSymbol { get; set; }
        public int? InfusedSymbolId { get; set; }
        public virtual Token InfusedSymbol { get; set; }
        public string INFUSED_VALUE { get; set; }
        public int? InfusionId { get; set; }
        public virtual Infusion Infusion { get; set; }

        public int? NftId { get; set; }
        public virtual Nft Nft { get; set; }
    }
    public class Token
    {
        public int ID { get; set; }
        public string SYMBOL { get; set; }
        public bool? FUNGIBLE { get; set; }
        public int? DECIMALS { get; set; }
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
        public int Chain2Id { get; set; }
        public virtual Chain Chain2 { get; set; } // Navigation properties can only participate in a single relationship, so adding second one
        public int ContractId { get; set; }
        public virtual Contract Contract { get; set; }
        public virtual List<Event> Events { get; set; }
        public virtual List<Event> InfusionEvents { get; set; }
        public virtual List<TokenDailyPrice> TokenDailyPrices { get; set; }
        public virtual List<Infusion> Infusions { get; set; }
    }
    public class TokenDailyPrice
    {
        public int ID { get; set; }
        public Int64 DATE_UNIX_SECONDS { get; set; }
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
            return $"Token daily price '{Token.SYMBOL}' for {UnixSeconds.Log(DATE_UNIX_SECONDS)}: SOUL: {PRICE_SOUL}, NEO: {PRICE_NEO}, ETH: {PRICE_ETH}, USD: {PRICE_USD}, EUR: {PRICE_EUR}, GBP: {PRICE_GBP}, JPY: {PRICE_JPY}, CAD: {PRICE_CAD}, AUD: {PRICE_AUD}, CNY: {PRICE_CNY}, RUB: {PRICE_RUB}";
        }
    }
    public class NftOwnership
    {
        public int ID { get; set; }
        public Int64 LAST_CHANGE_UNIX_SECONDS { get; set; } // Timestamp of last ownership changing tx (to avoid older tx changing ownership of NFT during multithreaded events loading)
        public int AMOUNT { get; set; }
        public int NftId { get; set; }
        public virtual Nft Nft { get; set; }
        public int AddressId { get; set; }
        public virtual Address Address { get; set; }
    }
    public class Nft
    {
        public int ID { get; set; }
        public Int64 DM_UNIX_SECONDS { get; set; } // Last modification date (in database).
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
        public Int64 MINT_DATE_UNIX_SECONDS { get; set; } // Last modification date (in database).
        public int MINT_NUMBER { get; set; }
        public JsonDocument OFFCHAIN_API_RESPONSE { get; set; }
        public JsonDocument CHAIN_API_RESPONSE { get; set; }
        public bool? BURNED { get; set; }
        public bool NSFW { get; set; }
        public bool BLACKLISTED { get; set; }
        public int VIEW_COUNT { get; set; }
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
        public Int64 DM_UNIX_SECONDS { get; set; } // Date - Modified
        public int? CreatorAddressId { get; set; }
        public virtual Address CreatorAddress { get; set; }
        public virtual List<Nft> Nfts { get; set; }
    }
    public class Infusion
    {
        public int ID { get; set; }
        public string KEY { get; set; }
        public string VALUE { get; set; }
        public virtual List<Event> Events { get; set; }
        public int? TokenId { get; set; }
        public virtual Token Token { get; set; }
        public int NftId { get; set; }
        public virtual Nft Nft { get; set; }
    }
    public class FiatExchangeRate
    {
        public int ID { get; set; }
        public string SYMBOL { get; set; }
        public decimal USD_PRICE { get; set; }
    }
}
