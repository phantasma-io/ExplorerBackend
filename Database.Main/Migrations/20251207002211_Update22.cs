using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Main.Migrations
{
    public partial class Update22 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<JsonDocument>(
                name: "METADATA",
                table: "Serieses",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "METADATA",
                table: "Nfts",
                type: "jsonb",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE ""Nfts"" AS n
                SET ""METADATA"" = jsonb_strip_nulls(
                    coalesce(n.""METADATA"", '{}'::jsonb) ||
                    coalesce(
                        (
                            SELECT jsonb_object_agg(p->>'key', p->>'value')
                            FROM jsonb_array_elements(coalesce(n.""CHAIN_API_RESPONSE"", '{}'::jsonb)->'properties') AS p
                        ),
                        '{}'::jsonb
                    ) ||
                    jsonb_build_object(
                        'name', n.""NAME"",
                        'description', n.""DESCRIPTION"",
                        'image', n.""IMAGE"",
                        'video', n.""VIDEO"",
                        'info_url', n.""INFO_URL"",
                        'rom', n.""ROM"",
                        'ram', n.""RAM"",
                        'mint_date', nullif(n.""MINT_DATE_UNIX_SECONDS"", 0),
                        'mint_number', nullif(n.""MINT_NUMBER"", 0)
                    )
                )
                WHERE n.""METADATA"" IS NULL OR n.""METADATA"" = '{}'::jsonb;
            ");

            migrationBuilder.Sql(@"
                UPDATE ""Serieses"" AS s
                SET ""METADATA"" = jsonb_strip_nulls(
                    coalesce(s.""METADATA"", '{}'::jsonb) ||
                    jsonb_build_object(
                        'name', s.""NAME"",
                        'description', s.""DESCRIPTION"",
                        'image', s.""IMAGE"",
                        'royalties', s.""ROYALTIES"",
                        'type', nullif(s.""TYPE"", 0),
                        'attr_type_1', s.""ATTR_TYPE_1"",
                        'attr_value_1', s.""ATTR_VALUE_1"",
                        'attr_type_2', s.""ATTR_TYPE_2"",
                        'attr_value_2', s.""ATTR_VALUE_2"",
                        'attr_type_3', s.""ATTR_TYPE_3"",
                        'attr_value_3', s.""ATTR_VALUE_3"",
                        'current_supply', nullif(s.""CURRENT_SUPPLY"", 0),
                        'max_supply', nullif(s.""MAX_SUPPLY"", 0)
                    ) ||
                    coalesce(
                        (
                            SELECT jsonb_build_object('mode_name', sm.""MODE_NAME"")
                            FROM ""SeriesModes"" AS sm
                            WHERE sm.""ID"" = s.""SeriesModeId""
                        ),
                        '{}'::jsonb
                    )
                )
                WHERE s.""METADATA"" IS NULL OR s.""METADATA"" = '{}'::jsonb;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "METADATA",
                table: "Serieses");

            migrationBuilder.DropColumn(
                name: "METADATA",
                table: "Nfts");
        }
    }
}
