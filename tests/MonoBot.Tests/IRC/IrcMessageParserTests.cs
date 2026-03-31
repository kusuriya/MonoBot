using MonoBot.IRC;

namespace MonoBot.Tests.IRC;

public class IrcMessageParserTests
{
    [Fact]
    public void Parse_Ping_ReturnsPingCommand()
    {
        var msg = IrcMessageParser.Parse("PING :irc.libera.chat");

        msg.Should().NotBeNull();
        msg!.Command.Should().Be("PING");
        msg.Trail.Should().Be("irc.libera.chat");
        msg.Prefix.Should().BeNull();
    }

    [Fact]
    public void Parse_Privmsg_ParsesAllFields()
    {
        var msg = IrcMessageParser.Parse(":kusuriya!user@host.com PRIVMSG #test :hello world");

        msg.Should().NotBeNull();
        msg!.Command.Should().Be("PRIVMSG");
        msg.Prefix.Should().Be("kusuriya!user@host.com");
        msg.Nick.Should().Be("kusuriya");
        msg.Parameters.Should().ContainSingle().Which.Should().Be("#test");
        msg.Trail.Should().Be("hello world");
    }

    [Fact]
    public void Parse_ServerNumeric001_ParsesCorrectly()
    {
        var msg = IrcMessageParser.Parse(":irc.libera.chat 001 monobot :Welcome to the network");

        msg.Should().NotBeNull();
        msg!.Command.Should().Be("001");
        msg.Prefix.Should().Be("irc.libera.chat");
        msg.Trail.Should().Be("Welcome to the network");
    }

    [Fact]
    public void Parse_JoinWithNoTrail_ParsesCorrectly()
    {
        var msg = IrcMessageParser.Parse(":kusuriya!user@host.com JOIN #test");

        msg.Should().NotBeNull();
        msg!.Command.Should().Be("JOIN");
        msg.Parameters.Should().ContainSingle().Which.Should().Be("#test");
        msg.Trail.Should().BeNull();
    }

    [Fact]
    public void Parse_NullInput_ReturnsNull()
    {
        var msg = IrcMessageParser.Parse(null);
        msg.Should().BeNull();
    }

    [Fact]
    public void Parse_EmptyString_ReturnsNull()
    {
        var msg = IrcMessageParser.Parse("");
        msg.Should().BeNull();
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsNull()
    {
        var msg = IrcMessageParser.Parse("   ");
        msg.Should().BeNull();
    }

    [Fact]
    public void Parse_MessageWithCarriageReturn_ParsesCorrectly()
    {
        // IRC lines may include \r\n line endings.
        var msg = IrcMessageParser.Parse("PING :server\r");

        msg.Should().NotBeNull();
        msg!.Command.Should().Be("PING");
        msg.Trail.Should().Be("server");
    }

    [Fact]
    public void Nick_WhenPrefixHasUserAtHost_ExtractsOnlyNick()
    {
        var msg = IrcMessageParser.Parse(":kusuriya!~user@10.0.0.1 PRIVMSG #test :hi");

        msg!.Nick.Should().Be("kusuriya");
    }

    [Fact]
    public void Nick_WhenPrefixIsServerOnly_ReturnsPrefix()
    {
        var msg = IrcMessageParser.Parse(":irc.libera.chat NOTICE * :Connecting");

        msg!.Nick.Should().Be("irc.libera.chat");
    }
}
