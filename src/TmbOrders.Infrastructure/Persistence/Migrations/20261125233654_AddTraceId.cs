using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TmbOrders.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTraceId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "trace_id",
                table: "pedidos",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "trace_id",
                table: "pedidos");
        }
    }
}
