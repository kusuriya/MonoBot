using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MonoBot.Abstractions;

namespace MonoBot.Plugins;

public static class PluginLoader
{
    /// <summary>
    /// Scans <paramref name="pluginsPath"/> for <c>MonoBot.Plugin.*.dll</c> files, loads each
    /// assembly, finds all <see cref="IBotPlugin"/> implementations, and calls
    /// <see cref="IBotPlugin.RegisterServices"/> on them.
    /// </summary>
    public static void LoadPlugins(IServiceCollection services, string pluginsPath, ILogger logger)
    {
        var absolutePath = Path.IsPathRooted(pluginsPath)
            ? pluginsPath
            : Path.Combine(AppContext.BaseDirectory, pluginsPath);

        if (!Directory.Exists(absolutePath))
        {
            logger.LogWarning("Plugins directory not found: {Path}", absolutePath);
            return;
        }

        var dlls = Directory.GetFiles(absolutePath, "MonoBot.Plugin.*.dll");
        if (dlls.Length == 0)
        {
            logger.LogWarning("No plugin DLLs found in {Path}", absolutePath);
            return;
        }

        foreach (var dll in dlls.OrderBy(Path.GetFileName))
        {
            try
            {
                var assembly = Assembly.LoadFrom(dll);
                var pluginTypes = assembly.GetExportedTypes()
                    .Where(t => typeof(IBotPlugin).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });

                foreach (var type in pluginTypes)
                {
                    var plugin = (IBotPlugin)Activator.CreateInstance(type)!;
                    logger.LogInformation("  [{Plugin}] v{Version} — {Description}",
                        plugin.Name, plugin.Version, plugin.Description);
                    plugin.RegisterServices(services);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load plugin from {Dll}", Path.GetFileName(dll));
            }
        }
    }
}
