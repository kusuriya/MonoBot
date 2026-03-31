using FluentAssertions;
using Microsoft.Extensions.Configuration;
using MonoBot.Configuration;

namespace MonoBot.Tests.Configuration;

public class ConfigurationLoaderTests
{
    private static IConfiguration BuildFrom(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    [Fact]
    public void Load_WithAllDefaults_ReturnsValidConfig()
    {
        var configuration = BuildFrom(new Dictionary<string, string?>
        {
            ["Server"] = "irc.libera.chat",
            ["Port"] = "6667",
            ["Nick"] = "monobot",
            ["Name"] = "MonoBot"
        });

        var config = ConfigurationLoader.Load(configuration);

        config.Server.Should().Be("irc.libera.chat");
        config.Port.Should().Be(6667);
        config.Nick.Should().Be("monobot");
        config.Name.Should().Be("MonoBot");
        config.Debug.Should().BeFalse();
        config.UseNickServ.Should().BeFalse();
    }

    [Fact]
    public void Load_EnvVarOverridesJsonNick()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server"] = "irc.libera.chat",
                ["Port"] = "6667",
                ["Nick"] = "defaultnick",
                ["Name"] = "MonoBot"
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // simulates MONOBOT_Nick env var after stripping the prefix
                ["Nick"] = "overriddennick"
            })
            .Build();

        var config = ConfigurationLoader.Load(configuration);

        config.Nick.Should().Be("overriddennick");
    }

    [Fact]
    public void Load_MissingServer_Throws()
    {
        var configuration = BuildFrom(new Dictionary<string, string?>
        {
            ["Server"] = "",
            ["Port"] = "6667",
            ["Nick"] = "monobot"
        });

        var act = () => ConfigurationLoader.Load(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Server*");
    }

    [Fact]
    public void Load_MissingNick_Throws()
    {
        var configuration = BuildFrom(new Dictionary<string, string?>
        {
            ["Server"] = "irc.libera.chat",
            ["Port"] = "6667",
            ["Nick"] = ""
        });

        var act = () => ConfigurationLoader.Load(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Nick*");
    }

    [Fact]
    public void Load_InvalidPort_Throws()
    {
        var configuration = BuildFrom(new Dictionary<string, string?>
        {
            ["Server"] = "irc.libera.chat",
            ["Port"] = "99999",
            ["Nick"] = "monobot"
        });

        var act = () => ConfigurationLoader.Load(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Port*");
    }

    [Fact]
    public void Load_NickServPasswordNotSet_DefaultsToEmptyString()
    {
        var configuration = BuildFrom(new Dictionary<string, string?>
        {
            ["Server"] = "irc.libera.chat",
            ["Port"] = "6667",
            ["Nick"] = "monobot"
        });

        var config = ConfigurationLoader.Load(configuration);

        config.NickServ.Password.Should().Be(string.Empty);
    }

    [Fact]
    public void Load_AdminsList_ParsedCorrectly()
    {
        var configuration = BuildFrom(new Dictionary<string, string?>
        {
            ["Server"] = "irc.libera.chat",
            ["Port"] = "6667",
            ["Nick"] = "monobot",
            ["Admins:0"] = "kusuriya",
            ["Admins:1"] = "otherAdmin"
        });

        var config = ConfigurationLoader.Load(configuration);

        config.Admins.Should().BeEquivalentTo(["kusuriya", "otherAdmin"]);
    }

    [Fact]
    public void Load_ChannelsList_ParsedCorrectly()
    {
        var configuration = BuildFrom(new Dictionary<string, string?>
        {
            ["Server"] = "irc.libera.chat",
            ["Port"] = "6667",
            ["Nick"] = "monobot",
            ["Channels:0"] = "#bots",
            ["Channels:1"] = "#general"
        });

        var config = ConfigurationLoader.Load(configuration);

        config.Channels.Should().BeEquivalentTo(["#bots", "#general"]);
    }

    [Fact]
    public void Load_DatabasePath_ParsedCorrectly()
    {
        var configuration = BuildFrom(new Dictionary<string, string?>
        {
            ["Server"] = "irc.libera.chat",
            ["Port"] = "6667",
            ["Nick"] = "monobot",
            ["Database:Path"] = "/data/monobot.db"
        });

        var config = ConfigurationLoader.Load(configuration);

        config.Database.Path.Should().Be("/data/monobot.db");
    }
}
