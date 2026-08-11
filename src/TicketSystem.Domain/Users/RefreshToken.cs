namespace TicketSystem.Domain.Users;

/// <summary>
/// Server-side record of a refresh token. We store the SHA-256 hash of the raw token,
/// never the raw value. FamilyId groups all tokens descended from a single login so
/// theft-detection can invalidate the whole chain in one UPDATE.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public Guid FamilyId { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }

    // Optimistic concurrency — Story 2.4 relies on this to prevent two concurrent
    // refreshes on the same token from both minting valid chains.
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation
    public User? User { get; set; }
}
