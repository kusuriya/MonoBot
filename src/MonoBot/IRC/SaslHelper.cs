using System.Text;

namespace MonoBot.IRC;

/// <summary>
/// Helpers for SASL authentication in IRC (IRCv3).
/// Extracted as a static class so credential-building logic can be unit-tested without a socket.
/// </summary>
public static class SaslHelper
{
    /// <summary>
    /// IRC limits a single AUTHENTICATE line to 400 characters of base64 payload.
    /// Larger payloads must be split into 400-character chunks.
    /// </summary>
    public const int MaxAuthenticateChunkSize = 400;

    /// <summary>
    /// Builds the base64-encoded SASL PLAIN credential string.
    /// Format: <c>base64( "" + NUL + username + NUL + password )</c>
    /// (authzid is intentionally empty — the authcid serves as both identities.)
    /// </summary>
    public static string BuildPlainCredentials(string username, string password)
    {
        // Layout: authzid(empty) \0 authcid \0 passwd
        var usernameBytes = Encoding.UTF8.GetBytes(username);
        var passwordBytes = Encoding.UTF8.GetBytes(password);

        var payload = new byte[1 + usernameBytes.Length + 1 + passwordBytes.Length];
        payload[0] = 0x00; // empty authzid
        usernameBytes.CopyTo(payload, 1);
        payload[1 + usernameBytes.Length] = 0x00;
        passwordBytes.CopyTo(payload, 2 + usernameBytes.Length);

        return Convert.ToBase64String(payload);
    }

    /// <summary>
    /// Splits a base64 credential string into IRC-safe 400-char chunks.
    /// If the string divides exactly into 400-char chunks, an extra <c>"+"</c> chunk is appended
    /// to signal end-of-data per the IRCv3 SASL specification.
    /// </summary>
    public static IReadOnlyList<string> ChunkCredentials(string base64)
    {
        var chunks = new List<string>();
        for (int i = 0; i < base64.Length; i += MaxAuthenticateChunkSize)
            chunks.Add(base64[i..Math.Min(i + MaxAuthenticateChunkSize, base64.Length)]);

        // A final chunk of exactly 400 chars means there is more data — send "+" as terminator.
        if (base64.Length > 0 && base64.Length % MaxAuthenticateChunkSize == 0)
            chunks.Add("+");

        // Empty payload (extremely unusual) is expressed as "+".
        if (chunks.Count == 0)
            chunks.Add("+");

        return chunks;
    }
}
