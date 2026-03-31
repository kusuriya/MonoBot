using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using MonoBot.Abstractions;
using MonoBot.Commands;
using MonoBot.Configuration;

namespace MonoBot.IRC;

/// <summary>Tracks where we are in the IRCv3 capability / SASL negotiation.</summary>
internal enum SaslState
{
    /// <summary>SASL is disabled — send NICK/USER immediately.</summary>
    Disabled,
    /// <summary>Sent <c>CAP LS 302</c>, waiting for the server's capability list.</summary>
    CapLsSent,
    /// <summary>Sent <c>CAP REQ :sasl</c>, waiting for ACK or NAK.</summary>
    CapReqSent,
    /// <summary>Sent <c>AUTHENTICATE PLAIN</c>, waiting for <c>AUTHENTICATE +</c>.</summary>
    AuthenticateSent,
    /// <summary>Sent credentials, waiting for 903 / 904 / 905.</summary>
    CredentialsSent,
    /// <summary>Negotiation complete (success or fallback) — NICK/USER already sent.</summary>
    Done,
}

public sealed class IrcClient : IMessageSender
{
    private readonly BotConfig _config;
    private readonly CommandRouter _router;
    private readonly DccManager _dccManager;
    private readonly ILogger<IrcClient> _logger;

    private StreamWriter? _writer;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>Accumulates multi-line <c>CAP LS *</c> responses until the final line.</summary>
    private readonly HashSet<string> _advertisedCaps = new(StringComparer.OrdinalIgnoreCase);
    private SaslState _saslState;

    public IrcClient(BotConfig config, CommandRouter router, DccManager dccManager, ILogger<IrcClient> logger)
    {
        _config = config;
        _router = router;
        _dccManager = dccManager;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var backoff = TimeSpan.FromSeconds(5);
        const int maxBackoffSeconds = 120;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ConnectAndRunAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IRC connection lost. Reconnecting in {Backoff}s", backoff.TotalSeconds);
            }

