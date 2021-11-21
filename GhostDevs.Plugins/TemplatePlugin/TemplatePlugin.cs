using GhostDevs.PluginEngine;
using GhostDevs;

namespace TestPlugin
{
    public class TestPlugin: Plugin, IDBAccessPlugin
    {

        public override string Name => "TestPlugin";

        public TestPlugin()
        {
            // constructor stuff
        }

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        public void Startup() 
        {
        }
        public void Shutdown()
        {
        }
    }
}
