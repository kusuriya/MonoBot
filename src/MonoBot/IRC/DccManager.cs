using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using MonoBot.Abstractions;
using MonoBot.Configuration;

namespace MonoBot.IRC;

/// <summary>
/// Accepts incoming DCC CHAT connections and runs each as a <see cref="DccSession"/>.
/// The caller (IrcClient) passes itself as <c>ircSender</c> when invoking <see cref="AcceptAsync"/>,
/// which avoids a circular DI dependency.
/// </summary>
public sealed class DccManager
{
    private readonly BotConfig _config;
    private readonly IEnumerable<ICommand> _allCommands;
    private readonly ILogger<DccManager> _logger;
    private readonly ILogger<DccSession> _sessionLogger;

    public DccManager(
        BotConfig config,
        IEnumerable<ICommand> allCommands,
        ILogger<DccManager> logger,
        ILogger<DccSession> sessionLogger)
    {
        _config = config;
        _allCommands = allCommands;
        _logger = logger;
        _sessionLogger = sessionLogger;
    }

    /// <summary>
    /// Connects outward to the DCC client at <paramref name="host"/>:<paramref name="port"/>
    /// and runs a new <see cref="DccSession"/> as a background task.
    /// </summary>
    /// <param name="ircSender">
    /// The live IRC connection — passed here rather than injected at construction to break the
    /// circular dependency between <see cref="IrcClient"/> and <see cref="DccManager"/>.
    /// </param>
    public Task AcceptAsync(string host, int port, IMessageSender ircSender, CancellationToken ct)
    {
        // Fire-and-forget: each session runs independently and logs its own errors.
        _ = RunSessionAsync(host, port, ircSender, ct);
        return Task.CompletedTask;
    }

    private async Task RunSessionAsync(string host, int port, IMessageSender ircSender, CancellationToken ct)
    {
        _logger.LogInformation("Connecting DCC session to {Host}:{Port}", host, port);
        TcpClient? tcp = null;
        try
        {
            tcp = new TcpClient();
            await tcp.ConnectAsync(host, port, ct);

            using var stream = tcp.GetStream();
            using var reader = new StreamReader(stream);
            await using var writer = new StreamWriter(stream) { AutoFlush = true };

            var session = new DccSession(_config.AdminPassword, _allCommands, _sessionLogger);
            await session.RunAsync(reader, writer, ircSender, ct);
        }
        catch (OperationCanceledException) { /* host shutting down */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DCC session to {Host}:{Port} ended with error", host, port);
        }
        finally
        {
            tcp?.Dispose();
            _logger.LogDebug("DCC session to {Host}:{Port} closed", host, port);
        }
    }
}
