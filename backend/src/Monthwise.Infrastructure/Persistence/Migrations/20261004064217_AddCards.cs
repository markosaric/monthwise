using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Monthwise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bank",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_predefined = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bank", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "card_issuer",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_predefined = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_card_issuer", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "card",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    issuer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_through = table.Column<DateOnly>(type: "date", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    debt_limit_cents = table.Column<long>(type: "bigint", nullable: true),
                    card_number_last4 = table.Column<string>(type: "character(4)", fixedLength: true, maxLength: 4, nullable: true),
                    iban_country = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true),
                    iban_last4 = table.Column<string>(type: "character(4)", fixedLength: true, maxLength: 4, nullable: true),
                    blocked_on = table.Column<DateOnly>(type: "date", nullable: true),
                    block_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    expired_on = table.Column<DateOnly>(type: "date", nullable: true),
                    expiry_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    archived_on = table.Column<DateOnly>(type: "date", nullable: true),
                    replaced_by_card_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    card_number = table.Column<string>(type: "text", nullable: true),
                    iban = table.Column<string>(type: "text", nullable: true),
                    card_number_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    iban_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_card", x => x.id);
                    table.CheckConstraint("ck_card_debt_limit_not_negative", "debt_limit_cents >= 0");
                    table.CheckConstraint("ck_card_has_number", "card_number IS NOT NULL OR iban IS NOT NULL");
                    table.CheckConstraint("ck_card_number_last4", "card_number_last4 ~ '^[0-9]{4}$'");
                    table.CheckConstraint("ck_card_primary_is_debit", "NOT is_primary OR type = 'Debit'");
                    table.CheckConstraint("ck_card_validity_order", "valid_through >= valid_from");
                    table.ForeignKey(
                        name: "fk_card_banks_bank_id",
                        column: x => x.bank_id,
                        principalTable: "bank",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_card_card_issuers_issuer_id",
                        column: x => x.issuer_id,
                        principalTable: "card_issuer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_card_card_replaced_by_card_id",
                        column: x => x.replaced_by_card_id,
                        principalTable: "card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "bank",
                columns: new[] { "id", "is_predefined", "name" },
                values: new object[,]
                {
                    { new Guid("0199b0c0-0000-7000-8000-000000000101"), true, "ZaBa" },
                    { new Guid("0199b0c0-0000-7000-8000-000000000102"), true, "PBZ" },
                    { new Guid("0199b0c0-0000-7000-8000-000000000103"), true, "Erste" },
                    { new Guid("0199b0c0-0000-7000-8000-000000000104"), true, "OTP" },
                    { new Guid("0199b0c0-0000-7000-8000-000000000105"), true, "RBA" },
                    { new Guid("0199b0c0-0000-7000-8000-000000000106"), true, "Addiko" }
                });

            migrationBuilder.InsertData(
                table: "card_issuer",
                columns: new[] { "id", "is_predefined", "name" },
                values: new object[,]
                {
                    { new Guid("0199b0c0-0000-7000-8000-000000000001"), true, "Visa" },
                    { new Guid("0199b0c0-0000-7000-8000-000000000002"), true, "Mastercard" },
                    { new Guid("0199b0c0-0000-7000-8000-000000000003"), true, "Diners" },
                    { new Guid("0199b0c0-0000-7000-8000-000000000004"), true, "Maestro" },
                    { new Guid("0199b0c0-0000-7000-8000-000000000005"), true, "American Express" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_card_bank_id",
                table: "card",
                column: "bank_id");

            migrationBuilder.CreateIndex(
                name: "ix_card_card_number_hash",
                table: "card",
                column: "card_number_hash",
                unique: true,
                filter: "card_number_hash IS NOT NULL AND archived_on IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_card_iban_hash",
                table: "card",
                column: "iban_hash",
                unique: true,
                filter: "iban_hash IS NOT NULL AND archived_on IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_card_issuer_id",
                table: "card",
                column: "issuer_id");

            migrationBuilder.CreateIndex(
                name: "ix_card_replaced_by_card_id",
                table: "card",
                column: "replaced_by_card_id");

            // Names are unique ignoring case (not expressible in the EF model).
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_card_name_lower ON card (lower(name));");
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_card_issuer_name_lower ON card_issuer (lower(name));");
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_bank_name_lower ON bank (lower(name));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "card");

            migrationBuilder.DropTable(
                name: "bank");

            migrationBuilder.DropTable(
                name: "card_issuer");
        }
    }
}
