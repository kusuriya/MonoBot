using System.Text;
using MonoBot.IRC;

namespace MonoBot.Tests.IRC;

public class SaslHelperTests
{
    // ── BuildPlainCredentials ─────────────────────────────────────────────

    [Fact]
    public void BuildPlainCredentials_ProducesCorrectBase64Format()
    {
        // SASL PLAIN format: base64("" + NUL + username + NUL + password)
        // For user="nick" password="pass":
        //   bytes: 0x00 'n' 'i' 'c' 'k' 0x00 'p' 'a' 's' 's'
        var result = SaslHelper.BuildPlainCredentials("nick", "pass");

        var decoded = Convert.FromBase64String(result);
        decoded[0].Should().Be(0x00, "authzid is empty");
        var nullIdx = Array.IndexOf(decoded, (byte)0x00, 1);
        nullIdx.Should().BeGreaterThan(0);
        Encoding.UTF8.GetString(decoded, 1, nullIdx - 1).Should().Be("nick");
        Encoding.UTF8.GetString(decoded, nullIdx + 1, decoded.Length - nullIdx - 1).Should().Be("pass");
    }

    [Fact]
    public void BuildPlainCredentials_EmptyPassword_StillEncodes()
    {
        var result = SaslHelper.BuildPlainCredentials("user", "");

        var decoded = Convert.FromBase64String(result);
        // Should be: NUL + "user" + NUL  (no trailing bytes for password)
        decoded.Should().HaveCount(1 + 4 + 1);
        decoded[0].Should().Be(0x00);
        decoded[^1].Should().Be(0x00);
    }

    [Fact]
    public void BuildPlainCredentials_Utf8UsernameAndPassword()
    {
        var result = SaslHelper.BuildPlainCredentials("usér", "pässwörd");

        var decoded = Convert.FromBase64String(result);
        decoded[0].Should().Be(0x00);

        var separatorIdx = -1;
        for (var i = 1; i < decoded.Length; i++)
        {
            if (decoded[i] == 0x00) { separatorIdx = i; break; }
        }
        separatorIdx.Should().BeGreaterThan(0);

        Encoding.UTF8.GetString(decoded, 1, separatorIdx - 1).Should().Be("usér");
        Encoding.UTF8.GetString(decoded, separatorIdx + 1, decoded.Length - separatorIdx - 1).Should().Be("pässwörd");
    }

    // ── ChunkCredentials ──────────────────────────────────────────────────

    [Fact]
    public void ChunkCredentials_ShortString_ReturnsSingleChunk()
    {
        var input = "abc";
        var chunks = SaslHelper.ChunkCredentials(input);

        chunks.Should().ContainSingle().Which.Should().Be("abc");
    }

    [Fact]
    public void ChunkCredentials_EmptyString_ReturnsPlusTerminator()
    {
        var chunks = SaslHelper.ChunkCredentials("");

        chunks.Should().ContainSingle().Which.Should().Be("+");
    }

    [Fact]
    public void ChunkCredentials_ExactlyMaxSize_AppendsTerminator()
    {
        var input = new string('A', SaslHelper.MaxAuthenticateChunkSize);
        var chunks = SaslHelper.ChunkCredentials(input);

        chunks.Should().HaveCount(2);
        chunks[0].Should().HaveLength(SaslHelper.MaxAuthenticateChunkSize);
        chunks[1].Should().Be("+");
    }

    [Fact]
    public void ChunkCredentials_OneOverMaxSize_SplitsIntoTwoChunks()
    {
        var input = new string('A', SaslHelper.MaxAuthenticateChunkSize + 1);
        var chunks = SaslHelper.ChunkCredentials(input);

        chunks.Should().HaveCount(2);
        chunks[0].Should().HaveLength(SaslHelper.MaxAuthenticateChunkSize);
        chunks[1].Should().HaveLength(1);
    }

    [Fact]
    public void ChunkCredentials_TwoFullChunks_AppendsTerminator()
    {
        var input = new string('A', SaslHelper.MaxAuthenticateChunkSize * 2);
        var chunks = SaslHelper.ChunkCredentials(input);

        chunks.Should().HaveCount(3);
        chunks[0].Should().HaveLength(SaslHelper.MaxAuthenticateChunkSize);
        chunks[1].Should().HaveLength(SaslHelper.MaxAuthenticateChunkSize);
        chunks[2].Should().Be("+");
    }

    [Fact]
    public void ChunkCredentials_LargePayload_ReconstitutesOriginal()
    {
        var input = new string('X', SaslHelper.MaxAuthenticateChunkSize * 3 + 50);
        var chunks = SaslHelper.ChunkCredentials(input);

        // Reconstruct: discard trailing "+" if present
        var reconstructed = string.Concat(chunks.Where(c => c != "+"));
        reconstructed.Should().Be(input);
    }

    // ── Round-trip ────────────────────────────────────────────────────────

    [Fact]
    public void BuildAndChunk_RealCredentials_RoundTrip()
    {
        var base64 = SaslHelper.BuildPlainCredentials("testuser", "hunter2");
        var chunks = SaslHelper.ChunkCredentials(base64);

        // Chunks must all be at most 400 chars (or the single "+" terminator).
        foreach (var chunk in chunks)
            chunk.Length.Should().BeLessThanOrEqualTo(SaslHelper.MaxAuthenticateChunkSize,
                because: "'+'terminator is 1 char so it always fits");

        // Reconstruct and verify base64 is intact.
        var reconstructed = string.Concat(chunks.Where(c => c != "+"));
        reconstructed.Should().Be(base64);

        // Decode and verify original credentials survive the round-trip.
        var decoded = Convert.FromBase64String(reconstructed);
        var separatorIdx = Array.IndexOf(decoded, (byte)0x00, 1);
        Encoding.UTF8.GetString(decoded, 1, separatorIdx - 1).Should().Be("testuser");
        Encoding.UTF8.GetString(decoded, separatorIdx + 1, decoded.Length - separatorIdx - 1).Should().Be("hunter2");
    }
}
