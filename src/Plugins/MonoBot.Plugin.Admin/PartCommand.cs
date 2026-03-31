using MonoBot.Abstractions;

namespace MonoBot.Plugin.Admin;

public sealed class PartCommand : ICommand
{
    public string Trigger => "!part";
    public bool IsAdminOnly => true;

    public Task ExecuteAsync(string channel, string nick, string? options, IMessageSender sender, CancellationToken ct = default)
    {
        // When called from DCC, channel is empty — require an explicit target.
        var target = string.IsNullOrWhiteSpace(options) ? channel : options.Trim();
        if (string.IsNullOrWhiteSpace(target))
            return sender.SendChannelMessageAsync(channel, "Usage: !part #channel", ct);

        return sender.SendRawAsync($"PART {target}", ct);
    }
}
