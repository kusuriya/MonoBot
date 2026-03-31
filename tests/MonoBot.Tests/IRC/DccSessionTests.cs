using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using MonoBot.Abstractions;
using MonoBot.IRC;
using MonoBot.Plugin.Admin;

namespace MonoBot.Tests.IRC;

public class DccSessionTests
{
    private const string CorrectPassword = "s3cr3tpassword";

    private static (DccSession session, IMessageSender ircSender) BuildSession(
        string password = CorrectPassword,
        IEnumerable<ICommand>? commands = null)
    {
        commands ??= [new JoinCommand(), new PartCommand()];
        var ircSender = Substitute.For<IMessageSender>();
        var session = new DccSession(password, commands, NullLogger<DccSession>.Instance);
        return (session, ircSender);
    }

    private static async Task<string> RunSessionWithInputAsync(
        DccSession session,
        IMessageSender ircSender,
        params string?[] inputs)
    {
        var output = new StringBuilder();
        var reader = new QueuedTextReader(inputs ?? [null]);
        var writer = new StringBuilderWriter(output);
        await session.RunAsync(reader, writer, ircSender);
        return output.ToString();
    }

    // ── No password configured ────────────────────────────────────────────

    [Fact]
    public async Task NoPassword_SessionRefusesImmediately()
    {
        var (session, ircSender) = BuildSession(password: "");
        var output = await RunSessionWithInputAsync(session, ircSender, null);

        output.Should().Contain("disabled");
        await ircSender.DidNotReceive().SendRawAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── Unauthenticated state ─────────────────────────────────────────────

    [Fact]
    public async Task Unauthenticated_SendsWelcomePromptOnConnect()
    {
        var (session, ircSender) = BuildSession();
        var output = await RunSessionWithInputAsync(session, ircSender, null);

        output.Should().Contain("AUTH");
    }

    [Fact]
    public async Task Unauthenticated_NonAuthCommand_RejectsWithPrompt()
    {
        var (session, ircSender) = BuildSession();
        var output = await RunSessionWithInputAsync(session, ircSender, "!join #test", null);

        output.Should().Contain("Not authenticated");
        await ircSender.DidNotReceive().SendRawAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unauthenticated_WrongPassword_RejectsAndKeepsRunning()
    {
        var (session, ircSender) = BuildSession();
        var output = await RunSessionWithInputAsync(session, ircSender, "AUTH wrongpassword", null);

        output.Should().Contain("Authentication failed");
        await ircSender.DidNotReceive().SendRawAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unauthenticated_CorrectPassword_AuthenticatesAndConfirms()
    {
        var (session, ircSender) = BuildSession();
        var output = await RunSessionWithInputAsync(session, ircSender, $"AUTH {CorrectPassword}", null);

        output.Should().Contain("Authenticated");
    }

    // ── Authenticated state ───────────────────────────────────────────────

    [Fact]
    public async Task Authenticated_JoinCommand_SendsJoinToIrc()
    {
        var (session, ircSender) = BuildSession();
        await RunSessionWithInputAsync(session, ircSender, $"AUTH {CorrectPassword}", "!join #newchan", null);

        await ircSender.Received(1).SendRawAsync("JOIN #newchan", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authenticated_PartCommand_SendsPartToIrc()
    {
        var (session, ircSender) = BuildSession();
        await RunSessionWithInputAsync(session, ircSender, $"AUTH {CorrectPassword}", "!part #oldchan", null);

        await ircSender.Received(1).SendRawAsync("PART #oldchan", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authenticated_PartCommandWithNoArgsAndNoChannel_ReturnsUsage()
    {
        var (session, ircSender) = BuildSession();
        var output = await RunSessionWithInputAsync(session, ircSender, $"AUTH {CorrectPassword}", "!part", null);

        output.Should().Contain("Usage");
        await ircSender.DidNotReceive().SendRawAsync(Arg.Is<string>(s => s.StartsWith("PART")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authenticated_UnknownCommand_ReturnsErrorAndDoesNotCrash()
    {
        var (session, ircSender) = BuildSession();
        var output = await RunSessionWithInputAsync(session, ircSender, $"AUTH {CorrectPassword}", "!nonexistent", null);

        output.Should().Contain("Unknown command");
        await ircSender.DidNotReceive().SendRawAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authenticated_HelpCommand_ListsAdminCommandTriggers()
    {
        var (session, ircSender) = BuildSession();
        var output = await RunSessionWithInputAsync(session, ircSender, $"AUTH {CorrectPassword}", "HELP", null);

        output.Should().Contain("!join");
        output.Should().Contain("!part");
    }

    [Fact]
    public async Task Authenticated_MultipleCommands_AllExecuteInOrder()
    {
        var (session, ircSender) = BuildSession();
        await RunSessionWithInputAsync(session, ircSender,
            $"AUTH {CorrectPassword}",
            "!join #chan1",
            "!join #chan2",
            null);

        await ircSender.Received(1).SendRawAsync("JOIN #chan1", Arg.Any<CancellationToken>());
        await ircSender.Received(1).SendRawAsync("JOIN #chan2", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authenticated_ResponseGoesToDccNotIrc()
    {
        // DebugCommand responds with a channel message (goes to DCC), not a raw IRC line.
        var debugCmd = new DebugCommand(NullLogger<DebugCommand>.Instance);
        var (session, ircSender) = BuildSession(commands: [debugCmd]);

        var output = await RunSessionWithInputAsync(session, ircSender,
            $"AUTH {CorrectPassword}", "!debug on", null);

        // The response text must appear in DCC output, not as a raw IRC command.
        output.Should().Contain("enabled");
        await ircSender.DidNotReceive().SendRawAsync(
            Arg.Is<string>(s => s.Contains("enabled")),
            Arg.Any<CancellationToken>());
    }

    // ── Auth cannot be re-attempted after lock-out ────────────────────────

    [Fact]
    public async Task Unauthenticated_WrongThenCorrectPassword_AuthenticatesOnSecondAttempt()
    {
        var (session, ircSender) = BuildSession();
        var output = await RunSessionWithInputAsync(session, ircSender,
            "AUTH wrongpassword",
            $"AUTH {CorrectPassword}",
            "!join #test",
            null);

        output.Should().Contain("Authentication failed");
        output.Should().Contain("Authenticated");
        await ircSender.Received(1).SendRawAsync("JOIN #test", Arg.Any<CancellationToken>());
    }

    // ── CommandRouter no longer handles admin commands ────────────────────

    [Fact]
    public async Task CommandRouter_AdminCommand_IsIgnoredFromChannel()
    {
        var sender = Substitute.For<IMessageSender>();
        var router = new MonoBot.Commands.CommandRouter(
            [new JoinCommand()],
            NullLogger<MonoBot.Commands.CommandRouter>.Instance);

        await router.RouteAsync("kusuriya!u@h", "#test", "!join #somewhere", sender);

        await sender.DidNotReceive().SendRawAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await sender.DidNotReceive().SendChannelMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>Feeds a fixed sequence of lines to DccSession as if typed by the admin.</summary>
    private sealed class QueuedTextReader(params string?[] lines) : TextReader
    {
        private readonly Queue<string?> _queue = new(lines);

        public override ValueTask<string?> ReadLineAsync(CancellationToken ct)
            => ValueTask.FromResult(_queue.Count > 0 ? _queue.Dequeue() : null);
    }

    /// <summary>Captures all WriteLine calls into a StringBuilder for assertions.</summary>
    private sealed class StringBuilderWriter(StringBuilder sb) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken ct)
        {
            sb.AppendLine(buffer.ToString());
            return Task.CompletedTask;
        }

        public override void WriteLine(string? value) => sb.AppendLine(value);
    }
}
