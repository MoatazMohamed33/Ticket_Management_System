using TicketSystem.Application.Abstractions.Security;

namespace TicketSystem.Infrastructure.Security;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
