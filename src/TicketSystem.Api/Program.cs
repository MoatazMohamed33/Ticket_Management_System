using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using TicketSystem.Api.Logging;
using TicketSystem.Api.Middleware;
using TicketSystem.Api.Swagger;
using TicketSystem.Application;
using TicketSystem.Application.Abstractions.Security;
using TicketSystem.Infrastructure;
using TicketSystem.Infrastructure.Persistence;
using TicketSystem.Infrastructure.Persistence.Seeding;

var builder = WebApplication.CreateBuilder(args);

// --- Serilog ---------------------------------------------------------------
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Destructure.With<SensitivePropertyDestructuringPolicy>()
    .WriteTo.Console(new Serilog.Formatting.Compact.RenderedCompactJsonFormatter()));

// --- Application + Infrastructure DI --------------------------------------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

// --- SignalR (Story 7.1) ---------------------------------------------------
builder.Services.AddSignalR();
builder.Services.AddScoped<
    TicketSystem.Application.Abstractions.Realtime.ITicketBroadcaster,
    TicketSystem.Api.Realtime.TicketBroadcaster>();

// ICurrentUser (Api-layer impl over HttpContextAccessor).
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<
    TicketSystem.Application.Abstractions.Security.ICurrentUser,
    TicketSystem.Api.Auth.CurrentUser>();

// --- Authentication + Authorization (Story 2.6) --------------------------
// CRITICAL — legacy JwtSecurityTokenHandler auto-maps standard JWT claim names to
// Microsoft URI claim types on inbound tokens: `sub` → ClaimTypes.NameIdentifier,
// which means `HttpContext.User.FindFirst("sub")` returns null and Serilog's
// UserId enricher shows "anonymous" for every authenticated request. Clear the map
// BEFORE AddJwtBearer to preserve raw claim names end-to-end.
System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var jwtOpts = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt section missing");
if (string.IsNullOrWhiteSpace(jwtOpts.SigningKey))
    throw new InvalidOperationException("Jwt:SigningKey missing");
byte[] jwtKeyBytes;
try { jwtKeyBytes = Convert.FromBase64String(jwtOpts.SigningKey); }
catch (FormatException) { throw new InvalidOperationException("Jwt:SigningKey must be base64-encoded"); }
if (jwtKeyBytes.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must decode to at least 32 bytes for HS256");

builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.MapInboundClaims = false;   // reinforces the DefaultInboundClaimTypeMap.Clear() above
        opts.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOpts.Issuer,
            ValidAudience = jwtOpts.Audience,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(jwtKeyBytes),
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub,
        };

        opts.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            // Story 7.1 — SignalR WebSocket connections can't set Authorization header,
            // so accept the token via ?access_token=... on /hubs/* paths. HTTP endpoints
            // and non-hub paths still require the header.
            OnMessageReceived = ctx =>
            {
                if (string.IsNullOrEmpty(ctx.Token))
                {
                    var accessToken = ctx.Request.Query["access_token"].FirstOrDefault();
                    var path = ctx.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    {
                        ctx.Token = accessToken;
                    }
                }
                return Task.CompletedTask;
            },

            OnAuthenticationFailed = ctx =>
            {
                var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var reason = ctx.Exception switch
                {
                    Microsoft.IdentityModel.Tokens.SecurityTokenExpiredException => "expired",
                    Microsoft.IdentityModel.Tokens.SecurityTokenInvalidSignatureException => "invalid-signature",
                    Microsoft.IdentityModel.Tokens.SecurityTokenValidationException => "invalid-token",
                    _ => "unknown"
                };
                logger.LogWarning(ctx.Exception, "JWT authentication failed. Reason={Reason}", reason);
                return Task.CompletedTask;
            },

            // C7 FIX — a JWT is valid for up to 15 minutes after issue. If an admin deactivates
            // a user in that window, the deactivated user's token still works. Look up IsActive
            // and DeletedAt on every authenticated request (extra DB hit per request — trade-off
            // vs shortening TTL further). Skip if unauthenticated.
            OnTokenValidated = async ctx =>
            {
                var subValue = ctx.Principal?.FindFirst(
                    System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
                if (!Guid.TryParse(subValue, out var userId))
                {
                    ctx.Fail("Invalid subject claim.");
                    return;
                }

                var db = ctx.HttpContext.RequestServices
                    .GetRequiredService<TicketSystem.Infrastructure.Persistence.AppDbContext>();
                var active = await db.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId && u.IsActive && u.DeletedAt == null)
                    .Select(u => (bool?)true)
                    .FirstOrDefaultAsync(ctx.HttpContext.RequestAborted);

                if (active is not true)
                {
                    ctx.Fail("User is not active.");
                }
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Deny-by-default: any endpoint without an explicit [AllowAnonymous] requires auth.
    // The architecture test in Story 2.6 Task 3 enforces per-method attribution as a
    // guardrail against class-level [AllowAnonymous] silently short-circuiting this.
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// --- MVC / Controllers / ProblemDetails / CORS ---------------------------
builder.Services.AddControllers().AddJsonOptions(o =>
{
    // camelCase everywhere — so ProblemDetails "errors" extension keys are camelCase
    // and Angular Reactive Forms can map them back to controls without case conversion.
    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.JsonSerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;

    // C1 FIX — serialize enums as strings so Angular can compare against string role names.
    // Without this, UserRole.Admin serializes as an integer and hasAnyRole(['Admin']) is
    // always false — every admin route silently 403s at the client-side guard.
    o.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter());
});
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        if (ctx.HttpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemsKey, out var cid))
        {
            ctx.ProblemDetails.Extensions["correlationId"] = cid;
        }
    };
});

