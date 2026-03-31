namespace MonoBot.Abstractions;

/// <summary>
/// A bot command triggered by a prefix in a channel message.
/// Set <see cref="IsAdminOnly"/> to <c>true</c> to restrict execution to configured admin nicks.
/// </summary>
public interface ICommand
{
    /// <summary>The trigger string that activates this command (e.g. ".bender", "!quit").</summary>
    string Trigger { get; }

    /// <summary>
    /// When <c>true</c> the command router enforces admin-nick authentication before calling
    /// <see cref="ExecuteAsync"/>. Non-admin callers are silently rejected and a warning is logged.
    /// </summary>
    bool IsAdminOnly { get; }

    /// <summary>Executes the command.</summary>
    /// <param name="channel">The IRC channel the message was sent to.</param>
    /// <param name="nick">The raw "nick!user@host" of the sender.</param>
    /// <param name="options">Everything after the trigger word, or <c>null</c> if the message was the trigger alone.</param>
    /// <param name="sender">Use this to send messages back to IRC.</param>
    Task ExecuteAsync(string channel, string nick, string? options, IMessageSender sender, CancellationToken ct = default);
}
