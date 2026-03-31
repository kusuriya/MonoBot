using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using MonoBot.Abstractions;

namespace MonoBot.IRC;

/// <summary>
/// Manages one DCC CHAT admin session.
/// The caller connects a TCP socket and passes its streams here; this class owns auth and command routing.
/// Takes <see cref="TextReader"/>/<see cref="TextWriter"/> so it can be tested without a real socket.
/// </summary>
public sealed class DccSession
{
    private readonly string _adminPassword;
    private readonly IReadOnlyList<ICommand> _adminCommands;
    private readonly ILogger<DccSession> _logger;

    public DccSession(
        string adminPassword,
        IEnumerable<ICommand> allCommands,
        ILogger<DccSession> logger)
    {
        _adminPassword = adminPassword;
        _adminCommands = allCommands.Where(c => c.IsAdminOnly).ToList();
        _logger = logger;
    }

    /// <summary>
    /// Runs the auth + command loop until the client disconnects or <paramref name="ct"/> is cancelled.
    /// </summary>
    /// <param name="reader">Reads lines from the DCC client.</param>
    /// <param name="writer">Writes lines back to the DCC client.</param>
    /// <param name="ircSender">Used to send raw IRC commands (JOIN, PART, QUIT…).</param>
    public async Task RunAsync(TextReader reader, TextWriter writer, IMessageSender ircSender, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_adminPassword))
        {
            await WriteLineAsync(writer, "ERROR: Admin password is not configured. DCC admin is disabled.", ct);
            _logger.LogWarning("DCC session rejected — AdminPassword is not set");
            return;
        }

        var dccSender = new DccDirectSender(writer);
        // Composite: raw IRC commands go to the IRC connection; text responses go back to the DCC client.
        var compositeSender = new DccCompositeMessageSender(ircSender, dccSender);

        await WriteLineAsync(writer, "MonoBot DCC Admin — send AUTH <password> to authenticate.", ct);

        var authenticated = false;

        while (!ct.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (line is null)
            {
                _logger.LogDebug("DCC client disconnected");
                break;
            }

            line = line.Trim();
            if (line.Length == 0)
                continue;

            if (!authenticated)
            {
                authenticated = await HandleUnauthenticatedAsync(writer, line, ct);
                continue;
            }

            await HandleCommandAsync(writer, compositeSender, line, ct);
        }
    }

    // Returns true if this line successfully authenticated the session.
    private async Task<bool> HandleUnauthenticatedAsync(TextWriter writer, string line, CancellationToken ct)
    {
        if (line.StartsWith("AUTH ", StringComparison.OrdinalIgnoreCase))
        {
            var password = line[5..]; // preserve original casing for timing-safe compare
            if (PasswordMatches(password))
            {
                _logger.LogInformation("DCC admin authenticated");
                await WriteLineAsync(writer, "Authenticated. Type HELP for available commands.", ct);
                return true;
            }
            else
            {
                _logger.LogWarning("DCC authentication attempt failed");
                await WriteLineAsync(writer, "Authentication failed.", ct);
                return false;
            }
        }

        await WriteLineAsync(writer, "Not authenticated. Send: AUTH <password>", ct);
        return false;
    }

    private async Task HandleCommandAsync(TextWriter writer, IMessageSender sender, string line, CancellationToken ct)
    {
        var parts = line.Split(' ', 2);
        var trigger = parts[0];
        var options = parts.Length > 1 ? parts[1] : null;

        // Built-in HELP — lists registered admin command triggers.
        if (trigger.Equals("HELP", StringComparison.OrdinalIgnoreCase))
        {
            var triggers = string.Join("  ", _adminCommands.Select(c => c.Trigger).OrderBy(t => t));
            await WriteLineAsync(writer, $"Admin commands: {triggers}", ct);
            return;
        }

        var cmd = _adminCommands.FirstOrDefault(c =>
            c.Trigger.Equals(trigger, StringComparison.OrdinalIgnoreCase));

        if (cmd is null)
        {
            await WriteLineAsync(writer, $"Unknown command: {trigger}  (type HELP for list)", ct);
            return;
        }

        try
        {
            // channel is empty — DCC has no "current channel"; commands that need one take it as an argument.
            await cmd.ExecuteAsync(string.Empty, "dcc-admin", options, sender, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing DCC admin command {Trigger}", trigger);
            await WriteLineAsync(writer, $"Error executing {trigger}: {ex.Message}", ct);
        }
    }

    /// <summary>
    /// Timing-safe password comparison. Prevents timing oracle attacks where an
    /// attacker can guess the password one character at a time by measuring response latency.
    /// </summary>
    private bool PasswordMatches(string candidate)
    {
        var expected = Encoding.UTF8.GetBytes(_adminPassword);
        var actual = Encoding.UTF8.GetBytes(candidate);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static Task WriteLineAsync(TextWriter writer, string line, CancellationToken ct)
        => writer.WriteLineAsync(line.AsMemory(), ct);
}

/// <summary>Sends text responses directly to the DCC client.</summary>
internal sealed class DccDirectSender(TextWriter writer) : IMessageSender
{
    // In a DCC session there are no channels — just write the message text.
    public Task SendChannelMessageAsync(string channel, string message, CancellationToken ct = default)
        => writer.WriteLineAsync(message.AsMemory(), ct);

    // Raw IRC protocol lines are meaningless on a DCC socket; echo them for transparency.
    public Task SendRawAsync(string rawLine, CancellationToken ct = default)
        => writer.WriteLineAsync(rawLine.AsMemory(), ct);
}

/// <summary>
/// Composite sender used inside an authenticated DCC session.
/// Raw IRC commands (JOIN, PART, QUIT…) are forwarded to the live IRC connection.
/// Text responses are written back to the DCC client.
/// </summary>
internal sealed class DccCompositeMessageSender(IMessageSender ircSender, IMessageSender dccSender) : IMessageSender
{
    // Responses to the admin go over DCC.
    public Task SendChannelMessageAsync(string channel, string message, CancellationToken ct = default)
        => dccSender.SendChannelMessageAsync(channel, message, ct);

    // IRC-level commands (JOIN, PART, QUIT…) go to the IRC server.
    public Task SendRawAsync(string rawLine, CancellationToken ct = default)
        => ircSender.SendRawAsync(rawLine, ct);
}
