using Microsoft.Extensions.Logging;
using MonoBot.Abstractions;
using MonoBot.Abstractions.Data;

namespace MonoBot.Plugin.Bender;

public sealed class BenderCommand(IBenderRepository repository, ILogger<BenderCommand> logger) : ICommand
{
    public string Trigger => ".bender";
    public bool IsAdminOnly => false;

    public async Task ExecuteAsync(string channel, string nick, string? options, IMessageSender sender, CancellationToken ct = default)
    {
        try
        {
            var quote = await repository.GetRandomQuoteAsync(ct);
            var message = string.IsNullOrEmpty(quote)
                ? "Bender has nothing to say right now."
                : quote;
            await sender.SendChannelMessageAsync(channel, message, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve Bender quote");
            await sender.SendChannelMessageAsync(channel, "Bender is temporarily unavailable.", ct);
        }
    }
}
