using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketClosedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "Tickets",
                type: "datetimeoffset",
                nullable: true);

            // Back-fill for pre-existing Closed rows — UpdatedAt is the best proxy since
            // ClosedAt wasn't tracked before this migration. Not exact but close enough for
            // the dashboard's averageResolutionMinutes purposes (Story 6.4). Documented
            // approximation; new closes going forward set ClosedAt precisely.
            migrationBuilder.Sql(
                "UPDATE [Tickets] SET [ClosedAt] = [UpdatedAt] WHERE [Status] = 4;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Tickets");
        }
    }
}
