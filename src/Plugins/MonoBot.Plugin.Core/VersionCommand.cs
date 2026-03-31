using MonoBot.Abstractions;

namespace MonoBot.Plugin.Core;

public sealed class VersionCommand : ICommand
{
    public string Trigger => ".version";
    public bool IsAdminOnly => false;

    public Task ExecuteAsync(string channel, string nick, string? options, IMessageSender sender, CancellationToken ct = default)
    {
        var version = typeof(VersionCommand).Assembly.GetName().Version?.ToString() ?? "unknown";
        var runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        return sender.SendChannelMessageAsync(channel, $"MonoBot {version} running on {runtime}", ct);
    }
}
