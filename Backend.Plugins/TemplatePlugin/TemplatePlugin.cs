using Backend;
using Backend.PluginEngine;

namespace TestPlugin;

public class TestPlugin : Plugin, IDBAccessPlugin
{
    public override string Name => "TestPlugin";


    public void Startup()
    {
    }


    public void Shutdown()
    {
    }


    protected override void Configure()
    {
        Settings.Load(GetConfiguration());
    }
}
