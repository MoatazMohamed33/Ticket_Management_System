using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace TicketSystem.ArchitectureTests;

/// <summary>
/// Guardrails for the fallback authorization policy (Story 2.6).
/// Prevents the two silent-auth-bypass patterns identified during story validation:
///   1. A controller action forgotten without [Authorize] or [AllowAnonymous]
///      — the fallback policy would silently protect it, but auditors need explicit intent.
///   2. Class-level [AllowAnonymous] on a controller — adds IAllowAnonymous metadata to
///      every action, short-circuiting any method-level [Authorize] and silently opening
///      those endpoints. Ban outright.
/// </summary>
public class AuthorizationTests
{
    private static readonly Assembly Api = typeof(Program).Assembly;

    [Fact]
    public void Every_controller_action_has_method_level_Authorize_or_AllowAnonymous()
    {
        var offenders = Api.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
            .Where(m =>
                !m.GetCustomAttributes<AuthorizeAttribute>().Any() &&
                !m.GetCustomAttributes<AllowAnonymousAttribute>().Any())
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .ToList();

        offenders.Should().BeEmpty(
            "every controller action must have a method-level [Authorize] or [AllowAnonymous] " +
            "attribute — class-level [AllowAnonymous] silently opens every action");
    }

    [Fact]
    public void No_controller_has_class_level_AllowAnonymous()
    {
        var offenders = Api.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => t.GetCustomAttributes<AllowAnonymousAttribute>().Any())
            .Select(t => t.Name)
            .ToList();

        offenders.Should().BeEmpty(
            "class-level [AllowAnonymous] adds IAllowAnonymous metadata to every action and " +
            "short-circuits sibling method-level [Authorize] — move to per-method");
    }
}
