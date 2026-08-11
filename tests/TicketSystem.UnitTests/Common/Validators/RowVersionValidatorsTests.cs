using FluentAssertions;
using FluentValidation;
using TicketSystem.Application.Common.Validators;

namespace TicketSystem.UnitTests.Common.Validators;

public class RowVersionValidatorsTests
{
    private sealed record Request(string RowVersion);

    private sealed class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator() => RuleFor(r => r.RowVersion).ValidBase64();
    }

    private readonly RequestValidator _sut = new();

    [Theory]
    [InlineData("AAAAAAAAB9E=")]  // typical 8-byte SQL Server rowversion base64
    [InlineData("dGVzdA==")]      // "test"
    [InlineData("AA==")]           // 1-byte
    public void ValidBase64_accepts_wellformed_base64(string value)
    {
        _sut.Validate(new Request(value)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidBase64_rejects_empty(string value)
    {
        var result = _sut.Validate(new Request(value));
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("not base64!")]      // invalid characters
    [InlineData("A")]                 // wrong length (< 4)
    [InlineData("AAA")]               // wrong length (not multiple of 4)
    [InlineData("****")]              // invalid base64 alphabet
    public void ValidBase64_rejects_malformed_input_with_expected_message(string value)
    {
        var result = _sut.Validate(new Request(value));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "rowVersion is malformed.");
    }
}
