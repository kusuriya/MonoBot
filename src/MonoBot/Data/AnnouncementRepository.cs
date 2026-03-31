using Microsoft.Data.Sqlite;

using MonoBot.Abstractions.Data;

namespace MonoBot.Data;

public sealed class AnnouncementRepository(string connectionString) : IAnnouncementRepository
{
    public async Task<IReadOnlyList<Announcement>> GetRecentAsync(string channel, int limit = 2, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT date, announcement
            FROM announcements
            WHERE chan = @chan
            ORDER BY date DESC
            LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@chan", channel);
        cmd.Parameters.AddWithValue("@limit", limit);

        var results = new List<Announcement>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new Announcement(
                Date: reader.GetString(0),
                Text: reader.GetString(1)
            ));
        }

        return results;
    }

    public async Task AddAsync(string channel, string text, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO announcements (chan, date, announcement)
            VALUES (@chan, @date, @announcement)
            """;
        cmd.Parameters.AddWithValue("@chan", channel);
        cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("@announcement", text);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
