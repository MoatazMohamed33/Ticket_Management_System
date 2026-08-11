namespace TicketSystem.Application.Abstractions.Security;

/// <summary>
/// Password hashing abstraction. Implemented in Infrastructure using ASP.NET Core Identity's default hasher.
/// Introduced early so <see cref="TicketSystem.Application.Abstractions.Persistence.IUnitOfWork"/> consumers
/// (and future <c>DatabaseSeeder</c>) can depend on the contract before Story 2.1 lands the concrete type.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
