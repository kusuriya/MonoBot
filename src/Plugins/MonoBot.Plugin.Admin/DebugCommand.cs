using Microsoft.Extensions.Logging;
using MonoBot.Abstractions;

namespace MonoBot.Plugin.Admin;

public sealed class DebugCommand(ILogger<DebugCommand> logger) : ICommand
{
    public string Trigger => "!debug";
    public bool IsAdminOnly => true;

    public Task ExecuteAsync(string channel, string nick, string? options, IMessageSender sender, CancellationToken ct = default)
    {
        var mode = options?.Trim().ToLowerInvariant();
        var message = mode switch
        {
            "on" => "Debug logging enabled (restart required to take full effect).",
            "off" => "Debug logging disabled (restart required to take full effect).",
            _ => "Usage: !debug on | !debug off"
        };

        logger.LogInformation("Admin {Nick} toggled debug: {Mode}", nick, mode);
        return sender.SendChannelMessageAsync(channel, message, ct);
    }
}
