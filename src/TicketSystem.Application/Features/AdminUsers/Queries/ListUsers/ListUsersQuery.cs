using MediatR;
using TicketSystem.Application.Common.Paging;
using TicketSystem.Domain.Users;

namespace TicketSystem.Application.Features.AdminUsers.Queries.ListUsers;

/// <summary>
/// Query bound from [FromQuery]. NOTE: query-string enum binding is case-sensitive by
/// default in .NET 8 — `?role=Admin` works, `?role=admin` returns 400 bind failure.
/// (JsonStringEnumConverter only applies to JSON body deserialization.)
/// </summary>
public sealed record ListUsersQuery(
    int Page = 1,
    int PageSize = 20,
    UserRole? Role = null,
    string? Search = null,
    string SortBy = "createdAt",
    string SortDirection = "desc") : IRequest<PagedResult<AdminUserDto>>;
