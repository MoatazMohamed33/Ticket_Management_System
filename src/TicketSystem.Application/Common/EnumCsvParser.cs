namespace TicketSystem.Application.Common;

/// <summary>
/// Shared helper for query-string filter fields that accept comma-separated enum tokens
/// (e.g., <c>?status=Open,InProgress</c>). Unknown tokens are silently dropped, matching
/// the tolerant-of-typos philosophy applied elsewhere (Story 3.2 unknown-sortBy fallback).
/// </summary>
public static class EnumCsvParser
{
    public static T[]? ParseEnumList<T>(string? csv) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(csv)) return null;
        var parsed = csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => Enum.TryParse<T>(token, ignoreCase: true, out var v) ? (T?)v : null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToArray();
        return parsed.Length == 0 ? null : parsed;
    }
}
