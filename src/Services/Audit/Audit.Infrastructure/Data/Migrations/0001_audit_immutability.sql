-- Applied after the initial audit schema migration/creation.
-- The application role can append and read, but cannot mutate history.
REVOKE UPDATE, DELETE ON TABLE audit."AuditEntries" FROM app_role;
