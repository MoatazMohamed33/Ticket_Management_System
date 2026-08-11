using FluentAssertions;
using TicketSystem.Application.Common;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.UnitTests.Common;

public class EnumCsvParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(",,,")]
    public void ParseEnumList_returns_null_for_empty_input(string? csv)
    {
        EnumCsvParser.ParseEnumList<TicketStatus>(csv).Should().BeNull();
    }

    [Fact]
    public void ParseEnumList_parses_single_token()
    {
        EnumCsvParser.ParseEnumList<TicketStatus>("Open")
            .Should().BeEquivalentTo(new[] { TicketStatus.Open });
    }

    [Fact]
    public void ParseEnumList_parses_multiple_tokens_preserving_order()
    {
        EnumCsvParser.ParseEnumList<TicketStatus>("Open,InProgress")
            .Should().BeEquivalentTo(new[] { TicketStatus.Open, TicketStatus.InProgress },
                opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void ParseEnumList_is_case_insensitive()
    {
        EnumCsvParser.ParseEnumList<TicketStatus>("open,INPROGRESS,rEsOlVeD")
            .Should().BeEquivalentTo(new[]
            {
                TicketStatus.Open, TicketStatus.InProgress, TicketStatus.Resolved,
            });
    }

    [Fact]
    public void ParseEnumList_trims_whitespace_around_tokens()
    {
        EnumCsvParser.ParseEnumList<TicketStatus>("  Open  ,  InProgress  ")
            .Should().BeEquivalentTo(new[] { TicketStatus.Open, TicketStatus.InProgress });
    }

    [Fact]
    public void ParseEnumList_drops_unknown_tokens_silently()
    {
        EnumCsvParser.ParseEnumList<TicketStatus>("Open,Bogus,Resolved")
            .Should().BeEquivalentTo(new[] { TicketStatus.Open, TicketStatus.Resolved });
    }

    [Fact]
    public void ParseEnumList_returns_null_when_all_tokens_unknown()
    {
        EnumCsvParser.ParseEnumList<TicketStatus>("Nope,AlsoNope").Should().BeNull();
    }

    [Fact]
    public void ParseEnumList_deduplicates()
    {
        EnumCsvParser.ParseEnumList<TicketStatus>("Open,Open,InProgress,open")
            .Should().BeEquivalentTo(new[] { TicketStatus.Open, TicketStatus.InProgress });
    }

    [Fact]
    public void ParseEnumList_works_for_priority_enum_too()
    {
        EnumCsvParser.ParseEnumList<TicketPriority>("High,Critical")
            .Should().BeEquivalentTo(new[] { TicketPriority.High, TicketPriority.Critical });
    }
}
