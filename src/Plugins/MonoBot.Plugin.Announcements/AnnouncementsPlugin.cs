using Microsoft.Extensions.DependencyInjection;
using MonoBot.Abstractions;

namespace MonoBot.Plugin.Announcements;

public sealed class AnnouncementsPlugin : IBotPlugin
{
    public string Name => "Announcements";
    public string Version => "1.0.0";
    public string Description => "Channel announcements via .announcements and .add-announcement";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<ICommand, AnnouncementsCommand>();
        services.AddSingleton<ICommand, AddAnnouncementCommand>();
    }
}
