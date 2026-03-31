using Microsoft.Extensions.DependencyInjection;
using MonoBot.Abstractions;

namespace MonoBot.Plugin.Core;

public sealed class CorePlugin : IBotPlugin
{
    public string Name => "Core";
    public string Version => "1.0.0";
    public string Description => "Core commands: .version, .help (auto-lists all loaded commands)";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<ICommand, VersionCommand>();
        services.AddSingleton<ICommand, HelpCommand>();
    }
}
