#!/bin/sh
set -eu

: "${AUDIT_APP_PASSWORD:?AUDIT_APP_PASSWORD is required}"

psql -v ON_ERROR_STOP=1 \
  --username "$POSTGRES_USER" \
  --dbname postgres \
  --set=audit_password="$AUDIT_APP_PASSWORD" <<-'EOSQL'
SELECT format('CREATE ROLE app_role LOGIN PASSWORD %L', :'audit_password')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'app_role') \gexec

SELECT 'CREATE DATABASE "Order_db"' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'Order_db') \gexec
SELECT 'CREATE DATABASE inventory_db' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'inventory_db') \gexec
SELECT 'CREATE DATABASE "Payment_db"' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'Payment_db') \gexec
SELECT 'CREATE DATABASE fulfillment_db' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'fulfillment_db') \gexec
SELECT 'CREATE DATABASE iam_db' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'iam_db') \gexec
SELECT 'CREATE DATABASE catalog_db' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'catalog_db') \gexec
SELECT 'CREATE DATABASE customer_db' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'customer_db') \gexec
SELECT 'CREATE DATABASE search_db' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'search_db') \gexec
SELECT 'CREATE DATABASE notification_db' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'notification_db') \gexec
SELECT 'CREATE DATABASE promotion_db' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'promotion_db') \gexec
SELECT 'CREATE DATABASE "Audit_db" OWNER app_role' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'Audit_db') \gexec
EOSQL
