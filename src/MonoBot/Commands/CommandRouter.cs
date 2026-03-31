using Microsoft.Extensions.Logging;
using MonoBot.Abstractions;

namespace MonoBot.Commands;

public sealed class CommandRouter
{
    private readonly Dictionary<string, ICommand> _commands;
    private readonly ILogger<CommandRouter> _logger;

    public CommandRouter(IEnumerable<ICommand> commands, ILogger<CommandRouter> logger)
    {
        // Admin commands are exclusively handled via DCC — exclude them here so
        // they cannot be triggered from a channel even by the correct nick.
        _commands = commands
            .Where(c => !c.IsAdminOnly)
            .ToDictionary(c => c.Trigger, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    public async Task RouteAsync(string rawNick, string channel, string message, IMessageSender sender, CancellationToken ct = default)
    {
        var parts = message.Split(' ', 2);
        var trigger = parts[0];
        var options = parts.Length > 1 ? parts[1] : null;

        if (!_commands.TryGetValue(trigger, out var cmd))
            return;

        _logger.LogDebug("{Nick} → {Trigger} in {Channel}", rawNick, trigger, channel);
        await cmd.ExecuteAsync(channel, rawNick, options, sender, ct);
    }
}
