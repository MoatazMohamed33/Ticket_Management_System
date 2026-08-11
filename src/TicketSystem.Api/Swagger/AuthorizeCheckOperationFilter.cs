using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TicketSystem.Api.Swagger;

/// <summary>
/// Adds the Bearer security requirement only to operations that carry <c>[Authorize]</c>
/// (and not on any that carry <c>[AllowAnonymous]</c>). Prevents the Swagger UI from
/// showing a misleading padlock icon on public endpoints like /api/health.
/// </summary>
public sealed class AuthorizeCheckOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasAuthorize =
            context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any() == true
            || context.MethodInfo.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any();

        // [AllowAnonymous] can appear at method OR class level — check both, or padlocks
        // show on public endpoints (e.g., HealthController which uses class-level).
        var hasAllowAnonymous =
            context.MethodInfo.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any()
            || context.MethodInfo.DeclaringType?.GetCustomAttributes(true)
                .OfType<AllowAnonymousAttribute>().Any() == true;

        if (!hasAuthorize || hasAllowAnonymous)
        {
            return;
        }

        operation.Security = new List<OpenApiSecurityRequirement>
        {
            new()
            {
                [new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    }
                ] = Array.Empty<string>()
            }
        };
    }
}
