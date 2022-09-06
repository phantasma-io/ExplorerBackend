using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Serilog;
using static System.IO.Path;

namespace Backend.PluginEngine;

public abstract class Plugin : IDisposable
{
    public static readonly List<Plugin> Plugins = new();
    public static readonly List<IDBAccessPlugin> DBAPlugins = new();
    public static readonly List<IBlockchainPlugin> BlockchainPlugins = new();

    public static readonly string PluginsDirectory =
        Combine(GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? string.Empty, "Plugins");

    private static readonly FileSystemWatcher configWatcher;


    static Plugin()
    {
        if ( !Directory.Exists(PluginsDirectory) ) return;
        configWatcher = new FileSystemWatcher(PluginsDirectory)
        {
            EnableRaisingEvents = true,
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.Size
        };
        configWatcher.Changed += Plugin_Changed;
        configWatcher.Created += Plugin_Changed;
        AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
    }


    protected Plugin()
    {
        Plugins.Add(this);
        if ( this is IDBAccessPlugin dbAccessPlugin ) DBAPlugins.Add(dbAccessPlugin);

        if ( this is IBlockchainPlugin blockchainPlugin ) BlockchainPlugins.Add(blockchainPlugin);

        Configure();
    }


    // New config placement, next to main backend config.
    public virtual string ConfigFile =>
        Combine(Combine(PluginsDirectory, "../.."), $"{GetType().Assembly.GetName().Name}.json");

    public virtual string Name => GetType().Name;
    public string Path => Combine(PluginsDirectory, GetType().Assembly.ManifestModule.ScopeName);
    public virtual Version Version => GetType().Assembly.GetName().Version;


    public virtual void Dispose()
    {
    }


    protected virtual void Configure()
    {
    }


    private static Assembly AssemblyLoadWrapper(string path)
    {
        var pdbFilePath = ChangeExtension(path, "pdb");
        return File.Exists(pdbFilePath)
            ? Assembly.Load(File.ReadAllBytes(path), File.ReadAllBytes(pdbFilePath))
            : Assembly.Load(File.ReadAllBytes(path));
    }


    private static void Plugin_Changed(object sender, FileSystemEventArgs e)
    {
        switch ( GetExtension(e.Name) )
        {
            case ".json":
                try
                {
                    Plugins.FirstOrDefault(p => p.ConfigFile == e.FullPath)?.Configure();
                }
                catch ( FormatException )
                {
                }

                break;
            case ".dll":
                if ( e.ChangeType != WatcherChangeTypes.Created ) return;

                if ( GetDirectoryName(e.FullPath) != PluginsDirectory ) return;

                try
                {
                    LoadPlugin(AssemblyLoadWrapper(e.FullPath));
                }
                catch
                {
                    // ignored
                }

                break;
        }
    }


    private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
    {
        if ( args.Name.Contains(".resources") ) return null;

        var an = new AssemblyName(args.Name);

        var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name) ??
                       AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == an.Name);

        if ( assembly != null ) return assembly;

        var filename = an.Name + ".dll";
        var path = filename;
        if ( !File.Exists(path) )
            path = Combine(GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? string.Empty, filename);

        if ( !File.Exists(path) ) path = Combine(PluginsDirectory, filename);

        if ( !File.Exists(path) )
            if ( args.RequestingAssembly != null )
            {
                var path2 = args.RequestingAssembly.GetName().Name;
                if ( path2 != null )
                    path = Combine(PluginsDirectory, path2, filename);
            }

        if ( !File.Exists(path) ) return null;

        try
        {
            return AssemblyLoadWrapper(path);
        }
        catch ( Exception e )
        {
            Log.Error(e, $"{nameof(Plugin)}: Failed to resolve assembly or its dependency");
            return null;
        }
    }


    protected IConfigurationSection GetConfiguration()
    {
        return new ConfigurationBuilder().AddJsonFile(ConfigFile, true).Build().GetSection("PluginConfiguration");
    }


    private static void LoadPlugin(Assembly assembly)
    {
        foreach ( var type in assembly.ExportedTypes )
        {
            if ( !type.IsSubclassOf(typeof(Plugin)) ) continue;

            if ( type.IsAbstract ) continue;

            var constructor = type.GetConstructor(Type.EmptyTypes);
            var retries = 0;
            while ( true )
                try
                {
                    constructor?.Invoke(null);
                    break;
                }
                catch ( Exception e )
                {
                    Log.Error(e, "Failed to initialize plugin");

                    if ( retries < 10 )
                    {
                        // Retrying every minute for 10 minutes.
                        Thread.Sleep(60000);
                        Log.Warning("Retrying again, retry #{Retries}", retries);
                    }
                    else
                        break;

                    retries++;
                }
        }
    }


    public static void LoadPlugins()
    {
        if ( !Directory.Exists(PluginsDirectory) )
        {
            Log.Error("LoadPlugins(): PluginsDirectory {PluginsDirectory} not exists", PluginsDirectory);
            return;
        }

        var assemblies = new List<Assembly>();

        foreach ( var filename in Directory.EnumerateFiles(PluginsDirectory, "*.dll", SearchOption.TopDirectoryOnly) )
            try
            {
                assemblies.Add(AssemblyLoadWrapper(filename));
            }
            catch ( Exception ex )
            {
                Log.Error(ex, "LoadPlugins(): Plugin load '{Filename}' exception", filename);
            }

        foreach ( var assembly in assemblies ) LoadPlugin(assembly);
    }


    protected virtual bool OnMessage(object message)
    {
        return false;
    }


    protected internal virtual void OnPluginsLoaded()
    {
    }
}
