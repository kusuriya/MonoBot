namespace MonoBot.Abstractions.Data;

public interface IBenderRepository
{
    Task<string?> GetRandomQuoteAsync(CancellationToken ct = default);
}
