using SEE_INSADE.Core.Plugins;
using SEE_INSADE.UI.Plugins;

namespace SEE_INSADE.Plugins.NuctechImg
{
    public sealed class NuctechImgPlugin : IScannerPlugin
    {
        public string Id => "see-insade.nuctech-img-viewer";
        public string Name => "Nuctech IMG Viewer";
        public string Description => "Open Nuctech CX/XT .img raw scans and apply SEE INSADE filters";

        public void Execute(PluginContext context)
        {
            var window = new NuctechImgPluginWindow
            {
                Owner = context.Owner
            };

            window.Show();
        }
    }
}
