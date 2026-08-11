using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NetArchTest.Rules;
using TicketSystem.Domain.Tickets;
using TicketSystem.Infrastructure.Persistence;

namespace TicketSystem.ArchitectureTests;

/// <summary>
/// Guardrails for Clean Architecture layering + DTO discipline.
/// Any change that violates these fails CI immediately.
/// </summary>
public class CleanArchitectureTests
{
    private static readonly Assembly Domain = typeof(TicketSystem.Domain.AssemblyMarker).Assembly;
    private static readonly Assembly Application = typeof(TicketSystem.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(TicketSystem.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    [Fact]
    public void Domain_should_not_depend_on_any_other_layer()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "TicketSystem.Application",
                "TicketSystem.Infrastructure",
                "TicketSystem.Api",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Domain must be dependency-free. Failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Application_should_not_depend_on_infrastructure_or_api()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny("TicketSystem.Infrastructure", "TicketSystem.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Application must not reference Infrastructure or Api. Failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Application_should_not_depend_on_entity_framework()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Application must stay EF-free — data access is abstracted behind IUnitOfWork/IGenericRepository. Failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Api_controllers_should_not_return_domain_entity_types()
    {
        // Convention: any Domain entity type must never appear as a public return type
        // on a controller method. Enforced by asserting no controller depends directly
        // on TicketSystem.Domain (they depend on Application DTOs which live in Application).
        var result = Types.InAssembly(Api)
            .That().ResideInNamespace("TicketSystem.Api.Controllers")
            .ShouldNot()
            .HaveDependencyOn("TicketSystem.Domain")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Controllers must return DTOs from Application, not entities from Domain. Failing types: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Ticket_RowVersion_is_mapped_as_concurrency_token()
    {
        // Build the model without opening a real DB connection. UseSqlServer sets the
        // provider so config like IsRowVersion resolves; no query is executed.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(local);Database=drift;Integrated Security=true;")
            .Options;
        using var ctx = new AppDbContext(options);

        var prop = ctx.Model.FindEntityType(typeof(Ticket))!
            .FindProperty(nameof(Ticket.RowVersion));
        prop.Should().NotBeNull();
        prop!.IsConcurrencyToken.Should().BeTrue(
            "FR20 requires optimistic concurrency on every ticket mutation — RowVersion is the token");
        prop.ValueGenerated.Should().Be(ValueGenerated.OnAddOrUpdate,
            "SQL Server rowversion is DB-generated on insert AND update");
    }

    [Fact]
    public void No_dto_type_exposes_password_or_hash_property()
    {
        // Guardrail: any future *Dto or *Response type in Application must never surface
        // credential material. Property-name scan catches the accidental
        // `public string PasswordHash { get; init; }` on a hand-rolled DTO.
        var offenders = Application.GetTypes()
            .Where(t => t.IsPublic && (t.Name.EndsWith("Dto") || t.Name.EndsWith("Response")))
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(p => p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase)
                     || p.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase))
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .ToList();

        offenders.Should().BeEmpty(
            "DTO properties must never expose credential material: {0}",
            string.Join(", ", offenders));
    }
}
