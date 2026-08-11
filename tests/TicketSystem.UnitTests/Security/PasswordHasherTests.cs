using FluentAssertions;
using TicketSystem.Infrastructure.Security;

namespace TicketSystem.UnitTests.Security;

public class PasswordHasherTests
{
    private readonly PasswordHasher _sut = new();

    [Fact]
    public void Hash_produces_verifiable_output()
    {
        var hash = _sut.Hash("Passw0rd!");
        _sut.Verify("Passw0rd!", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_rejects_wrong_password()
    {
        var hash = _sut.Hash("Passw0rd!");
        _sut.Verify("wrong", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_produces_distinct_output_across_calls_per_user_salt()
    {
        var a = _sut.Hash("Passw0rd!");
        var b = _sut.Hash("Passw0rd!");
        a.Should().NotBe(b, "each call must generate a fresh salt");
    }

    [Fact]
    public void Verify_against_malformed_hash_returns_false_without_throwing()
    {
        // Needed for Story 2.3's timing-attack defense — Login handler calls Verify()
        // against a dummy hash on lookup-miss. If that throws, the whole login path breaks.
        var act = () => _sut.Verify("Passw0rd!", "not-a-real-hash-format");
        act.Should().NotThrow();
    }
}
