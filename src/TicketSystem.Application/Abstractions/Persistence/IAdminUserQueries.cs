using TicketSystem.Application.Common.Paging;
using TicketSystem.Application.Features.AdminUsers;
using TicketSystem.Domain.Users;

namespace TicketSystem.Application.Abstractions.Persistence;

/// <summary>
/// Admin-specific read queries. Kept out of <see cref="IGenericRepository{T}"/> because
/// they need EF-specific features (EF.Functions.Like for wildcard-escaped search).
/// Implemented in Infrastructure so Application stays free of EF Core.
/// </summary>
public interface IAdminUserQueries
{
    Task<PagedResult<AdminUserDto>> ListAsync(
        int page,
        int pageSize,
        UserRole? role,
        string? search,
        string sortBy,
        string sortDirection,
        CancellationToken ct = default);
}
