using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using static System.IO.Path;
using Serilog;

namespace GhostDevs.PluginEngine
{
    public abstract class Plugin: IDisposable
    {
        public static readonly List<Plugin> Plugins = new List<Plugin>();
        public static readonly List<IDBAccessPlugin> DBAPlugins = new List<IDBAccessPlugin>();
        public static readonly List<IBlockchainPlugin> BlockchainPlugins = new List<IBlockchainPlugin>();

        public static readonly string PluginsDirectory = Combine(GetDirectoryName(Assembly.GetEntryAssembly().Location), "Plugins");
        private static readonly FileSystemWatcher configWatcher;

        // New config placement, next to main backend config.
        public virtual string ConfigFile => System.IO.Path.Combine(System.IO.Path.Combine(PluginsDirectory, "../.."), $"{GetType().Assembly.GetName().Name}.json");
        public virtual string Name => GetType().Name;
        public string Path => Combine(PluginsDirectory, GetType().Assembly.ManifestModule.ScopeName);
        public virtual Version Version => GetType().Assembly.GetName().Version;

        static Plugin()
        {
            if (Directory.Exists(PluginsDirectory))
            {
                configWatcher = new FileSystemWatcher(PluginsDirectory)
                {
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.Size,
                };
                configWatcher.Changed += Plugin_Changed;
                configWatcher.Created += Plugin_Changed;
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            }
        }

        protected Plugin()
        {
            Plugins.Add(this);
            if (this is IDBAccessPlugin dbAccessPlugin) DBAPlugins.Add(dbAccessPlugin);
            if (this is IBlockchainPlugin blockchainPlugin) BlockchainPlugins.Add(blockchainPlugin);
            Configure();
        }

        protected virtual void Configure() {}
        private static Assembly AssemblyLoadWrapper(string path)
        {
            var pdbFilePath = System.IO.Path.ChangeExtension(path, "pdb");
            if (File.Exists(pdbFilePath))
            {
                return Assembly.Load(File.ReadAllBytes(path), File.ReadAllBytes(pdbFilePath));
            }
            else
            {
                return Assembly.Load(File.ReadAllBytes(path));
            }
        }

        private static void Plugin_Changed(object sender, FileSystemEventArgs e)
        {
            switch (GetExtension(e.Name))
            {
                case ".json":
                    try
                    {
                        Plugins.FirstOrDefault(p => p.ConfigFile == e.FullPath)?.Configure();
                    }
                    catch (FormatException) { }
                    break;
                case ".dll":
                    if (e.ChangeType != WatcherChangeTypes.Created) return;
                    if (GetDirectoryName(e.FullPath) != PluginsDirectory) return;
                    try
                    {
                        LoadPlugin(AssemblyLoadWrapper(e.FullPath));
                    }
                    catch {}
                    break;
            }
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (args.Name.Contains(".resources"))
                return null;

            AssemblyName an = new AssemblyName(args.Name);

            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
            if (assembly is null)
                assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == an.Name);
            if (assembly != null) return assembly;

            string filename = an.Name + ".dll";
            string path = filename;
            if (!File.Exists(path)) path = Combine(GetDirectoryName(Assembly.GetEntryAssembly().Location), filename);
            if (!File.Exists(path)) path = Combine(PluginsDirectory, filename);
            if (!File.Exists(path)) path = Combine(PluginsDirectory, args.RequestingAssembly.GetName().Name, filename);
            if (!File.Exists(path)) return null;

            try
            {
                return AssemblyLoadWrapper(path);
            }
            catch (Exception e)
            {
                Log.Error(e, $"{nameof(Plugin)}: Failed to resolve assembly or its dependency");
                return null;
            }
        }

        public virtual void Dispose()
        {
        }

        protected IConfigurationSection GetConfiguration()
        {
            return new ConfigurationBuilder().AddJsonFile(ConfigFile, optional: true).Build().GetSection("PluginConfiguration");
        }

        private static void LoadPlugin(Assembly assembly)
        {
            foreach (Type type in assembly.ExportedTypes)
            {
                if (!type.IsSubclassOf(typeof(Plugin))) continue;
                if (type.IsAbstract) continue;

                ConstructorInfo constructor = type.GetConstructor(Type.EmptyTypes);
                var retries = 0;
                while (true)
                {
                    try
                    {
                        constructor?.Invoke(null);
                        break;
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, $"Failed to initialize plugin");

                        if (retries < 10)
                        {
                            // Retrying every minute for 10 minutes.
                            System.Threading.Thread.Sleep(60000);
                            Log.Warning($"Retrying again, retry #{retries}");
                        }
                        else
                        {
                            break;
                        }

                        retries++;
                    }
                }
            }
        }

        public static void LoadPlugins()
        {
            if (!Directory.Exists(PluginsDirectory))
            {
                Log.Error($"LoadPlugins(): PluginsDirectory {PluginsDirectory} not exists");
                return;
            }

            List<Assembly> assemblies = new List<Assembly>();

            foreach (string filename in Directory.EnumerateFiles(PluginsDirectory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    assemblies.Add(AssemblyLoadWrapper(filename));
                }
                catch(Exception ex)
                {
                    Log.Error(ex, $"LoadPlugins(): Plugin load '{filename}' exception");
                }
            }

            foreach (Assembly assembly in assemblies)
            {
                LoadPlugin(assembly);
            }
        }

        protected virtual bool OnMessage(object message)
        {
            return false;
        }

        internal protected virtual void OnPluginsLoaded() {}
    }
}
