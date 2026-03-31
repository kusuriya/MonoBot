using Microsoft.Extensions.Logging;
using MonoBot.Abstractions;
using MonoBot.Abstractions.Data;

namespace MonoBot.Plugin.Announcements;

public sealed class AnnouncementsCommand(IAnnouncementRepository repository, ILogger<AnnouncementsCommand> logger) : ICommand
{
    public string Trigger => ".announcements";
    public bool IsAdminOnly => false;

    public async Task ExecuteAsync(string channel, string nick, string? options, IMessageSender sender, CancellationToken ct = default)
    {
        try
        {
            var announcements = await repository.GetRecentAsync(channel, limit: 2, ct);
            if (announcements.Count == 0)
            {
                await sender.SendChannelMessageAsync(channel, "No announcements for this channel.", ct);
                return;
            }

            foreach (var a in announcements)
                await sender.SendChannelMessageAsync(channel, $"[{a.Date}] {a.Text}", ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve announcements for {Channel}", channel);
            await sender.SendChannelMessageAsync(channel, "Could not retrieve announcements.", ct);
        }
    }
}