// --- Rate limiter (auth policy — used by Register/Login/Refresh) ----------
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Per-IP partition — NOT a shared global bucket. One caller must not rate-limit everyone.
    // C6 FIX — behind a proxy, RemoteIpAddress is the proxy IP. Prefer X-Forwarded-For
    // (populated by UseForwardedHeaders configured below). Falls back to RemoteIpAddress.
    o.AddPolicy("auth", ctx =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: (ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                            ?? ctx.Connection.RemoteIpAddress?.ToString()
                            ?? "anon"),
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));

    // Story 3.1 — admin write endpoints partitioned by user's sub claim (not IP), so admins
    // behind corporate NAT don't throttle each other. Fallback matches auth policy pattern.
    o.AddPolicy("admin-write", ctx =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: (ctx.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                           ?? ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                           ?? ctx.Connection.RemoteIpAddress?.ToString()
                           ?? "anon"),
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));

    // Story 7.3 — split ticket-create policy into three, aligned to PRD limits:
    //   ticket-create      → POST /api/tickets                                (30/hour/user)
    //   comment-create     → POST /api/tickets/{id}/comments + /close         (60/hour/user)
    //   time-entry-create  → POST /api/tickets/{id}/time-entries              (60/hour/user)
    // admin-write covers all staff mutations (agent status change + admin priority/assign).
    // NOTE on admin-write naming: also covers Agent ChangeStatus writes — mildly
    // misnamed as of Story 7.3, but renaming would churn Story 3.1 endpoints. Growth-cleanup.
    o.AddPolicy("ticket-create",     UserOrIpPartition(30, TimeSpan.FromHours(1)));
    o.AddPolicy("comment-create",    UserOrIpPartition(60, TimeSpan.FromHours(1)));
    o.AddPolicy("time-entry-create", UserOrIpPartition(60, TimeSpan.FromHours(1)));

    o.OnRejected = async (context, ct) =>
    {
        var retryAfterSeconds = 60;
        if (context.Lease.TryGetMetadata(
                System.Threading.RateLimiting.MetadataName.RetryAfter, out var retry))
        {
            retryAfterSeconds = (int)retry.TotalSeconds;
            context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
        }

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/problem+json";

        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Type = "https://httpstatuses.io/429",
            Title = "Too many requests.",
            Status = StatusCodes.Status429TooManyRequests,
            Detail = $"Rate limit exceeded. Try again after {retryAfterSeconds} seconds.",
        };
        if (context.HttpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemsKey, out var cid))
            problem.Extensions["correlationId"] = cid;

        await context.HttpContext.Response.WriteAsJsonAsync(problem, ct);
    };
});

