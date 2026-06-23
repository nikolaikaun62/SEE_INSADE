using SEE_INSADE.Core.Plugins;
using SEE_INSADE.UI.Plugins;

namespace SEE_INSADE.Plugins.Configuration
{
    public sealed class ConfigurationPlugin : IScannerPlugin
    {
        public string Id => "see-insade.configuration";
        public string Name => "Configuration";
        public string Description => "Deep system, plugin and user access configuration";

        public void Execute(PluginContext context)
        {
            var window = new ConfigurationPluginWindow(context.ScanService)
            {
                Owner = context.Owner
            };

            window.Show();
        }
    }
}
