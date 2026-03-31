using Microsoft.Data.Sqlite;

namespace MonoBot.Data;

public sealed class DatabaseInitializer(string connectionString)
{
    public async Task EnsureCreatedAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS bender (
                id       INTEGER PRIMARY KEY AUTOINCREMENT,
                sayings  TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS announcements (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                chan         TEXT NOT NULL,
                date         TEXT NOT NULL,
                announcement TEXT NOT NULL
            );
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
