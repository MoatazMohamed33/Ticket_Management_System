namespace TicketSystem.IntegrationTests;

/// <summary>
/// xUnit collection definition — all integration test classes decorated with
/// <c>[Collection("Api")]</c> share ONE <see cref="ApiFactory"/> (one SQL Testcontainer)
/// instead of spinning up a fresh container per class (which would OOM CI runners).
/// </summary>
[CollectionDefinition("Api")]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    // Marker class only — xUnit reads the attribute + interface via reflection.
}
