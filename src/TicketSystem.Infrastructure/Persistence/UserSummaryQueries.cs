using Microsoft.EntityFrameworkCore;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Features.Tickets;

namespace TicketSystem.Infrastructure.Persistence;

public sealed class UserSummaryQueries : IUserSummaryQueries
{
    private readonly AppDbContext _db;
    public UserSummaryQueries(AppDbContext db) => _db = db;

    public Task<UserSummaryDto?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserSummaryDto(u.Id, u.DisplayName))
            .FirstOrDefaultAsync(ct);
}
