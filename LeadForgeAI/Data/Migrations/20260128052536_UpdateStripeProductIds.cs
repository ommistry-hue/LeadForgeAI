using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadForgeAI.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStripeProductIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "StripePriceId", "StripeProductId" },
                values: new object[] { "price_1SuRCwSEwGBhjH9cdgTTwAtj", "prod_TsBZFdwA7zau09" });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "StripePriceId", "StripeProductId" },
                values: new object[] { "price_1SuRDCSEwGBhjH9cLhCYXQdr", "prod_TsBah3O2lsIIHJ" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "StripePriceId", "StripeProductId" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "StripePriceId", "StripeProductId" },
                values: new object[] { "", "" });
        }
    }
}
