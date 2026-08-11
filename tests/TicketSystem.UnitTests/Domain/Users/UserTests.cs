using FluentAssertions;
using TicketSystem.Domain.Users;

namespace TicketSystem.UnitTests.Domain.Users;

public class UserTests
{
    // ---- Factory guards --------------------------------------------------

    [Fact]
    public void CreateCustomer_returns_active_Customer_with_normalized_email()
    {
        var u = User.CreateCustomer("Test@Example.COM", "Test User", "hash");

        u.Id.Should().NotBe(Guid.Empty);
        u.Email.Should().Be("Test@Example.COM");
        u.EmailNormalized.Should().Be("TEST@EXAMPLE.COM");
        u.DisplayName.Should().Be("Test User");
        u.Role.Should().Be(UserRole.Customer);
        u.PasswordHash.Should().Be("hash");
        u.IsActive.Should().BeTrue();
        u.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void CreateStaff_Admin_role_succeeds()
    {
        var u = User.CreateStaff("admin@x.com", "Admin", UserRole.Admin, "hash");
        u.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public void CreateStaff_SupportAgent_role_succeeds()
    {
        var u = User.CreateStaff("agent@x.com", "Agent", UserRole.SupportAgent, "hash");
        u.Role.Should().Be(UserRole.SupportAgent);
    }

    [Fact]
    public void CreateStaff_Customer_role_throws()
    {
        Action act = () => User.CreateStaff("c@x.com", "C", UserRole.Customer, "hash");
        act.Should().Throw<ArgumentException>().WithMessage("*CreateCustomer*Customer*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void CreateCustomer_blank_email_throws(string? email)
    {
        Action act = () => User.CreateCustomer(email!, "name", "hash");
        act.Should().Throw<ArgumentException>().WithMessage("*mail*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void CreateCustomer_blank_displayName_throws(string? name)
    {
        Action act = () => User.CreateCustomer("e@x.com", name!, "hash");
        act.Should().Throw<ArgumentException>().WithMessage("*name*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void CreateCustomer_blank_passwordHash_throws(string? hash)
    {
        Action act = () => User.CreateCustomer("e@x.com", "name", hash!);
        act.Should().Throw<ArgumentException>().WithMessage("*hash*");
    }

    // ---- Deactivate / Reactivate (Story 3.3) -----------------------------

    [Fact]
    public void Deactivate_flips_IsActive_to_false()
    {
        var u = User.CreateCustomer("e@x.com", "name", "hash");

        u.Deactivate();

        u.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_when_already_inactive_is_idempotent()
    {
        var u = User.CreateCustomer("e@x.com", "name", "hash");
        u.Deactivate();

        u.Deactivate();   // no throw, no state flip back

        u.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Reactivate_flips_IsActive_to_true()
    {
        var u = User.CreateCustomer("e@x.com", "name", "hash");
        u.Deactivate();

        u.Reactivate();

        u.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Reactivate_when_already_active_is_idempotent()
    {
        var u = User.CreateCustomer("e@x.com", "name", "hash");

        u.Reactivate();

        u.IsActive.Should().BeTrue();
    }

    // ---- ChangeRole (Story 3.4) ------------------------------------------

    [Fact]
    public void ChangeRole_updates_role()
    {
        var u = User.CreateStaff("a@x.com", "Agent", UserRole.SupportAgent, "hash");

        u.ChangeRole(UserRole.Admin);

        u.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public void ChangeRole_same_role_is_idempotent()
    {
        var u = User.CreateStaff("a@x.com", "Admin", UserRole.Admin, "hash");

        u.ChangeRole(UserRole.Admin);

        u.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public void ChangeRole_to_Customer_from_staff_is_allowed()
    {
        // Domain factory prevents CreateStaff with Customer, but ChangeRole to Customer
        // IS allowed (Story 3.4 supports demoting staff to Customer).
        var u = User.CreateStaff("a@x.com", "Agent", UserRole.SupportAgent, "hash");

        u.ChangeRole(UserRole.Customer);

        u.Role.Should().Be(UserRole.Customer);
    }
}
