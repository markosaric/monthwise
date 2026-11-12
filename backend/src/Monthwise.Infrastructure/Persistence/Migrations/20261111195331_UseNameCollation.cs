using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monthwise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UseNameCollation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:CollationDefinition:monthwise_name", "und-u-ks-level2,und-u-ks-level2,icu,False");

            // FR-6: names that are the same under the new rule stop the update before anything changes,
            // and the error names them so the user can rename one and start Monthwise again.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE duplicates text;
                BEGIN
                    SELECT string_agg(list || ': ' || names, '; ' ORDER BY list) INTO duplicates FROM (
                    SELECT 'card' AS list, string_agg(quote_literal(name), ', ' ORDER BY name) AS names FROM card GROUP BY name COLLATE monthwise_name HAVING count(*) > 1
                    UNION ALL SELECT 'card_issuer' AS list, string_agg(quote_literal(name), ', ' ORDER BY name) AS names FROM card_issuer GROUP BY name COLLATE monthwise_name HAVING count(*) > 1
                    UNION ALL SELECT 'bank' AS list, string_agg(quote_literal(name), ', ' ORDER BY name) AS names FROM bank GROUP BY name COLLATE monthwise_name HAVING count(*) > 1
                    UNION ALL SELECT 'payer' AS list, string_agg(quote_literal(name), ', ' ORDER BY name) AS names FROM payer GROUP BY name COLLATE monthwise_name HAVING count(*) > 1
                    UNION ALL SELECT 'input_category' AS list, string_agg(quote_literal(name), ', ' ORDER BY name) AS names FROM input_category GROUP BY name COLLATE monthwise_name HAVING count(*) > 1
                    UNION ALL SELECT 'loan_category' AS list, string_agg(quote_literal(name), ', ' ORDER BY name) AS names FROM loan_category GROUP BY name COLLATE monthwise_name HAVING count(*) > 1
                    UNION ALL SELECT 'loan_subcategory' AS list, string_agg(quote_literal(name), ', ' ORDER BY name) AS names FROM loan_subcategory GROUP BY category_id, name COLLATE monthwise_name HAVING count(*) > 1
                    UNION ALL SELECT 'loan' AS list, string_agg(quote_literal(name), ', ' ORDER BY name) AS names FROM loan WHERE closed_on IS NULL GROUP BY name COLLATE monthwise_name HAVING count(*) > 1
                    UNION ALL SELECT 'expense_category' AS list, string_agg(quote_literal(name), ', ' ORDER BY name) AS names FROM expense_category GROUP BY name COLLATE monthwise_name HAVING count(*) > 1
                    UNION ALL SELECT 'recurring_bill' AS list, string_agg(quote_literal(name), ', ' ORDER BY name) AS names FROM recurring_bill WHERE stopped_on IS NULL GROUP BY name COLLATE monthwise_name HAVING count(*) > 1
                    UNION ALL SELECT 'participant' AS list, string_agg(quote_literal(name), ', ' ORDER BY name) AS names FROM participant GROUP BY name COLLATE monthwise_name HAVING count(*) > 1
                    ) d;
                    IF duplicates IS NOT NULL THEN
                        RAISE EXCEPTION USING
                            ERRCODE = 'unique_violation',
                            MESSAGE = 'These names differ only in upper/lower case and must be renamed before this update: ' || duplicates;
                    END IF;
                END $$;
                """);

            // The lower(name) indexes give way to indexes on name, which use the column collation.
            migrationBuilder.Sql("DROP INDEX ux_card_name_lower;");
            migrationBuilder.Sql("DROP INDEX ux_card_issuer_name_lower;");
            migrationBuilder.Sql("DROP INDEX ux_bank_name_lower;");
            migrationBuilder.Sql("DROP INDEX ux_payer_name_lower;");
            migrationBuilder.Sql("DROP INDEX ux_input_category_name_lower;");
            migrationBuilder.Sql("DROP INDEX ux_loan_category_name_lower;");
            migrationBuilder.Sql("DROP INDEX ux_loan_subcategory_name_lower;");
            migrationBuilder.Sql("DROP INDEX ux_loan_name_lower;");
            migrationBuilder.Sql("DROP INDEX ux_expense_category_name_lower;");
            migrationBuilder.Sql("DROP INDEX ux_recurring_bill_name_lower;");
            migrationBuilder.Sql("DROP INDEX ux_participant_name_lower;");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "recurring_bill",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                collation: "monthwise_name",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "payer",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                collation: "monthwise_name",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "participant",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                collation: "monthwise_name",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "loan_subcategory",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                collation: "monthwise_name",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "loan_category",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                collation: "monthwise_name",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "loan",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                collation: "monthwise_name",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "input_category",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                collation: "monthwise_name",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "expense_category",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                collation: "monthwise_name",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "card_issuer",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                collation: "monthwise_name",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "card",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                collation: "monthwise_name",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "bank",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                collation: "monthwise_name",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "ux_recurring_bill_name",
                table: "recurring_bill",
                column: "name",
                unique: true,
                filter: "stopped_on IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_payer_name",
                table: "payer",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_participant_name",
                table: "participant",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_loan_subcategory_category_id_name",
                table: "loan_subcategory",
                columns: new[] { "category_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_loan_category_name",
                table: "loan_category",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_loan_name",
                table: "loan",
                column: "name",
                unique: true,
                filter: "closed_on IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_input_category_name",
                table: "input_category",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_expense_category_name",
                table: "expense_category",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_card_issuer_name",
                table: "card_issuer",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_card_name",
                table: "card",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_bank_name",
                table: "bank",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_recurring_bill_name",
                table: "recurring_bill");

            migrationBuilder.DropIndex(
                name: "ux_payer_name",
                table: "payer");

            migrationBuilder.DropIndex(
                name: "ux_participant_name",
                table: "participant");

            migrationBuilder.DropIndex(
                name: "ux_loan_subcategory_category_id_name",
                table: "loan_subcategory");

            migrationBuilder.DropIndex(
                name: "ux_loan_category_name",
                table: "loan_category");

            migrationBuilder.DropIndex(
                name: "ux_loan_name",
                table: "loan");

            migrationBuilder.DropIndex(
                name: "ux_input_category_name",
                table: "input_category");

            migrationBuilder.DropIndex(
                name: "ux_expense_category_name",
                table: "expense_category");

            migrationBuilder.DropIndex(
                name: "ux_card_issuer_name",
                table: "card_issuer");

            migrationBuilder.DropIndex(
                name: "ux_card_name",
                table: "card");

            migrationBuilder.DropIndex(
                name: "ux_bank_name",
                table: "bank");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "recurring_bill",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldCollation: "monthwise_name");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "payer",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldCollation: "monthwise_name");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "participant",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldCollation: "monthwise_name");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "loan_subcategory",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldCollation: "monthwise_name");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "loan_category",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldCollation: "monthwise_name");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "loan",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldCollation: "monthwise_name");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "input_category",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldCollation: "monthwise_name");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "expense_category",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldCollation: "monthwise_name");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "card_issuer",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldCollation: "monthwise_name");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "card",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldCollation: "monthwise_name");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "bank",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldCollation: "monthwise_name");

            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_card_name_lower ON card (lower(name));");
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_card_issuer_name_lower ON card_issuer (lower(name));");
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_bank_name_lower ON bank (lower(name));");
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_payer_name_lower ON payer (lower(name));");
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_input_category_name_lower ON input_category (lower(name));");
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_loan_category_name_lower ON loan_category (lower(name));");
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_loan_subcategory_name_lower ON loan_subcategory (category_id, lower(name));");
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_loan_name_lower ON loan (lower(name)) WHERE closed_on IS NULL;");
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_expense_category_name_lower ON expense_category (lower(name));");
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_recurring_bill_name_lower ON recurring_bill (lower(name)) WHERE stopped_on IS NULL;");
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_participant_name_lower ON participant (lower(name));");

            // The collation goes last, once no column uses it.
            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:CollationDefinition:monthwise_name", "und-u-ks-level2,und-u-ks-level2,icu,False");
        }
    }
}
