using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketSystem.Application.Abstractions.Persistence;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Infrastructure.Persistence;
using TicketSystem.Infrastructure.Security;

namespace TicketSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default is not configured. Set it via appsettings or env var 'ConnectionStrings__Default'.");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3));
            if (environment.IsDevelopment())
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDatabaseProbe, DatabaseProbe>();
        services.AddScoped<IAuthPersistence, AuthPersistence>();
        services.AddScoped<IAdminUserQueries, AdminUserQueries>();
        services.AddScoped<IMyTicketsQueries, MyTicketsQueries>();
        services.AddScoped<IMyAssignedTicketsQueries, MyAssignedTicketsQueries>();
        services.AddScoped<IAllTicketsQueries, AllTicketsQueries>();
        services.AddScoped<ITicketDetailQueries, TicketDetailQueries>();
        services.AddScoped<IUserSummaryQueries, UserSummaryQueries>();
        services.AddScoped<IDashboardQueries, DashboardQueries>();
        services.AddSingleton<TicketSystem.Application.Abstractions.Caching.IDashboardCacheInvalidator,
                              TicketSystem.Infrastructure.Caching.DashboardCacheInvalidator>();
        services.AddMemoryCache();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        // Story 2.3 — JWT + refresh token infrastructure
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.PostConfigure<JwtOptions>(o =>
        {
            if (string.IsNullOrWhiteSpace(o.SigningKey))
            {
                throw new InvalidOperationException(
                    "Jwt:SigningKey is missing. Set via Jwt__SigningKey env var or user-secrets.");
            }
            byte[] decoded;
            try { decoded = Convert.FromBase64String(o.SigningKey); }
            catch (FormatException)
            {
                throw new InvalidOperationException("Jwt:SigningKey must be base64-encoded.");
            }
            if (decoded.Length < 32)
            {
                throw new InvalidOperationException(
                    "Jwt:SigningKey must decode to at least 32 bytes (256 bits) for HS256.");
            }
        });

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();

        return services;
    }
}
