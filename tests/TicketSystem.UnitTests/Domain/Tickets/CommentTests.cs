using FluentAssertions;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.UnitTests.Domain.Tickets;

public class CommentTests
{
    private static readonly DateTimeOffset _now = new(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_returns_comment_with_matching_fields()
    {
        var ticketId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var c = Comment.Create(ticketId, authorId, "hello", _now);

        c.Id.Should().NotBe(Guid.Empty);
        c.TicketId.Should().Be(ticketId);
        c.AuthorUserId.Should().Be(authorId);
        c.Body.Should().Be("hello");
        c.CreatedAt.Should().Be(_now);
    }

    [Fact]
    public void Create_trims_body()
    {
        var c = Comment.Create(Guid.NewGuid(), Guid.NewGuid(), "  hi  ", _now);
        c.Body.Should().Be("hi");
    }

    [Fact]
    public void Create_empty_ticketId_throws()
    {
        Action act = () => Comment.Create(Guid.Empty, Guid.NewGuid(), "body", _now);
        act.Should().Throw<ArgumentException>().WithMessage("*TicketId*");
    }

    [Fact]
    public void Create_empty_authorId_throws()
    {
        Action act = () => Comment.Create(Guid.NewGuid(), Guid.Empty, "body", _now);
        act.Should().Throw<ArgumentException>().WithMessage("*AuthorUserId*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_blank_body_throws(string? body)
    {
        Action act = () => Comment.Create(Guid.NewGuid(), Guid.NewGuid(), body!, _now);
        act.Should().Throw<ArgumentException>().WithMessage("*Body*");
    }
}
