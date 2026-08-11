namespace TicketSystem.Domain.Users;

public sealed class User
{
    // EF Core needs a parameterless ctor (can be private).
    private User() { }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = "";
    public string EmailNormalized { get; private set; } = "";
    public string PasswordHash { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public static User CreateCustomer(string email, string displayName, string passwordHash) =>
        Create(email, displayName, UserRole.Customer, passwordHash);

    public static User CreateStaff(string email, string displayName, UserRole role, string passwordHash)
    {
        if (role == UserRole.Customer)
        {
            throw new ArgumentException("Use CreateCustomer for Customer role.", nameof(role));
        }
        return Create(email, displayName, role, passwordHash);
    }

    /// <summary>Deactivate the account. Idempotent — no state change if already inactive.</summary>
    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
    }

    /// <summary>Reactivate a deactivated account. Idempotent — no state change if already active.</summary>
    public void Reactivate()
    {
        if (IsActive) return;
        IsActive = true;
    }

    /// <summary>Change the user's role. Idempotent — no state change if already that role.</summary>
    public void ChangeRole(UserRole newRole)
    {
        if (Role == newRole) return;
        Role = newRole;
    }

    private static User Create(string email, string displayName, UserRole role, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required.", nameof(displayName));
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("Password hash is required.", nameof(passwordHash));

        return new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            EmailNormalized = email.ToUpperInvariant(),
            DisplayName = displayName,
            Role = role,
            PasswordHash = passwordHash,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
