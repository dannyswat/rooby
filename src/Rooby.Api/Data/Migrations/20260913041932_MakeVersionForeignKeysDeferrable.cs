using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rooby.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeVersionForeignKeysDeferrable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Publish flips the parent Version row's (profile_id, version_id) key and the child
            // item/item_line/test_case rows' version_id in the same transaction; Postgres checks
            // non-deferrable FKs per-statement, which rejects that intermediate state. Deferring to
            // COMMIT lets the whole flip land consistently.
            migrationBuilder.Sql("ALTER TABLE item ALTER CONSTRAINT fk_item_versions_profile_id_version_id DEFERRABLE INITIALLY DEFERRED;");
            migrationBuilder.Sql("ALTER TABLE item_line ALTER CONSTRAINT fk_item_line_versions_profile_id_version_id DEFERRABLE INITIALLY DEFERRED;");
            migrationBuilder.Sql("ALTER TABLE test_case ALTER CONSTRAINT fk_test_case_versions_profile_id_version_id DEFERRABLE INITIALLY DEFERRED;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE item ALTER CONSTRAINT fk_item_versions_profile_id_version_id NOT DEFERRABLE;");
            migrationBuilder.Sql("ALTER TABLE item_line ALTER CONSTRAINT fk_item_line_versions_profile_id_version_id NOT DEFERRABLE;");
            migrationBuilder.Sql("ALTER TABLE test_case ALTER CONSTRAINT fk_test_case_versions_profile_id_version_id NOT DEFERRABLE;");
        }
    }
}
