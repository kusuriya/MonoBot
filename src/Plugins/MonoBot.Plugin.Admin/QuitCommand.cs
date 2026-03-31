using Microsoft.Extensions.Logging;
using MonoBot.Abstractions;

namespace MonoBot.Plugin.Admin;

public sealed class QuitCommand(ILogger<QuitCommand> logger, CancellationTokenSource shutdownSource) : ICommand
{
    public string Trigger => "!quit";
    public bool IsAdminOnly => true;

    public async Task ExecuteAsync(string channel, string nick, string? options, IMessageSender sender, CancellationToken ct = default)
    {
        logger.LogInformation("Admin {Nick} issued !quit — shutting down", nick);
        await sender.SendRawAsync("QUIT :Bye!", ct);
        await shutdownSource.CancelAsync();
    }
}
