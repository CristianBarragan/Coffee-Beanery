using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Graph.Banking.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE EXTENSION IF NOT EXISTS age;
                LOAD 'age';
                SET search_path = ag_catalog, ""$user"", public;
            ");
            
            migrationBuilder.Sql(@$"
                SELECT create_graph('{nameof(CustomerCustomerRelationshipEdge)}');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
