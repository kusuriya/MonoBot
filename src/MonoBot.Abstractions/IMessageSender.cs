namespace MonoBot.Abstractions;

/// <summary>Abstraction for sending messages back to IRC, injected into commands.</summary>
public interface IMessageSender
{
    Task SendChannelMessageAsync(string channel, string message, CancellationToken ct = default);
    Task SendRawAsync(string rawLine, CancellationToken ct = default);
}