            if (!ct.IsCancellationRequested)
            {
                await Task.Delay(backoff, ct).ConfigureAwait(false);
                backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, maxBackoffSeconds));
            }
        }

        _logger.LogInformation("IrcClient stopped.");
    }

    private async Task ConnectAndRunAsync(CancellationToken ct)
    {
        // Reset negotiation state for each (re)connect.
        _advertisedCaps.Clear();
        _saslState = _config.Sasl.Enabled ? SaslState.CapLsSent : SaslState.Disabled;

        using var tcp = new TcpClient();
        _logger.LogInformation("Connecting to {Server}:{Port}", _config.Server, _config.Port);
        await tcp.ConnectAsync(_config.Server, _config.Port, ct);
        _logger.LogInformation("Connected.");

        Stream stream = tcp.GetStream();
        if (_config.UseTls)
        {
            var sslStream = new SslStream(stream, leaveInnerStreamOpen: false);
            await sslStream.AuthenticateAsClientAsync(
                new SslClientAuthenticationOptions { TargetHost = _config.Server }, ct);
            stream = sslStream;
            _logger.LogInformation("TLS handshake complete ({Protocol}).", sslStream.SslProtocol);
        }

        using var ownedStream = stream;
        using var reader = new StreamReader(ownedStream);
        await using var writer = new StreamWriter(ownedStream) { AutoFlush = true };

        _writeLock.Wait(ct);
        _writer = writer;
        _writeLock.Release();

        try
        {
            await StartConnectionAsync(ct);
            await ReadLoopAsync(reader, ct);
        }
        finally
        {
            await _writeLock.WaitAsync(CancellationToken.None);
            _writer = null;
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Kicks off the IRC handshake.
    /// If SASL is enabled, send <c>CAP LS 302</c> first and defer NICK/USER until negotiation completes.
    /// Otherwise send NICK/USER immediately.
    /// </summary>
    private async Task StartConnectionAsync(CancellationToken ct)
    {
        if (_saslState == SaslState.CapLsSent)
        {
            await SendRawAsync("CAP LS 302", ct);
        }
        else
        {
            await SendNickUserAsync(ct);
        }
    }

    /// <summary>Sends NICK, USER, and (if configured) NickServ IDENTIFY.</summary>
    private async Task SendNickUserAsync(CancellationToken ct)
    {
        await SendRawAsync($"NICK {_config.Nick}", ct);
        await SendRawAsync($"USER {_config.Nick} 0 * :{_config.Name}", ct);

        if (_config.UseNickServ && !string.IsNullOrEmpty(_config.NickServ.Password))
        {
            await SendRawAsync(
                $"PRIVMSG NickServ :IDENTIFY {_config.NickServ.Username} {_config.NickServ.Password}", ct);
        }
    }

    private async Task ReadLoopAsync(StreamReader reader, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null)
            {
                _logger.LogWarning("Server closed connection.");
                return;
            }

            _logger.LogDebug(">> {Line}", line);

            var message = IrcMessageParser.Parse(line);
            if (message is null)
                continue;

            await HandleMessageAsync(message, ct);
        }
    }

    private async Task HandleMessageAsync(IrcMessage message, CancellationToken ct)
    {
        switch (message.Command)
        {
            case "PING":
                await SendRawAsync($"PONG :{message.Trail ?? message.Parameters.FirstOrDefault()}", ct);
                break;

            case "001": // RPL_WELCOME — server accepted us, now join channels
                foreach (var channel in _config.Channels)
                    await SendRawAsync($"JOIN {channel}", ct);
                break;

            case "CAP":
                await HandleCapAsync(message, ct);
                break;

            case "AUTHENTICATE":
                await HandleAuthenticateAsync(message, ct);
                break;

            case "900": // RPL_LOGGEDIN — informational, no action needed
                _logger.LogInformation("SASL: logged in as {Account}", message.Trail);
                break;

            case "903": // RPL_SASLSUCCESS
                _logger.LogInformation("SASL authentication succeeded.");
                _saslState = SaslState.Done;
                await SendRawAsync("CAP END", ct);
                await SendNickUserAsync(ct);
                break;

            case "904": // ERR_SASLFAIL
            case "905": // ERR_SASLTOOLONG
                _logger.LogError("SASL authentication failed (numeric {Code}). Continuing without SASL.", message.Command);
                _saslState = SaslState.Done;
                await SendRawAsync("CAP END", ct);
                await SendNickUserAsync(ct);
                break;

            case "906": // ERR_SASLABORTED
            case "907": // ERR_SASLALREADYAUTHED
                _logger.LogWarning("SASL numeric {Code} received; finalising handshake.", message.Command);
                if (_saslState != SaslState.Done)
                {
                    _saslState = SaslState.Done;
                    await SendRawAsync("CAP END", ct);
                    await SendNickUserAsync(ct);
                }
                break;

            case "PRIVMSG":
                await HandlePrivmsgAsync(message, ct);
                break;

            default:
                _logger.LogDebug("Unhandled command: {Command}", message.Command);
                break;
        }
    }

    // ── CAP handling ─────────────────────────────────────────────────────────

    private async Task HandleCapAsync(IrcMessage message, CancellationToken ct)
    {
        // Parameters[0] = target nick (or *), Parameters[1] = sub-command
        var subCommand = message.Parameters.Length >= 2
            ? message.Parameters[1]
            : message.Parameters.FirstOrDefault();

        if (subCommand is null)
            return;

        switch (subCommand.ToUpperInvariant())
        {
            case "LS":
                await HandleCapLsAsync(message, ct);
                break;

            case "ACK":
                await HandleCapAckAsync(message, ct);
                break;

            case "NAK":
                _logger.LogWarning("Server rejected CAP REQ :sasl. Continuing without SASL.");
                _saslState = SaslState.Done;
                await SendRawAsync("CAP END", ct);
                await SendNickUserAsync(ct);
                break;
        }
    }

    private async Task HandleCapLsAsync(IrcMessage message, CancellationToken ct)
    {
        if (_saslState != SaslState.CapLsSent)
            return;

        // Multi-line CAP LS: Parameters[2] == "*" means more lines follow.
        // The cap list is in Trail.
        var isMultiLine = message.Parameters.Length >= 3 &&
                          message.Parameters[2].Equals("*", StringComparison.Ordinal);

        if (message.Trail is not null)
        {
            foreach (var cap in message.Trail.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                // Caps may carry values ("sasl=PLAIN,EXTERNAL") — keep only the name.
                var capName = cap.Contains('=') ? cap[..cap.IndexOf('=')] : cap;
                _advertisedCaps.Add(capName);
            }
        }

        if (isMultiLine)
            return; // wait for the final LS line

        // Final CAP LS line received.
        if (_advertisedCaps.Contains("sasl"))
        {
            _saslState = SaslState.CapReqSent;
            await SendRawAsync("CAP REQ :sasl", ct);
        }
        else
        {
            _logger.LogWarning("Server does not advertise SASL capability. Continuing without SASL.");
            _saslState = SaslState.Done;
            await SendRawAsync("CAP END", ct);
            await SendNickUserAsync(ct);
        }
    }

    private async Task HandleCapAckAsync(IrcMessage message, CancellationToken ct)
    {
        if (_saslState != SaslState.CapReqSent)
            return;

        var acked = (message.Trail ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (acked.Any(c => c.Equals("sasl", StringComparison.OrdinalIgnoreCase)))
        {
            _saslState = SaslState.AuthenticateSent;
            await SendRawAsync($"AUTHENTICATE {_config.Sasl.Mechanism}", ct);
        }
        else
        {
            _logger.LogWarning("SASL cap ACK did not include 'sasl'. Continuing without SASL.");
            _saslState = SaslState.Done;
            await SendRawAsync("CAP END", ct);
            await SendNickUserAsync(ct);
        }
    }

    // ── AUTHENTICATE handling ─────────────────────────────────────────────────

    private async Task HandleAuthenticateAsync(IrcMessage message, CancellationToken ct)
    {
        if (_saslState != SaslState.AuthenticateSent)
            return;

        // Server sends "AUTHENTICATE +" to prompt for credentials.
        var payload = message.Parameters.FirstOrDefault() ?? message.Trail;
        if (payload != "+")
        {
            _logger.LogWarning("Unexpected AUTHENTICATE payload: {Payload}", payload);
            return;
        }

        _saslState = SaslState.CredentialsSent;

        var base64 = SaslHelper.BuildPlainCredentials(_config.Sasl.Username, _config.Sasl.Password);
        var chunks = SaslHelper.ChunkCredentials(base64);

        foreach (var chunk in chunks)
            await SendRawAsync($"AUTHENTICATE {chunk}", ct);
    }

    // ── PRIVMSG handling ──────────────────────────────────────────────────────

    private async Task HandlePrivmsgAsync(IrcMessage message, CancellationToken ct)
    {
        var target = message.Parameters.FirstOrDefault();
        var text = message.Trail;
        var rawNick = message.Prefix;

        if (target is null || text is null || rawNick is null)
            return;

        // CTCP messages are wrapped in ASCII 0x01 (sent as a direct message to the bot).
        if (text.StartsWith('\x01'))
        {
            var ctcp = text.Trim('\x01');
            await HandleCtcpAsync(rawNick, ctcp, ct);
            return;
        }

        // Regular channel message — route non-admin commands.
        if (target.StartsWith('#'))
            await _router.RouteAsync(rawNick, target, text, this, ct);
    }

    private async Task HandleCtcpAsync(string rawNick, string ctcp, CancellationToken ct)
    {
        // DCC CHAT request format: "DCC CHAT chat <ip_as_uint32_decimal> <port>"
        // The IP is encoded as a big-endian 32-bit unsigned integer in decimal.
        if (ctcp.StartsWith("DCC CHAT chat ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = ctcp.Split(' ');
            if (parts.Length >= 5
                && uint.TryParse(parts[3], out var ipUint)
                && int.TryParse(parts[4], out var port)
                && port is > 0 and <= 65535)
            {
                var ipBytes = new byte[]
                {
                    (byte)(ipUint >> 24),
                    (byte)(ipUint >> 16),
                    (byte)(ipUint >> 8),
                    (byte)ipUint
                };
                var ipAddress = new IPAddress(ipBytes);
                _logger.LogInformation("DCC CHAT request from {Nick} at {IP}:{Port}", rawNick, ipAddress, port);
                // Pass 'this' as the IRC sender — DccManager has no constructor dep on IrcClient.
                await _dccManager.AcceptAsync(ipAddress.ToString(), port, this, ct);
            }
            else
            {
                _logger.LogWarning("Malformed DCC CHAT from {Nick}: {Ctcp}", rawNick, ctcp);
            }
            return;
        }

        _logger.LogDebug("Unhandled CTCP from {Nick}: {Ctcp}", rawNick, ctcp);
    }

    // ── IMessageSender implementation ──────────────────────────────────────

    public Task SendChannelMessageAsync(string channel, string message, CancellationToken ct = default)
        => SendRawAsync($"PRIVMSG {channel} :{message}", ct);

    public async Task SendRawAsync(string rawLine, CancellationToken ct = default)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            if (_writer is null)
            {
                _logger.LogWarning("Attempted to send while disconnected: {Line}", rawLine);
                return;
            }
            _logger.LogDebug("<< {Line}", rawLine);
            await _writer.WriteLineAsync(rawLine.AsMemory(), ct);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
