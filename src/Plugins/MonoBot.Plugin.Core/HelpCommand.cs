using MonoBot.Abstractions;

namespace MonoBot.Plugin.Core;

/// <summary>
/// Lists all registered commands dynamically — automatically reflects whatever plugins are loaded.
/// </summary>
public sealed class HelpCommand(IEnumerable<ICommand> allCommands) : ICommand
{
    public string Trigger => ".help";
    public bool IsAdminOnly => false;

    public Task ExecuteAsync(string channel, string nick, string? options, IMessageSender sender, CancellationToken ct = default)
    {
        var userCommands = allCommands
            .Where(c => !c.IsAdminOnly && c.Trigger != Trigger)
            .Select(c => c.Trigger)
            .OrderBy(t => t);

        var adminCommands = allCommands
            .Where(c => c.IsAdminOnly)
            .Select(c => c.Trigger)
            .OrderBy(t => t);

        var message = $"Commands: {string.Join(" | ", userCommands)}  Admin: {string.Join(" | ", adminCommands)}";
        return sender.SendChannelMessageAsync(channel, message, ct);
    }
}