// Shared fallback partition helper: JWT sub → X-Forwarded-For → RemoteIpAddress → "anon".
// Returns a Func<HttpContext, RateLimitPartition<string>> so it composes with AddPolicy.
static Func<HttpContext, System.Threading.RateLimiting.RateLimitPartition<string>>
    UserOrIpPartition(int permitLimit, TimeSpan window) =>
    ctx =>
    {
        var key = ctx.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                  ?? ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                  ?? ctx.Connection.RemoteIpAddress?.ToString()
                  ?? "anon";
        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: key,
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    };

var webOrigin = builder.Configuration["Cors:WebOrigin"] ?? "http://localhost:4200";
builder.Services.AddCors(o => o.AddPolicy("Web", p => p
    .WithOrigins(webOrigin)
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowCredentials()));

// --- Swagger / OpenAPI ----------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Ticket Management API",
        Version = "v1",
        Description = "See PRD for full spec."
    });
    options.UseInlineDefinitionsForEnums();

    foreach (var xmlPath in EnumerateXmlDocPaths())
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste your JWT access token (Swagger will send it as `Authorization: Bearer <token>`)."
    });

    options.OperationFilter<AuthorizeCheckOperationFilter>();
});

var app = builder.Build();

// --- Migrations + Seed (Dev only) -----------------------------------------
// Fail-fast policy: if migrations cannot be applied on Dev startup we throw and let
// the orchestrator restart the container. Reporting healthy while broken (F4 in the
// code review) is the anti-pattern this replaces.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Unconditional migrate — no-op when zero migrations exist, correct behavior when they do.
    await db.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(db, hasher, logger);
}

// --- Middleware pipeline (order is authoritative) --------------------------
// C6 FIX — process X-Forwarded-* before anything else so RemoteIpAddress and the
// rate-limiter partition key reflect the real client, not the proxy.
app.UseForwardedHeaders(new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                     | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto,
    // In dev + Docker Compose we don't know the proxy network — clear KnownNetworks/Proxies
    // so headers are accepted from any source (fine for dev; prod should restrict).
    KnownNetworks = { },
    KnownProxies = { },
});

// CorrelationId FIRST so every subsequent middleware (including SerilogRequestLogging's
// completion log line) sees the ID in LogContext. Fixing C1 from Epic 1 code review.
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseSerilogRequestLogging(opts =>
{
    opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0} ms";
    opts.EnrichDiagnosticContext = (diag, http) =>
    {
        diag.Set("UserId", http.User?.FindFirst("sub")?.Value ?? "anonymous");
        diag.Set("RouteTemplate",
            (http.GetEndpoint() as Microsoft.AspNetCore.Routing.RouteEndpoint)?.RoutePattern?.RawText
            ?? http.Request.Path.Value);
    };
});

app.UseRouting();

// CORS BEFORE the exception middleware AND the rate limiter so 4xx/429 responses
// carry the CORS headers — otherwise the browser sees a network error and the Angular
// client cannot surface the ProblemDetails body (Epic 1 C8, Story 2.2 pipeline pin).
app.UseCors("Web");

app.UseRateLimiter();

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Swagger BEFORE authentication/authorization: without this, the Story 2.6 FallbackPolicy
// (RequireAuthenticatedUser) rejects unauthenticated requests to /swagger/* with 401 —
// but Swagger UI has no auth. Swagger middleware handles its own routes and short-circuits;
// requests for /api/* pass through to Auth as normal.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/swagger/v1/swagger.json", "Ticket Management API v1");
        o.RoutePrefix = "swagger";
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<TicketSystem.Api.Realtime.TicketsHub>("/hubs/tickets");   // Story 7.1

await app.RunAsync();

// --- Local helpers --------------------------------------------------------
static IEnumerable<string> EnumerateXmlDocPaths()
{
    var dir = AppContext.BaseDirectory;
    foreach (var xml in Directory.EnumerateFiles(dir, "TicketSystem.*.xml"))
    {
        yield return xml;
    }
}

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> in integration tests can boot the API.</summary>
public partial class Program { }
