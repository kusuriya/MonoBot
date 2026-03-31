using Microsoft.Data.Sqlite;

using MonoBot.Abstractions.Data;

namespace MonoBot.Data;

public sealed class BenderRepository(string connectionString) : IBenderRepository
{
    public async Task<string?> GetRandomQuoteAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT sayings FROM bender ORDER BY RANDOM() LIMIT 1";

        var result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }
}
