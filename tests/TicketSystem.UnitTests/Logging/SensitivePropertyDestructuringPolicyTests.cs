using FluentAssertions;
using Serilog;
using Serilog.Sinks.InMemory;
using TicketSystem.Api.Logging;

namespace TicketSystem.UnitTests.Logging;

public class SensitivePropertyDestructuringPolicyTests
{
    [Fact]
    public void Sensitive_property_values_are_redacted()
    {
        var sink = new InMemorySink();
        var logger = new LoggerConfiguration()
            .Destructure.With<SensitivePropertyDestructuringPolicy>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var credential = new { Email = "user@example.com", Password = "hunter2", Token = "abc.def.ghi" };
        logger.Information("Login attempt {@Credential}", credential);

        var evt = sink.LogEvents.Single();
        var rendered = evt.RenderMessage() + " | " +
                       string.Join(" | ", evt.Properties.Select(p => $"{p.Key}={p.Value}"));

        rendered.Should().Contain("user@example.com");
        rendered.Should().NotContain("hunter2");
        rendered.Should().NotContain("abc.def.ghi");
        rendered.Should().Contain("***REDACTED***");
    }
}
