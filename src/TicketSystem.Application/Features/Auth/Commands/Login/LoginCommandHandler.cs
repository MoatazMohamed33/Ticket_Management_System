using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Application.Common.Exceptions;
using TicketSystem.Domain.Users;

namespace TicketSystem.Application.Features.Auth.Commands.Login;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponseDto>
{
    // Precomputed once per process on first use. A real Identity-produced hash of a random,
    // unusable password so Verify() takes ~constant time whether or not the user exists —
    // defeats email-enumeration by response-time analysis (see Dev Notes in Story 2.3).
    // Computed via IPasswordHasher (production impl uses ASP.NET Identity) rather than
    // referencing Microsoft.AspNetCore.Identity directly (keeps Application EF/Identity free).
    private static string? _dummyHash;
    private static readonly object _dummyHashLock = new();

    private static string GetDummyHash(IPasswordHasher hasher)
    {
        if (_dummyHash is not null) return _dummyHash;
        lock (_dummyHashLock)
        {
            _dummyHash ??= hasher.Hash(Guid.NewGuid().ToString());
        }
        return _dummyHash;
    }

    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IRefreshTokenGenerator _refresh;
    private readonly IClock _clock;
    private readonly IOptions<JwtOptions> _opts;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUnitOfWork uow,
        IPasswordHasher hasher,
        IJwtTokenGenerator jwt,
        IRefreshTokenGenerator refresh,
        IClock clock,
        IOptions<JwtOptions> opts,
        ILogger<LoginCommandHandler> logger)
    {
        _uow = uow;
        _hasher = hasher;
        _jwt = jwt;
        _refresh = refresh;
        _clock = clock;
        _opts = opts;
        _logger = logger;
    }

    public async Task<AuthResponseDto> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var normalized = cmd.Email.Trim().ToUpperInvariant();

        var user = await _uow.Repository<User>()
            .FirstOrDefaultNoTrackingAsync(u => u.EmailNormalized == normalized, ct);

        // CRITICAL — always call Verify. On lookup miss (user is null), verify against a
        // precomputed dummy hash so timing does not leak whether the email exists.
        var hashToCheck = user?.PasswordHash ?? GetDummyHash(_hasher);
        var passwordOk = _hasher.Verify(cmd.Password, hashToCheck);

        if (user is null || !user.IsActive || !passwordOk)
        {
            _logger.LogWarning("Failed login attempt for {Email}", cmd.Email);
            throw new InvalidCredentialsException();
        }

        var (accessToken, accessExp) = _jwt.GenerateAccessToken(user);
        var (rawRefresh, refreshHash) = _refresh.Generate();
        var refreshExp = _clock.UtcNow.AddDays(_opts.Value.RefreshTokenDays);

        var refreshEntity = new Domain.Users.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshHash,
            IssuedAt = _clock.UtcNow,
            ExpiresAt = refreshExp,
            FamilyId = Guid.NewGuid(),
        };

        await _uow.Repository<Domain.Users.RefreshToken>().AddAsync(refreshEntity, ct);
        await _uow.SaveChangesAsync(ct);

        return new AuthResponseDto(
            accessToken,
            rawRefresh,
            accessExp,
            refreshExp,
            new UserDto(user.Id, user.Email, user.DisplayName, user.Role));
    }
}
