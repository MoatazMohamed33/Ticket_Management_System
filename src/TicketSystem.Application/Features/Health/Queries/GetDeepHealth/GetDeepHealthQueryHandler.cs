using System.Diagnostics;
using System.Reflection;
using MediatR;
using TicketSystem.Application.Abstractions.Persistence;

namespace TicketSystem.Application.Features.Health.Queries.GetDeepHealth;

public sealed class GetDeepHealthQueryHandler : IRequestHandler<GetDeepHealthQuery, HealthDetailDto>
{
    private readonly IDatabaseProbe _probe;

    public GetDeepHealthQueryHandler(IDatabaseProbe probe) => _probe = probe;

    public async Task<HealthDetailDto> Handle(GetDeepHealthQuery request, CancellationToken ct)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

        var sw = Stopwatch.StartNew();
        var reachable = await _probe.CanConnectAsync(ct);
        sw.Stop();

        return new HealthDetailDto(version, reachable, sw.ElapsedMilliseconds, DateTimeOffset.UtcNow);
    }
}
