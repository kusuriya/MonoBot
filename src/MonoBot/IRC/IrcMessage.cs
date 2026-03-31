using System.Text.RegularExpressions;

namespace MonoBot.IRC;

/// <summary>Immutable parsed IRC message per RFC 1459.</summary>
public sealed record IrcMessage(
    string? Prefix,
    string Command,
    string[] Parameters,
    string? Trail
)
{
    // nick!user@host format — extracts just the nick portion.
    public string? Nick => Prefix is null ? null
        : Prefix.Contains('!') ? Prefix[..Prefix.IndexOf('!')] : Prefix;
}

public static class IrcMessageParser
{
    // Compiled once — fixes the original per-message regex allocation bug.
    private static readonly Regex MessageRegex = new(
        @"^(:(?<prefix>\S+) )?(?<command>[A-Z0-9]+)( (?!:)(?<params>[^:]+?))?( :(?<trail>.+))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        matchTimeout: TimeSpan.FromMilliseconds(100));

    public static IrcMessage? Parse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var match = MessageRegex.Match(line.TrimEnd('\r', '\n'));
        if (!match.Success)
            return null;

        var prefix = match.Groups["prefix"].Success ? match.Groups["prefix"].Value : null;
        var command = match.Groups["command"].Value;
        var paramsRaw = match.Groups["params"].Success ? match.Groups["params"].Value.Trim() : null;
        var trail = match.Groups["trail"].Success ? match.Groups["trail"].Value : null;

        var parameters = paramsRaw is null
            ? []
            : paramsRaw.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return new IrcMessage(prefix, command, parameters, trail);
    }
}
