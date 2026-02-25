using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    public partial class Update33_UseBigintForBlockAndChainHeights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Cast text heights to bigint explicitly to avoid implicit-cast failures on PostgreSQL.
            migrationBuilder.Sql(
                """
                ALTER TABLE "Chains" ALTER COLUMN "CURRENT_HEIGHT" DROP DEFAULT;
                UPDATE "Chains"
                SET "CURRENT_HEIGHT" = '0'
                WHERE "CURRENT_HEIGHT" IS NULL
                   OR "CURRENT_HEIGHT" = ''
                   OR "CURRENT_HEIGHT" !~ '^[0-9]+$';
                ALTER TABLE "Chains"
                    ALTER COLUMN "CURRENT_HEIGHT" TYPE bigint
                    USING "CURRENT_HEIGHT"::bigint;
                ALTER TABLE "Chains" ALTER COLUMN "CURRENT_HEIGHT" SET DEFAULT 0;
                ALTER TABLE "Chains" ALTER COLUMN "CURRENT_HEIGHT" SET NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "Blocks" ALTER COLUMN "HEIGHT" DROP DEFAULT;
                UPDATE "Blocks"
                SET "HEIGHT" = '0'
                WHERE "HEIGHT" IS NULL
                   OR "HEIGHT" = ''
                   OR "HEIGHT" !~ '^[0-9]+$';
                ALTER TABLE "Blocks"
                    ALTER COLUMN "HEIGHT" TYPE bigint
                    USING "HEIGHT"::bigint;
                ALTER TABLE "Blocks" ALTER COLUMN "HEIGHT" SET DEFAULT 0;
                ALTER TABLE "Blocks" ALTER COLUMN "HEIGHT" SET NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Chains" ALTER COLUMN "CURRENT_HEIGHT" DROP DEFAULT;
                ALTER TABLE "Chains"
                    ALTER COLUMN "CURRENT_HEIGHT" TYPE text
                    USING "CURRENT_HEIGHT"::text;
                ALTER TABLE "Chains" ALTER COLUMN "CURRENT_HEIGHT" DROP NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "Blocks" ALTER COLUMN "HEIGHT" DROP DEFAULT;
                ALTER TABLE "Blocks"
                    ALTER COLUMN "HEIGHT" TYPE text
                    USING "HEIGHT"::text;
                ALTER TABLE "Blocks" ALTER COLUMN "HEIGHT" DROP NOT NULL;
                """);
        }
    }
}
