namespace TicketSystem.Api.Middleware;

/// <summary>
/// Reads or generates a correlation ID for the request. Validates any incoming header as a GUID
/// to prevent log-injection (CWE-117) — untrusted client input must never flow verbatim into logs.
/// Sets the ID in <c>HttpContext.Items</c>, into the Serilog log context, and into the response header.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemsKey = "CorrelationId";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        string id;
        // Reject: missing, multi-valued (indicates duplicate headers from proxy + client),
        // non-GUID (log-injection defense — CWE-117), and Guid.Empty (would collapse
        // correlation across every client that sends all-zeros).
        if (ctx.Request.Headers.TryGetValue(HeaderName, out var raw)
            && raw.Count == 1
            && Guid.TryParse(raw.ToString(), out var parsed)
            && parsed != Guid.Empty)
        {
            id = parsed.ToString();
        }
        else
        {
            id = Guid.NewGuid().ToString();
        }

        ctx.Items[ItemsKey] = id;
        ctx.Response.OnStarting(() =>
        {
            ctx.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        });

        using (Serilog.Context.LogContext.PushProperty("CorrelationId", id))
        {
            await _next(ctx);
        }
    }
}
