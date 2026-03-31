using Microsoft.Extensions.Logging.Abstractions;
using MonoBot.Abstractions;
using MonoBot.Abstractions.Data;
using MonoBot.Plugin.Announcements;

namespace MonoBot.Tests.Commands;

public class AnnouncementsCommandTests
{
    private readonly IAnnouncementRepository _repo = Substitute.For<IAnnouncementRepository>();
    private readonly IMessageSender _sender = Substitute.For<IMessageSender>();
    private readonly AnnouncementsCommand _command;

    public AnnouncementsCommandTests()
    {
        _command = new AnnouncementsCommand(_repo, NullLogger<AnnouncementsCommand>.Instance);
    }

    [Fact]
    public async Task Execute_WithAnnouncements_SendsEachOneToChannel()
    {
        _repo.GetRecentAsync("#test", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Announcement>
            {
                new("2024-01-01T10:00:00Z", "First announcement"),
                new("2024-01-02T10:00:00Z", "Second announcement")
            });

        await _command.ExecuteAsync("#test", "nick", null, _sender);

        await _sender.Received(2).SendChannelMessageAsync(
            "#test", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_EmptyList_SendsNoAnnouncementsMessage()
    {
        _repo.GetRecentAsync("#test", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Announcement>());

        await _command.ExecuteAsync("#test", "nick", null, _sender);

        await _sender.Received(1).SendChannelMessageAsync(
            "#test",
            Arg.Is<string>(s => s.Contains("No announcements")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_MessageContainsDateAndText()
    {
        _repo.GetRecentAsync("#test", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Announcement>
            {
                new("2024-06-15T00:00:00Z", "Summer party!")
            });

        await _command.ExecuteAsync("#test", "nick", null, _sender);

        // Regression: original code read GetString(0) twice — both date and text must appear.
        await _sender.Received(1).SendChannelMessageAsync(
            "#test",
            Arg.Is<string>(s => s.Contains("2024-06-15") && s.Contains("Summer party!")),
            Arg.Any<CancellationToken>());
    }
}
