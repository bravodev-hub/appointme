using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppointMe.Crm.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerRegistrationDateIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Customers_CompanyId_RegistrationDate",
                schema: "crm",
                table: "Customers",
                columns: new[] { "CompanyId", "RegistrationDate" },
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_CompanyId_RegistrationDate",
                schema: "crm",
                table: "Customers");
        }
    }
}
