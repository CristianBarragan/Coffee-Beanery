using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Database.Banking.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerCustomerRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InnerCustomerCustomerRelationshipCustomerId",
                schema: "Banking",
                table: "Customer",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InnerCustomerCustomerRelationshipCustomerKey",
                schema: "Banking",
                table: "Customer",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OuterCustomerCustomerRelationshipCustomerId",
                schema: "Banking",
                table: "Customer",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OuterCustomerCustomerRelationshipCustomerKey",
                schema: "Banking",
                table: "Customer",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerCustomerRelationship",
                schema: "Banking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerCustomerRelationshipKey = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessedDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerCustomerRelationship", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerCustomerRelationshipCustomer",
                schema: "Banking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerCustomerRelationshipCustomerKey = table.Column<Guid>(type: "uuid", nullable: false),
                    OuterCustomerKey = table.Column<Guid>(type: "uuid", nullable: true),
                    OuterCustomerId = table.Column<int>(type: "integer", nullable: true),
                    InnerCustomerKey = table.Column<Guid>(type: "uuid", nullable: true),
                    InnerCustomerId = table.Column<int>(type: "integer", nullable: true),
                    CustomerCustomerRelationshipKey = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerCustomerRelationshipId = table.Column<int>(type: "integer", nullable: true),
                    ProcessedDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerCustomerRelationshipCustomer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerCustomerRelationshipCustomer_CustomerCustomerRelati~",
                        column: x => x.CustomerCustomerRelationshipId,
                        principalSchema: "Banking",
                        principalTable: "CustomerCustomerRelationship",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CustomerCustomerRelationshipCustomer_Customer_InnerCustomer~",
                        column: x => x.InnerCustomerId,
                        principalSchema: "Banking",
                        principalTable: "Customer",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CustomerCustomerRelationshipCustomer_Customer_OuterCustomer~",
                        column: x => x.OuterCustomerId,
                        principalSchema: "Banking",
                        principalTable: "Customer",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCustomerRelationship_CustomerCustomerRelationshipKey",
                schema: "Banking",
                table: "CustomerCustomerRelationship",
                column: "CustomerCustomerRelationshipKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCustomerRelationshipCustomer_CustomerCustomerRelat~1",
                schema: "Banking",
                table: "CustomerCustomerRelationshipCustomer",
                column: "CustomerCustomerRelationshipId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCustomerRelationshipCustomer_CustomerCustomerRelati~",
                schema: "Banking",
                table: "CustomerCustomerRelationshipCustomer",
                column: "CustomerCustomerRelationshipCustomerKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCustomerRelationshipCustomer_InnerCustomerId",
                schema: "Banking",
                table: "CustomerCustomerRelationshipCustomer",
                column: "InnerCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCustomerRelationshipCustomer_OuterCustomerId",
                schema: "Banking",
                table: "CustomerCustomerRelationshipCustomer",
                column: "OuterCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCustomerRelationshipCustomer_OuterCustomerKey_Inner~",
                schema: "Banking",
                table: "CustomerCustomerRelationshipCustomer",
                columns: new[] { "OuterCustomerKey", "InnerCustomerKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerCustomerRelationshipCustomer",
                schema: "Banking");

            migrationBuilder.DropTable(
                name: "CustomerCustomerRelationship",
                schema: "Banking");

            migrationBuilder.DropColumn(
                name: "InnerCustomerCustomerRelationshipCustomerId",
                schema: "Banking",
                table: "Customer");

            migrationBuilder.DropColumn(
                name: "InnerCustomerCustomerRelationshipCustomerKey",
                schema: "Banking",
                table: "Customer");

            migrationBuilder.DropColumn(
                name: "OuterCustomerCustomerRelationshipCustomerId",
                schema: "Banking",
                table: "Customer");

            migrationBuilder.DropColumn(
                name: "OuterCustomerCustomerRelationshipCustomerKey",
                schema: "Banking",
                table: "Customer");
        }
    }
}
