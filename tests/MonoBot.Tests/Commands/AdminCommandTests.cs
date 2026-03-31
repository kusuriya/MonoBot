using MonoBot.Plugin.Admin;

namespace MonoBot.Tests.Commands;

/// <summary>
/// Sanity checks that admin command metadata is correct.
/// Behaviour tests live in DccSessionTests — admin commands only execute over DCC.
/// </summary>
public class AdminCommandTests
{
    [Fact]
    public void JoinCommand_IsAdminOnly() => new JoinCommand().IsAdminOnly.Should().BeTrue();

    [Fact]
    public void PartCommand_IsAdminOnly() => new PartCommand().IsAdminOnly.Should().BeTrue();

    [Fact]
    public void DebugCommand_IsAdminOnly()
        => new DebugCommand(Microsoft.Extensions.Logging.Abstractions.NullLogger<DebugCommand>.Instance)
            .IsAdminOnly.Should().BeTrue();
}
