using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace TicketSystem.DataIsolationTests.Infrastructure;

/// <summary>
/// Convenience helpers for authenticating an HttpClient against the seeded users. Every
/// isolation test starts with "authenticate as X" — extracting into one place keeps the
/// test body focused on the isolation assertion.
/// </summary>
internal static class TestAuth
{
    public const string DemoPassword = "Passw0rd!";

    public static async Task LoginAsync(HttpClient client, string email, string password = DemoPassword)
    {
        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>()
            ?? throw new InvalidOperationException($"Login for {email} returned no body.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);
    }

    private sealed record LoginResponse(string AccessToken, string RefreshToken);
}
