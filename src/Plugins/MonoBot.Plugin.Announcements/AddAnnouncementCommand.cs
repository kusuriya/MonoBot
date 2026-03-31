using Microsoft.Extensions.Logging;
using MonoBot.Abstractions;
using MonoBot.Abstractions.Data;

namespace MonoBot.Plugin.Announcements;

public sealed class AddAnnouncementCommand(IAnnouncementRepository repository, ILogger<AddAnnouncementCommand> logger) : ICommand
{
    private const int MaxLength = 500;

    public string Trigger => ".add-announcement";
    public bool IsAdminOnly => false;

    public async Task ExecuteAsync(string channel, string nick, string? options, IMessageSender sender, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options))
        {
            await sender.SendChannelMessageAsync(channel, "Usage: .add-announcement <text>", ct);
            return;
        }

        if (options.Length > MaxLength)
        {
            await sender.SendChannelMessageAsync(channel, $"Announcement too long (max {MaxLength} characters).", ct);
            return;
        }

        try
        {
            await repository.AddAsync(channel, options.Trim(), ct);
            await sender.SendChannelMessageAsync(channel, "Announcement added.", ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add announcement for {Channel}", channel);
            await sender.SendChannelMessageAsync(channel, "Could not save announcement.", ct);
        }
    }
}
