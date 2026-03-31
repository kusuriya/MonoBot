using MonoBot.Abstractions;

namespace MonoBot.Plugin.Admin;

public sealed class JoinCommand : ICommand
{
    public string Trigger => "!join";
    public bool IsAdminOnly => true;

    public Task ExecuteAsync(string channel, string nick, string? options, IMessageSender sender, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options))
            return sender.SendChannelMessageAsync(channel, "Usage: !join #channel", ct);

        return sender.SendRawAsync($"JOIN {options.Trim()}", ct);
    }
}
