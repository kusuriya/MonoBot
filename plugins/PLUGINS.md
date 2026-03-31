# MonoBot Plugin System

MonoBot loads commands from plugin DLLs in this directory at startup.
Any file matching `MonoBot.Plugin.*.dll` is scanned for `IBotPlugin` implementations.

## How It Works

1. On startup the host scans `plugins/` (configurable via `MONOBOT_PluginsPath`) for `MonoBot.Plugin.*.dll` files.
2. Each DLL is loaded with `Assembly.LoadFrom`.
3. Every concrete class that implements `IBotPlugin` is instantiated and `RegisterServices(IServiceCollection)` is called.
4. Your plugin registers one or more `ICommand` services, which the `CommandRouter` picks up automatically.

Setting `IsAdminOnly = true` on a command restricts it to nicks listed in `MONOBOT_Admins__*`.

## Creating a Plugin

### 1. Create a class library

```bash
dotnet new classlib -n MonoBot.Plugin.MyPlugin -o src/Plugins/MonoBot.Plugin.MyPlugin
```

Add to `MonoBot.slnx`:
```bash
dotnet sln add src/Plugins/MonoBot.Plugin.MyPlugin/MonoBot.Plugin.MyPlugin.csproj
```

### 2. Reference MonoBot.Abstractions

Edit `MonoBot.Plugin.MyPlugin.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="9.*" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\MonoBot.Abstractions\MonoBot.Abstractions.csproj" />
  </ItemGroup>

</Project>
```

### 3. Implement ICommand

```csharp
// src/Plugins/MonoBot.Plugin.MyPlugin/MyCommand.cs
using Microsoft.Extensions.Logging;
using MonoBot.Abstractions;

namespace MonoBot.Plugin.MyPlugin;

public sealed class MyCommand(ILogger<MyCommand> logger) : ICommand
{
    // The text that triggers this command in a channel message.
    public string Trigger => ".mycommand";

    // Set to true to require the sender to be in the admin list.
    public bool IsAdminOnly => false;

    public async Task ExecuteAsync(
        string channel,   // IRC channel the message was sent to
        string nick,      // Sender's "nick!user@host"
        string? options,  // Everything after the trigger word, or null
        IMessageSender sender,
        CancellationToken ct = default)
    {
        logger.LogDebug(".mycommand invoked by {Nick} in {Channel}", nick, channel);
        await sender.SendChannelMessageAsync(channel, $"Hello, {nick}! Options: {options ?? "(none)"}", ct);
    }
}
```

### 4. Implement IBotPlugin

```csharp
// src/Plugins/MonoBot.Plugin.MyPlugin/MyPlugin.cs
using Microsoft.Extensions.DependencyInjection;
using MonoBot.Abstractions;

namespace MonoBot.Plugin.MyPlugin;

public sealed class MyPlugin : IBotPlugin
{
    public string Name => "MyPlugin";
    public string Version => "1.0.0";
    public string Description => "A short description of what this plugin does";

    public void RegisterServices(IServiceCollection services)
    {
        // Register one ICommand per command your plugin provides.
        // Other types (repositories, HTTP clients, etc.) can also be registered here.
        services.AddSingleton<ICommand, MyCommand>();
    }
}
```

### 5. Build and deploy

**Local development** — the plugin builds directly into `src/MonoBot/bin/<Config>/net10.0/plugins/`
via `src/Plugins/Directory.Build.props`, so `dotnet run` finds it automatically:

```bash
dotnet build src/Plugins/MonoBot.Plugin.MyPlugin
# then
cd src/MonoBot && dotnet run
```

**Docker** — add a `dotnet publish` line to the Dockerfile for your plugin:

```dockerfile
RUN dotnet publish src/Plugins/MonoBot.Plugin.MyPlugin/MonoBot.Plugin.MyPlugin.csproj \
    --configuration Release --no-restore --output /app/publish/plugins
```

**External / drop-in** — compile your plugin separately and copy the DLL (and any dependencies
not already in the host) into the `plugins/` directory, then restart the bot:

```bash
cp bin/Release/net10.0/MonoBot.Plugin.MyPlugin.dll /path/to/plugins/
```

---

## Injecting Bot Services

The host registers the following services before loading plugins.
Your command constructor can declare any of these as parameters.

| Type | Description |
|---|---|
| `IBenderRepository` | Read random Bender quotes from SQLite |
| `IAnnouncementRepository` | Read/write channel announcements in SQLite |
| `BotConfig` | Full bot configuration (server, nick, admins, etc.) |
| `CancellationTokenSource` | The host shutdown token source (see `QuitCommand` for usage) |
| `ILogger<T>` | Structured logging via Serilog |

Example — command that reads bot config:

```csharp
public sealed class StatusCommand(BotConfig config) : ICommand
{
    public string Trigger => ".status";
    public bool IsAdminOnly => false;

    public Task ExecuteAsync(string channel, string nick, string? options,
        IMessageSender sender, CancellationToken ct = default)
        => sender.SendChannelMessageAsync(channel,
            $"Connected to {config.Server}:{config.Port} as {config.Nick}", ct);
}
```

---

## Built-in Plugins

| Plugin DLL | Commands | Admin? |
|---|---|---|
| `MonoBot.Plugin.Bender.dll` | `.bender` | No |
| `MonoBot.Plugin.Announcements.dll` | `.announcements`, `.add-announcement` | No |
| `MonoBot.Plugin.Core.dll` | `.version`, `.help` | No |
| `MonoBot.Plugin.Admin.dll` | `!join`, `!part`, `!debug`, `!quit` | Yes — DCC only |

`.help` automatically lists all loaded commands — it updates itself when new plugins are added.

### Admin commands and DCC CHAT

Admin commands (`IsAdminOnly = true`) are **never** executed from IRC channels.
They can only be invoked over an authenticated **DCC CHAT** session:

1. From your IRC client, send the bot a DCC CHAT request.
2. The bot connects back to you over a direct TCP link.
3. Send `AUTH <password>` (set via `MONOBOT_AdminPassword` env var).
4. Once authenticated, type any admin command (`!join`, `!part`, `!quit`, etc.) or `HELP`.

**To write an admin-only plugin command**, set `IsAdminOnly = true` on your `ICommand` and
register it as usual with `services.AddSingleton<ICommand, YourCommand>()`. The `DccSession`
automatically picks it up — no extra wiring needed.

---

## Tips

- Plugin DLLs are loaded in filename order (`MonoBot.Plugin.Admin.dll` before `MonoBot.Plugin.Bender.dll` etc.).
- A failed plugin load (bad DLL, missing dependency) is logged as an error and skipped — the bot keeps running.
- Trigger strings are case-insensitive.
- If two plugins register the same trigger, the second registration wins (last DLL wins in load order).
