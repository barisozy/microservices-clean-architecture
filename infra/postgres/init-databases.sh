#!/bin/sh
set -eu

: "${DB_RUNTIME_PASSWORD:?DB_RUNTIME_PASSWORD is required}"
: "${DB_MIGRATION_PASSWORD:?DB_MIGRATION_PASSWORD is required}"

create_db_and_roles() {
  local db=$1
  local prefix=$2
  
  local runtime_role="${prefix}_runtime"
  local migration_role="${prefix}_migration"

  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres <<-EOSQL
    SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', '$runtime_role', '$DB_RUNTIME_PASSWORD')
    WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '$runtime_role') \gexec

    SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', '$migration_role', '$DB_MIGRATION_PASSWORD')
    WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '$migration_role') \gexec

    SELECT 'CREATE DATABASE "$db" OWNER "$migration_role"' 
    WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = '$db') \gexec
EOSQL

  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$db" <<-EOSQL
    CREATE SCHEMA IF NOT EXISTS "$prefix" AUTHORIZATION "$migration_role";
    GRANT USAGE ON SCHEMA "$prefix" TO "$runtime_role";
    
    ALTER DEFAULT PRIVILEGES FOR ROLE "$migration_role" IN SCHEMA "$prefix" 
      GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO "$runtime_role";
    ALTER DEFAULT PRIVILEGES FOR ROLE "$migration_role" IN SCHEMA "$prefix" 
      GRANT USAGE, SELECT ON SEQUENCES TO "$runtime_role";
EOSQL
}

create_db_and_roles "Order_db" "order"
create_db_and_roles "inventory_db" "inventory"
create_db_and_roles "Payment_db" "payment"
create_db_and_roles "fulfillment_db" "fulfillment"
create_db_and_roles "iam_db" "iam"
create_db_and_roles "catalog_db" "catalog"
create_db_and_roles "customer_db" "customer"
create_db_and_roles "search_db" "search"
create_db_and_roles "notification_db" "notification"
create_db_and_roles "promotion_db" "promotion"
create_db_and_roles "Audit_db" "audit"

# Special immutable role configuration for Audit as per AGENTS.md
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "Audit_db" <<-EOSQL
    ALTER DEFAULT PRIVILEGES FOR ROLE "audit_migration" IN SCHEMA "audit" 
      REVOKE UPDATE, DELETE ON TABLES FROM "audit_runtime";
EOSQL
