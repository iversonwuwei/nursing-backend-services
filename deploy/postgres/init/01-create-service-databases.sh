#!/bin/bash
set -euo pipefail

databases=(
  nursing_elder
  nursing_health
  nursing_care
  nursing_visit
  nursing_billing
  nursing_notification
  nursing_operations
  nursing_config
  nursing_ai
)

for database in "${databases[@]}"; do
  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres <<-EOSQL
    SELECT 'CREATE DATABASE ${database}'
    WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = '${database}')\gexec
EOSQL
done