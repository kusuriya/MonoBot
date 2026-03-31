namespace MonoBot.Abstractions.Data;

public interface IAnnouncementRepository
{
    Task<IReadOnlyList<Announcement>> GetRecentAsync(string channel, int limit = 2, CancellationToken ct = default);
    Task AddAsync(string channel, string text, CancellationToken ct = default);
}
