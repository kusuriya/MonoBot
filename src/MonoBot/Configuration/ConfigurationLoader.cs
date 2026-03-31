using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MonoBot.Configuration;

public static class ConfigurationLoader
{
    public static IConfiguration Build(string[] args)
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "MONOBOT_")
            .AddCommandLine(args)
            .Build();
    }

    public static BotConfig Load(IConfiguration configuration)
    {
        var config = new BotConfig();
        configuration.Bind(config);

        if (string.IsNullOrWhiteSpace(config.Server))
            throw new InvalidOperationException("Configuration error: Server must not be empty.");
        if (config.Port is < 1 or > 65535)
            throw new InvalidOperationException($"Configuration error: Port {config.Port} is not a valid port number.");
        if (string.IsNullOrWhiteSpace(config.Nick))
            throw new InvalidOperationException("Configuration error: Nick must not be empty.");

        return config;
    }
}
