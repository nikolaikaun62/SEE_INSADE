using System.Windows;

namespace SEE_INSADE.Core.Plugins
{
    public interface IScannerPlugin
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        void Execute(PluginContext context);
    }

    public sealed class PluginContext
    {
        public PluginContext(Services.Scanning.ScanService scanService, Window owner)
        {
            ScanService = scanService;
            Owner = owner;
        }

        public Services.Scanning.ScanService ScanService { get; }
        public Window Owner { get; }
    }
}
