using Microsoft.Extensions.Logging.Abstractions;
using MonoBot.Abstractions;
using MonoBot.Abstractions.Data;
using MonoBot.Plugin.Bender;

namespace MonoBot.Tests.Commands;

public class BenderCommandTests
{
    private readonly IBenderRepository _repo = Substitute.For<IBenderRepository>();
    private readonly IMessageSender _sender = Substitute.For<IMessageSender>();
    private readonly BenderCommand _command;

    public BenderCommandTests()
    {
        _command = new BenderCommand(_repo, NullLogger<BenderCommand>.Instance);
    }

    [Fact]
    public async Task Execute_ReturnsQuoteFromRepository()
    {
        _repo.GetRandomQuoteAsync(Arg.Any<CancellationToken>())
            .Returns("Bite my shiny metal ass!");

        await _command.ExecuteAsync("#test", "nick", null, _sender);

        await _sender.Received(1).SendChannelMessageAsync("#test", "Bite my shiny metal ass!", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_EmptyDatabase_SendsFallbackMessage()
    {
        _repo.GetRandomQuoteAsync(Arg.Any<CancellationToken>())
            .Returns((string?)null);

        await _command.ExecuteAsync("#test", "nick", null, _sender);

        await _sender.Received(1).SendChannelMessageAsync("#test",
            Arg.Is<string>(s => s.Contains("nothing")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_RepositoryThrows_SendsErrorMessageAndDoesNotCrash()
    {
        _repo.GetRandomQuoteAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("DB is gone"));

        var act = async () => await _command.ExecuteAsync("#test", "nick", null, _sender);
        await act.Should().NotThrowAsync();

        await _sender.Received(1).SendChannelMessageAsync("#test",
            Arg.Is<string>(s => s.Contains("unavailable")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void IsAdminOnly_IsFalse()
    {
        _command.IsAdminOnly.Should().BeFalse();
    }
}
