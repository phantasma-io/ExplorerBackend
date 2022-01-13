namespace GhostDevs.PluginEngine;

public interface IDBAccessPlugin
{
    // Starts self contained plugins
    void Startup();


    // Stop self contained plugins and give them a hint to shut down gracefully.
    void Shutdown();
}
