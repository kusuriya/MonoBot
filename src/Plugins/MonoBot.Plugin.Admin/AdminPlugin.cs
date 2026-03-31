using Microsoft.Extensions.DependencyInjection;
using MonoBot.Abstractions;

namespace MonoBot.Plugin.Admin;

public sealed class AdminPlugin : IBotPlugin
{
    public string Name => "Admin";
    public string Version => "1.0.0";
    public string Description => "Admin commands: !join, !part, !debug, !quit";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<ICommand, JoinCommand>();
        services.AddSingleton<ICommand, PartCommand>();
        services.AddSingleton<ICommand, DebugCommand>();
        services.AddSingleton<ICommand, QuitCommand>();
    }
}
