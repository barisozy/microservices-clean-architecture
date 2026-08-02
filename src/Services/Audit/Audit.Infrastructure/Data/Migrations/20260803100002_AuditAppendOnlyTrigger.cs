using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Audit.Infrastructure.Data.Migrations;

[Migration("20260803100002_AuditAppendOnlyTrigger")]
public partial class AuditAppendOnlyTrigger : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_role') THEN
                    RAISE EXCEPTION 'Required audit application role app_role does not exist';
                END IF;
                REVOKE UPDATE, DELETE ON TABLE audit."AuditEntries" FROM app_role;
            END $$;

            CREATE OR REPLACE FUNCTION audit.prevent_audit_entry_mutation()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $fn$
            BEGIN
                RAISE EXCEPTION 'AuditEntries is append-only';
            END;
            $fn$;

            CREATE TRIGGER audit_entries_append_only
                BEFORE UPDATE OR DELETE ON audit."AuditEntries"
                FOR EACH ROW EXECUTE FUNCTION audit.prevent_audit_entry_mutation();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS audit_entries_append_only ON audit."AuditEntries";
            DROP FUNCTION IF EXISTS audit.prevent_audit_entry_mutation();
            """);
    }
}
