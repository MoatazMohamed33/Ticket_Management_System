using MediatR;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Common.Paging;

namespace TicketSystem.Application.Features.AdminUsers.Queries.ListUsers;

public sealed class ListUsersQueryHandler : IRequestHandler<ListUsersQuery, PagedResult<AdminUserDto>>
{
    private readonly IAdminUserQueries _queries;

    public ListUsersQueryHandler(IAdminUserQueries queries) => _queries = queries;

    public Task<PagedResult<AdminUserDto>> Handle(ListUsersQuery q, CancellationToken ct) =>
        _queries.ListAsync(q.Page, q.PageSize, q.Role, q.Search, q.SortBy, q.SortDirection, ct);
}
