using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SEE_INSADE.Core.Plugins
{
    public sealed class PluginManager
    {
        public PluginManager(PluginContext context)
        {
            Context = context;
        }

        public PluginContext Context { get; }
        public ObservableCollection<IScannerPlugin> Plugins { get; } = new();

        public void Register(IScannerPlugin plugin)
        {
            if (Plugins.Any(existing => existing.Id.Equals(plugin.Id, StringComparison.OrdinalIgnoreCase)))
                return;

            Plugins.Add(plugin);
        }

        public void LoadExternalPlugins()
        {
            string pluginDirectory = Path.Combine(AppContext.BaseDirectory, "Plugins");
            Directory.CreateDirectory(pluginDirectory);

            foreach (string pluginPath in Directory.EnumerateFiles(pluginDirectory, "*.dll"))
            {
                TryLoadPluginAssembly(pluginPath);
            }
        }

        private void TryLoadPluginAssembly(string pluginPath)
        {
            try
            {
                Assembly assembly = Assembly.LoadFrom(pluginPath);
                Type pluginType = typeof(IScannerPlugin);

                foreach (Type type in assembly.GetTypes().Where(type =>
                             pluginType.IsAssignableFrom(type) &&
                             type is { IsAbstract: false, IsInterface: false } &&
                             type.GetConstructor(Type.EmptyTypes) != null))
                {
                    if (Activator.CreateInstance(type) is IScannerPlugin plugin)
                        Register(plugin);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Plugin load failed for {pluginPath}: {ex.Message}");
            }
        }
    }
}
