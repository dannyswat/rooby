using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

namespace Rooby.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.CreateTable(
                name: "project",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_disabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    last_modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    remark = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    login_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    login_provider = table.Column<int>(type: "integer", nullable: false),
                    display_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    is_disabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    last_modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "version_schema",
                columns: table => new
                {
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<int>(type: "integer", nullable: false),
                    schema_id = table.Column<Guid>(type: "uuid", nullable: false),
                    definition = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    validity = table.Column<NpgsqlRange<DateOnly>>(type: "daterange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_version_schema", x => new { x.profile_id, x.version_id, x.schema_id });
                });

            migrationBuilder.CreateTable(
                name: "profile",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    publish_uri = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    publish_secret = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    api_key_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    api_key_rotated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    api_key_rotated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    next_stash_id = table.Column<int>(type: "integer", nullable: false, defaultValue: -2),
                    test_gate = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    is_disabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    last_modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    remark = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_profile", x => x.id);
                    table.ForeignKey(
                        name: "fk_profile_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "schema",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    validity = table.Column<NpgsqlRange<DateOnly>>(type: "daterange", nullable: false),
                    definition = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    last_modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schema", x => x.id);
                    table.ForeignKey(
                        name: "fk_schema_project_project_id",
                        column: x => x.project_id,
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_access",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    access_level = table.Column<int>(type: "integer", nullable: false),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    last_modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_by_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_access", x => x.id);
                    table.CheckConstraint("ck_user_access_scope", "project_id IS NOT NULL OR profile_id IS NULL");
                    table.ForeignKey(
                        name: "fk_user_access_users_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "version",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<int>(type: "integer", nullable: false),
                    from_version_id = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_version", x => x.id);
                    table.UniqueConstraint("ak_versions_profile_id_version_id", x => new { x.profile_id, x.version_id });
                    table.CheckConstraint("ck_version_ids", "version_id <> 0 AND from_version_id >= 0");
                    table.ForeignKey(
                        name: "fk_version_profile_profile_id",
                        column: x => x.profile_id,
                        principalTable: "profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<int>(type: "integer", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    item_type = table.Column<int>(type: "integer", nullable: false),
                    data_type = table.Column<int>(type: "integer", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    schema_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    content = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    last_modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item", x => new { x.id, x.version_id });
                    table.ForeignKey(
                        name: "fk_item_versions_profile_id_version_id",
                        columns: x => new { x.profile_id, x.version_id },
                        principalTable: "version",
                        principalColumns: new[] { "profile_id", "version_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<int>(type: "integer", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    schema_id = table.Column<Guid>(type: "uuid", nullable: true),
                    inherit_schema = table.Column<bool>(type: "boolean", nullable: false),
                    validity = table.Column<NpgsqlRange<DateOnly>>(type: "daterange", nullable: true),
                    remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    last_modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_line", x => new { x.id, x.version_id });
                    table.ForeignKey(
                        name: "fk_item_line_versions_profile_id_version_id",
                        columns: x => new { x.profile_id, x.version_id },
                        principalTable: "version",
                        principalColumns: new[] { "profile_id", "version_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "test_case",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<int>(type: "integer", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_key = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    input_data = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    output_value = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    remarks = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    last_modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_modified_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_test_case", x => new { x.id, x.version_id });
                    table.ForeignKey(
                        name: "fk_test_case_versions_profile_id_version_id",
                        columns: x => new { x.profile_id, x.version_id },
                        principalTable: "version",
                        principalColumns: new[] { "profile_id", "version_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_item_profile_id_key_draft",
                table: "item",
                columns: new[] { "profile_id", "key" },
                unique: true,
                filter: "version_id = -1");

            migrationBuilder.CreateIndex(
                name: "ix_item_profile_id_key_version_id",
                table: "item",
                columns: new[] { "profile_id", "key", "version_id" });

            migrationBuilder.CreateIndex(
                name: "ix_item_profile_id_version_id",
                table: "item",
                columns: new[] { "profile_id", "version_id" });

            migrationBuilder.CreateIndex(
                name: "ix_item_line_profile_id_item_id_version_id",
                table: "item_line",
                columns: new[] { "profile_id", "item_id", "version_id" });

            migrationBuilder.CreateIndex(
                name: "ix_item_line_profile_id_version_id",
                table: "item_line",
                columns: new[] { "profile_id", "version_id" });

            migrationBuilder.CreateIndex(
                name: "ix_profile_project_id_code",
                table: "profile",
                columns: new[] { "project_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_code",
                table: "project",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_schema_project_id",
                table: "schema",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_test_case_profile_id_item_key",
                table: "test_case",
                columns: new[] { "profile_id", "item_key" });

            migrationBuilder.CreateIndex(
                name: "ix_test_case_profile_id_version_id",
                table: "test_case",
                columns: new[] { "profile_id", "version_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_access_user_id",
                table: "user_access",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_version_profile_id_version_id",
                table: "version",
                columns: new[] { "profile_id", "version_id" },
                unique: true);

            // Fluent API has no equivalent for PostgreSQL exclusion constraints or expression indexes.
            migrationBuilder.Sql(
                "ALTER TABLE schema ADD CONSTRAINT ex_schema_project_id_code_validity " +
                "EXCLUDE USING gist (project_id WITH =, code WITH =, validity WITH &&);");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_user_login_name_lower ON \"user\" (lower(login_name));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_user_login_name_lower;");
            migrationBuilder.Sql("ALTER TABLE schema DROP CONSTRAINT IF EXISTS ex_schema_project_id_code_validity;");

            migrationBuilder.DropTable(
                name: "item");

            migrationBuilder.DropTable(
                name: "item_line");

            migrationBuilder.DropTable(
                name: "schema");

            migrationBuilder.DropTable(
                name: "test_case");

            migrationBuilder.DropTable(
                name: "user_access");

            migrationBuilder.DropTable(
                name: "version_schema");

            migrationBuilder.DropTable(
                name: "version");

            migrationBuilder.DropTable(
                name: "user");

            migrationBuilder.DropTable(
                name: "profile");

            migrationBuilder.DropTable(
                name: "project");
        }
    }
}
