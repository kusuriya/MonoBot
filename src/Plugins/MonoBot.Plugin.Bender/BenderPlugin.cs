using Microsoft.Extensions.DependencyInjection;
using MonoBot.Abstractions;

namespace MonoBot.Plugin.Bender;

public sealed class BenderPlugin : IBotPlugin
{
    public string Name => "Bender";
    public string Version => "1.0.0";
    public string Description => "Serves random Bender quotes via .bender";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<ICommand, BenderCommand>();
    }
}
