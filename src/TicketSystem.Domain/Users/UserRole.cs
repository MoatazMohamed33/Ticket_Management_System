namespace TicketSystem.Domain.Users;

/// <summary>
/// The three fixed roles in the system. Values start at 1 so the default(UserRole) = 0
/// is never a valid role — surfaces uninitialized-enum bugs immediately.
/// </summary>
public enum UserRole
{
    Admin = 1,
    SupportAgent = 2,
    Customer = 3
}
