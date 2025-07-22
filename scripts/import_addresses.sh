#!/usr/bin/env sh

# Load .env variables
. ./.env

# Check if input file is passed
if [ -z "$1" ]; then
  echo "Usage: ./import_addresses.sh <addresses.csv>"
  exit 1
fi

FILE="$1"

# Copy CSV file into container
docker cp "$FILE" "$PG_CONTAINER":/tmp/addresses.csv

# Execute SQL in container
PGPASSWORD="$DB_PWD" docker exec -i "$PG_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" <<'SQL'
CREATE TEMP TABLE tmp_addresses ("ADDRESS" TEXT);
\copy tmp_addresses("ADDRESS") FROM '/tmp/addresses.csv' WITH (FORMAT csv);
INSERT INTO "Addresses" ("ADDRESS", "ChainId", "NAME_LAST_UPDATED_UNIX_SECONDS", "TOTAL_SOUL_AMOUNT")
SELECT t."ADDRESS", 1, 0, 0
FROM tmp_addresses t
WHERE NOT EXISTS (
    SELECT 1 FROM "Addresses" a WHERE a."ADDRESS" = t."ADDRESS" AND a."ChainId" = 1
);
DROP TABLE tmp_addresses;
DELETE FROM "GlobalVariables" WHERE "NAME" = 'BALANCE_REFETCH_TIMESTAMP';
SQL

# Clean up copied file
docker exec "$PG_CONTAINER" rm /tmp/addresses.csv
