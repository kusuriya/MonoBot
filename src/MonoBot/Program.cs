using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MonoBot.Abstractions.Data;
using MonoBot.Commands;
using MonoBot.Configuration;
using MonoBot.Data;
using MonoBot.IRC;
using MonoBot.Plugins;
using Serilog;
using Serilog.Events;

var configuration = ConfigurationLoader.Build(args);
var botConfig = ConfigurationLoader.Load(configuration);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(botConfig.Debug ? LogEventLevel.Debug : LogEventLevel.Information)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

if (string.IsNullOrEmpty(botConfig.AdminPassword))
    Log.Logger.Warning("AdminPassword is not set — DCC admin is disabled. Set MONOBOT_AdminPassword to enable it.");

using var shutdownSource = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdownSource.Cancel();
};
AppDomain.CurrentDomain.ProcessExit += (_, _) => shutdownSource.Cancel();

var services = new ServiceCollection();
services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddSerilog(dispose: true);
});
services.AddSingleton(botConfig);
services.AddSingleton(configuration);
services.AddSingleton(shutdownSource);

// Data layer — concrete implementations registered before plugins so they can be injected.
var connectionString = $"Data Source={botConfig.Database.Path}";
services.AddSingleton(new DatabaseInitializer(connectionString));
services.AddSingleton<IBenderRepository>(new BenderRepository(connectionString));
services.AddSingleton<IAnnouncementRepository>(new AnnouncementRepository(connectionString));

// Plugin loading — scans plugins/ for MonoBot.Plugin.*.dll and calls RegisterServices on each.
var bootstrapLogger = Log.Logger.ForContext("SourceContext", "PluginLoader");
var msLogger = new Serilog.Extensions.Logging.SerilogLoggerFactory(bootstrapLogger)
    .CreateLogger("PluginLoader");

Log.Logger.Information("Loading plugins from '{Path}'...", botConfig.PluginsPath);
PluginLoader.LoadPlugins(services, botConfig.PluginsPath, msLogger);

services.AddSingleton<CommandRouter>();
services.AddSingleton<DccManager>();
services.AddSingleton<IrcClient>();

var provider = services.BuildServiceProvider();
var logger = provider.GetRequiredService<ILogger<Program>>();

logger.LogInformation("MonoBot {Version} starting — connecting to {Server}:{Port}",
    typeof(Program).Assembly.GetName().Version, botConfig.Server, botConfig.Port);

await provider.GetRequiredService<DatabaseInitializer>().EnsureCreatedAsync(shutdownSource.Token);

await provider.GetRequiredService<IrcClient>().RunAsync(shutdownSource.Token);

logger.LogInformation("MonoBot exited cleanly.");
