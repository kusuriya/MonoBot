using Microsoft.Extensions.DependencyInjection;

namespace MonoBot.Abstractions;

/// <summary>
/// Entry point for a MonoBot plugin assembly.
/// Implement this interface in your plugin and call
/// <c>services.AddSingleton&lt;ICommand, YourCommand&gt;()</c> inside
/// <see cref="RegisterServices"/> to make your commands available.
/// </summary>
public interface IBotPlugin
{
    /// <summary>Human-readable plugin name shown in startup logs.</summary>
    string Name { get; }

    /// <summary>Plugin version string shown in startup logs.</summary>
    string Version { get; }

    /// <summary>One-line description of what this plugin provides.</summary>
    string Description { get; }

    /// <summary>
    /// Called by the host at startup. Register <see cref="ICommand"/> implementations
    /// and any supporting services (repositories, HTTP clients, etc.) here.
    /// </summary>
    void RegisterServices(IServiceCollection services);
}
