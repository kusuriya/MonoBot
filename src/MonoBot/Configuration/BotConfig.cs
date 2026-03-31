namespace MonoBot.Configuration;

public sealed class BotConfig
{
    public string Server { get; init; } = "irc.libera.chat";
    public int Port { get; init; } = 6697;
    /// <summary>
    /// Use TLS when connecting to the IRC server. Defaults to <c>true</c>.
    /// Set <c>MONOBOT_UseTls=false</c> only when connecting to a plaintext server.
    /// </summary>
    public bool UseTls { get; init; } = true;
    public string Nick { get; init; } = "monobot";
    public string Name { get; init; } = "MonoBot";
    public string[] Admins { get; init; } = [];
    public string[] Channels { get; init; } = [];
    public bool UseNickServ { get; init; } = false;
    public NickServConfig NickServ { get; init; } = new();
    public bool Debug { get; init; } = false;
    public DatabaseConfig Database { get; init; } = new();

    /// <summary>
    /// Password required to authenticate over a DCC CHAT admin session.
    /// Must be set via <c>MONOBOT_AdminPassword</c> environment variable — never in a committed file.
    /// DCC admin is disabled when this is empty.
    /// </summary>
    public string AdminPassword { get; init; } = string.Empty;

    /// <summary>
    /// Directory containing plugin DLLs. Relative paths are resolved against the application base directory.
    /// Override with <c>MONOBOT_PluginsPath</c> environment variable (e.g. <c>/app/plugins</c> in Docker).
    /// </summary>
    public string PluginsPath { get; init; } = "plugins";

    /// <summary>
    /// IRCv3 SASL authentication settings. When enabled, the bot negotiates SASL before
    /// sending NICK/USER, which authenticates with the network during connection.
    /// </summary>
    public SaslConfig Sasl { get; init; } = new();
}

public sealed class SaslConfig
{
    public bool Enabled { get; init; } = false;
    /// <summary>Only "PLAIN" is currently supported.</summary>
    public string Mechanism { get; init; } = "PLAIN";
    public string Username { get; init; } = string.Empty;
    // Never put a real password here — use the MONOBOT_Sasl__Password environment variable.
    public string Password { get; init; } = string.Empty;
}

public sealed class NickServConfig
{
    public string Username { get; init; } = string.Empty;
    // Never put a real password here — use the MONOBOT_NickServ__Password environment variable.
    public string Password { get; init; } = string.Empty;
}

public sealed class DatabaseConfig
{
    public string Path { get; init; } = "monobot.db";
}
