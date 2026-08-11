namespace TicketSystem.DataIsolationTests.Infrastructure;

/// <summary>
/// Every test class in this project shares ONE <see cref="IsolationApiFactory"/> — one
/// SQL Testcontainer, one seed. Decorate with <c>[Collection("Isolation")]</c>.
/// </summary>
[CollectionDefinition("Isolation")]
public sealed class IsolationCollection : ICollectionFixture<IsolationApiFactory> { }
