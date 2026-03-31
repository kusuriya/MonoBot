using Microsoft.Data.Sqlite;
using MonoBot.Abstractions.Data;
using MonoBot.Data;

namespace MonoBot.Tests.Data;

public class RepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private string _connectionString = null!;
    private DatabaseInitializer _initializer = null!;

    public async Task InitializeAsync()
    {
        // Use a named in-memory database with shared cache so multiple connections
        // (from DatabaseInitializer, BenderRepository, AnnouncementRepository) all
        // see the same data. The name is unique per test instance.
        var dbName = $"testdb_{Guid.NewGuid():N}";
        _connectionString = $"Data Source=file:{dbName}?mode=memory&cache=shared";

        // Keep one connection open for the test lifetime so the in-memory DB stays alive.
        _connection = new SqliteConnection(_connectionString);
        await _connection.OpenAsync();

        _initializer = new DatabaseInitializer(_connectionString);
        await _initializer.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    // ──────────────────────────────────────────────────────
    // DatabaseInitializer
    // ──────────────────────────────────────────────────────

    [Fact]
    public async Task DatabaseInitializer_IsIdempotent()
    {
        // Calling EnsureCreatedAsync twice should not throw.
        var act = async () => await _initializer.EnsureCreatedAsync();
        await act.Should().NotThrowAsync();
    }

    // ──────────────────────────────────────────────────────
    // BenderRepository
    // ──────────────────────────────────────────────────────

    [Fact]
    public async Task Bender_EmptyTable_ReturnsNull()
    {
        var repo = new BenderRepository(_connectionString);
        var result = await repo.GetRandomQuoteAsync();
        result.Should().BeNull();
    }

    [Fact]
    public async Task Bender_WithRows_ReturnsAQuote()
    {
        await SeedBender("Bite my shiny metal ass!", "Your account is a million years overdue!");

        var repo = new BenderRepository(_connectionString);
        var result = await repo.GetRandomQuoteAsync();

        result.Should().BeOneOf("Bite my shiny metal ass!", "Your account is a million years overdue!");
    }

    [Fact]
    public async Task Bender_WithRows_EventuallyReturnsDifferentQuotes()
    {
        await SeedBender("Quote A", "Quote B", "Quote C", "Quote D", "Quote E");
        var repo = new BenderRepository(_connectionString);

        var results = new HashSet<string?>();
        for (int i = 0; i < 30; i++)
            results.Add(await repo.GetRandomQuoteAsync());

        // With 5 distinct quotes and 30 draws, we expect more than one distinct result.
        results.Count.Should().BeGreaterThan(1);
    }

    // ──────────────────────────────────────────────────────
    // AnnouncementRepository
    // ──────────────────────────────────────────────────────

    [Fact]
    public async Task Announcements_EmptyTable_ReturnsEmptyList()
    {
        var repo = new AnnouncementRepository(_connectionString);
        var results = await repo.GetRecentAsync("#test");
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task Announcements_FiltersByChannel()
    {
        var repo = new AnnouncementRepository(_connectionString);
        await repo.AddAsync("#channelA", "Only for A");
        await repo.AddAsync("#channelB", "Only for B");

        var resultsA = await repo.GetRecentAsync("#channelA");
        var resultsB = await repo.GetRecentAsync("#channelB");

        resultsA.Should().ContainSingle().Which.Text.Should().Be("Only for A");
        resultsB.Should().ContainSingle().Which.Text.Should().Be("Only for B");
    }

    [Fact]
    public async Task Announcements_RespectsLimit()
    {
        var repo = new AnnouncementRepository(_connectionString);
        await repo.AddAsync("#test", "First");
        await repo.AddAsync("#test", "Second");
        await repo.AddAsync("#test", "Third");

        var results = await repo.GetRecentAsync("#test", limit: 2);
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task Announcements_ResultHasDateAndText()
    {
        var repo = new AnnouncementRepository(_connectionString);
        await repo.AddAsync("#test", "Hello world");

        var results = await repo.GetRecentAsync("#test");

        results.Should().ContainSingle();
        results[0].Text.Should().Be("Hello world");
        results[0].Date.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Announcements_SqlInjectionStoredLiterally()
    {
        // A SQL injection attempt must be stored as literal text, not executed.
        const string injection = "'; DROP TABLE announcements; --";
        var repo = new AnnouncementRepository(_connectionString);
        await repo.AddAsync("#test", injection);

        // The table must still exist and contain the literal injection string.
        var results = await repo.GetRecentAsync("#test");
        results.Should().ContainSingle().Which.Text.Should().Be(injection);
    }

    [Fact]
    public async Task Announcements_TablesStillExistAfterInjectionAttempt()
    {
        const string injection = "'; DROP TABLE bender; DROP TABLE announcements; --";
        var repo = new AnnouncementRepository(_connectionString);
        await repo.AddAsync("#test", injection);

        // Both tables must still be queryable.
        var annAct = async () => await repo.GetRecentAsync("#test");
        var benderAct = async () => await new BenderRepository(_connectionString).GetRandomQuoteAsync();

        await annAct.Should().NotThrowAsync();
        await benderAct.Should().NotThrowAsync();
    }

    // ──────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────

    private async Task SeedBender(params string[] quotes)
    {
        await using var cmd = _connection.CreateCommand();
        foreach (var quote in quotes)
        {
            cmd.CommandText = "INSERT INTO bender (sayings) VALUES (@q)";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@q", quote);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
