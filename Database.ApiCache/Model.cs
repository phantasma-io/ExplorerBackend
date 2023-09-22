using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Database.ApiCache;

//since we build our db struct here, we have a different naming as well
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedAutoPropertyAccessor.Global
public class ApiCacheDbContext : DbContext
{
    public DbSet<Chain> Chains { get; set; }
    public DbSet<Contract> Contracts { get; set; }
    public DbSet<Nft> Nfts { get; set; }
    public DbSet<Block> Blocks { get; set; }
    
    private static string DetectConfigFilePath()
    {
        var fileName = "explorer-backend-config.json";

        // TODO move into "config" subfolder everywhere
        // This path is valid when we are updating database on deployed server.
        var configFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../..", fileName);
        if (!File.Exists(configFile))
        {
            // This path is valid when we are launching our deployed server.
            configFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", fileName);
            if (!File.Exists(configFile))
            {
                // Checking if we are using it locally to create/update local database.
                configFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../..", fileName);
            }
        }

        return configFile;
    }
    
    private static string GetConnectionString()
    {
        if (!string.IsNullOrEmpty(Settings.Default?.ConnectionString))
            return Settings.Default.ConnectionString;
        
        Settings.Load(new ConfigurationBuilder().AddJsonFile(DetectConfigFilePath(), optional: false).Build().GetSection("DatabaseConfiguration"));
        return Settings.Default!.ConnectionString;
    }

    public static int GetConnectionMaxRetries()
    {
        Settings.Load(new ConfigurationBuilder().AddJsonFile(DetectConfigFilePath(), false).Build()
            .GetSection("DatabaseConfiguration"));
        return Settings.Default.ConnectMaxRetries;
    }


    //for now...
    public int GetConnectionRetryTimeout()
    {
        Settings.Load(new ConfigurationBuilder().AddJsonFile(DetectConfigFilePath(), false).Build()
            .GetSection("DatabaseConfiguration"));
        return Settings.Default.ConnectRetryTimeout;
    }


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(GetConnectionString());
        optionsBuilder.EnableSensitiveDataLogging();
        optionsBuilder.UseLazyLoadingProxies();
    }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Here we add relations between tables and indexes.

        //////////////////////
        // Chain
        //////////////////////

        // Indexes

        modelBuilder.Entity<Chain>()
            .HasIndex(x => x.SHORT_NAME)
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

        modelBuilder.Entity<Contract>()
            .HasIndex(x => new {x.ChainId, x.HASH})
            .IsUnique();

        //////////////////////
        // Nfts
        //////////////////////

        // FKs

        modelBuilder.Entity<Nft>()
            .HasOne(x => x.Contract)
            .WithMany(y => y.Nfts)
            .HasForeignKey(x => x.ContractId);

        // Indexes

        modelBuilder.Entity<Nft>()
            .HasIndex(x => new {x.ContractId, x.TOKEN_ID})
            .IsUnique();

        //////////////////////
        // Block
        //////////////////////

        // FKs

        modelBuilder.Entity<Block>()
            .HasOne(x => x.Chain)
            .WithMany(y => y.Blocks)
            .HasForeignKey(x => x.ChainId);

        // Indexes

        modelBuilder.Entity<Block>()
            .HasIndex(x => new {x.ChainId, x.HEIGHT})
            .IsUnique();
    }
}

public class Chain
{
    public int ID { get; set; }
    public string SHORT_NAME { get; set; }
    public string CURRENT_HEIGHT { get; set; }
    public virtual List<Contract> Contracts { get; set; }
    public virtual List<Block> Blocks { get; set; }
}

public class Contract
{
    public int ID { get; set; }
    public string HASH { get; set; }
    public int ChainId { get; set; }
    public virtual Chain Chain { get; set; }
    public virtual List<Nft> Nfts { get; set; }
}

public class Nft
{
    public int ID { get; set; }
    public string TOKEN_ID { get; set; }
    public JsonDocument CHAIN_API_RESPONSE { get; set; }
    public long CHAIN_API_RESPONSE_DM_UNIX_SECONDS { get; set; } // Date - Modified
    public JsonDocument OFFCHAIN_API_RESPONSE { get; set; }
    public long OFFCHAIN_API_RESPONSE_DM_UNIX_SECONDS { get; set; } // Date - Modified
    public int ContractId { get; set; }
    public virtual Contract Contract { get; set; }
}

public class Block // Cache of known blocks that is used to speed up BSC/ETH resync.
{
    public int ID { get; set; }
    public string HEIGHT { get; set; }
    public long TIMESTAMP { get; set; }
    public JsonDocument DATA { get; set; }
    public int ChainId { get; set; }
    public virtual Chain Chain { get; set; }
}
