namespace TicketSystem.Application.Common.Paging;

/// <summary>Generic page envelope for list endpoints. Reused across features.</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long TotalCount,
    int TotalPages);
