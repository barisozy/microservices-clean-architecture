-- Applied after the initial audit schema migration/creation.
-- The application role can append and read, but cannot mutate history.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'audit_runtime') THEN
        RAISE EXCEPTION 'Required audit application role audit_runtime does not exist';
    END IF;

    REVOKE UPDATE, DELETE ON TABLE audit."AuditEntries" FROM audit_runtime;
END $$;
