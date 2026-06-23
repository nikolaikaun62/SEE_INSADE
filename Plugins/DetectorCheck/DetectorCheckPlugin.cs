using SEE_INSADE.Core.Plugins;
using SEE_INSADE.UI.Plugins;

namespace SEE_INSADE.Plugins.DetectorCheck
{
    public sealed class DetectorCheckPlugin : IScannerPlugin
    {
        public string Id => "see-insade.detector-check";
        public string Name => "Detector Check";
        public string Description => "Live horizontal detector-array monitor";

        public void Execute(PluginContext context)
        {
            var window = new DetectorCheckPluginWindow(context.ScanService)
            {
                Owner = context.Owner
            };

            window.Show();
        }
    }
}
