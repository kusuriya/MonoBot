using Microsoft.Extensions.Logging.Abstractions;
using MonoBot.Abstractions;
using MonoBot.Abstractions.Data;
using MonoBot.Plugin.Announcements;

namespace MonoBot.Tests.Commands;

public class AddAnnouncementCommandTests
{
    private readonly IAnnouncementRepository _repo = Substitute.For<IAnnouncementRepository>();
    private readonly IMessageSender _sender = Substitute.For<IMessageSender>();
    private readonly AddAnnouncementCommand _command;

    public AddAnnouncementCommandTests()
    {
        _command = new AddAnnouncementCommand(_repo, NullLogger<AddAnnouncementCommand>.Instance);
    }

    [Fact]
    public async Task Execute_NullOptions_SendsUsageAndSkipsRepository()
    {
        await _command.ExecuteAsync("#test", "nick", null, _sender);

        await _repo.DidNotReceive().AddAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _sender.Received(1).SendChannelMessageAsync(
            "#test", Arg.Is<string>(s => s.Contains("Usage")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_EmptyOptions_SendsUsageAndSkipsRepository()
    {
        await _command.ExecuteAsync("#test", "nick", "   ", _sender);

        await _repo.DidNotReceive().AddAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_TooLongText_RejectsAndSkipsRepository()
    {
        var longText = new string('x', 501);
        await _command.ExecuteAsync("#test", "nick", longText, _sender);

        await _repo.DidNotReceive().AddAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _sender.Received(1).SendChannelMessageAsync(
            "#test", Arg.Is<string>(s => s.Contains("too long")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ValidText_CallsRepositoryWithChannelAndText()
    {
        await _command.ExecuteAsync("#test", "nick", "Deploy is tonight at 9pm", _sender);

        await _repo.Received(1).AddAsync("#test", "Deploy is tonight at 9pm", Arg.Any<CancellationToken>());
        await _sender.Received(1).SendChannelMessageAsync(
            "#test", Arg.Is<string>(s => s.Contains("added")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_RepositoryThrows_SendsErrorAndDoesNotCrash()
    {
        _repo.AddAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("disk full"));

        var act = async () => await _command.ExecuteAsync("#test", "nick", "some text", _sender);
        await act.Should().NotThrowAsync();

        await _sender.Received(1).SendChannelMessageAsync(
            "#test", Arg.Is<string>(s => s.Contains("not") || s.Contains("Could")), Arg.Any<CancellationToken>());
    }
}
