[private]
just:
    just -l

set dotenv-load
API_TMUX_SESSION_NAME := env("API_TMUX_SESSION_NAME")
WORKER_TMUX_SESSION_NAME := env("WORKER_TMUX_SESSION_NAME")
API_BIN_DIR := env("API_BIN_DIR")
WORKER_BIN_DIR := env("WORKER_BIN_DIR")
RELEASE_MODE := env("RELEASE_MODE")

DB_HOST_PORT := env("DB_HOST_PORT")
DB_USER := env("DB_USER")
DB_PWD := env("DB_PWD")
DB_NAME := env("DB_NAME")
DB_CLONE_NAME := env("DB_CLONE_NAME")
PG_CONTAINER := env("PG_CONTAINER")
DB_STATE_ZERO_BACKUP := env("DB_STATE_ZERO_BACKUP")
DB_BACKUP_DIR := env("DB_BACKUP_DIR")

FUNGIBLE_BALANCES_V1_EXPORT := env("FUNGIBLE_BALANCES_V1_EXPORT")

TIMESTAMP := `date "+%Y%m%d%H%M"`

# Builds ALL
[group('build')]
b:
    dotnet build ExplorerBackend.sln

[group('build')]
c:
    sh clean.sh

[group('publish')]
p:
    just pa
    just pw

[group('publish')]
pa:
    dotnet publish ./Backend.Service.Api/Backend.Service.Api.csproj \
        --configuration "$RELEASE_MODE" \
        --output "$API_BIN_DIR" \
        -p:UseAppHost=true #-v:diag > publish.log

[group('publish')]
pw:
    dotnet publish ./Backend.Service.Worker/Backend.Service.Worker.csproj \
        --configuration "$RELEASE_MODE" \
        --output "$WORKER_BIN_DIR" \
        -p:UseAppHost=true #-v:diag > publish.log
    dotnet publish ./Backend.Plugins/Blockchain.Common/Blockchain.Common.csproj --output "$WORKER_BIN_DIR/Plugins" --configuration "$RELEASE_MODE" -p:UseAppHost=true #-v:diag > publish.log
    dotnet publish ./Backend.Plugins/Blockchain.Img/Blockchain.Img.csproj --output "$WORKER_BIN_DIR/Plugins" --configuration "$RELEASE_MODE" -p:UseAppHost=true #-v:diag > publish.log
    dotnet publish ./Backend.Plugins/Blockchain.Phantasma/Blockchain.Phantasma.csproj --output "$WORKER_BIN_DIR/Plugins" --configuration "$RELEASE_MODE" -p:UseAppHost=true #-v:diag > publish.log
    dotnet publish ./Backend.Plugins/Nft.TTRS/Nft.TTRS.csproj --output "$WORKER_BIN_DIR/Plugins" --configuration "$RELEASE_MODE" -p:UseAppHost=true #-v:diag > publish.log
    dotnet publish ./Backend.Plugins/Price.CoinGecko/Price.CoinGecko.csproj --output "$WORKER_BIN_DIR/Plugins" --configuration "$RELEASE_MODE" -p:UseAppHost=true #-v:diag > publish.log
    dotnet publish ./Backend.Plugins/Price.ExchangeRatesApiIo/Price.ExchangeRatesApiIo.csproj --output "$WORKER_BIN_DIR/Plugins" --configuration "$RELEASE_MODE" -p:UseAppHost=true #-v:diag > publish.log

[group('run')]
ra0:
    just pa
    cd {{API_BIN_DIR}} && ./Backend.Service.Api

[group('run')]
ra:
    sh ./scripts/run_in_tmux.sh {{API_TMUX_SESSION_NAME}} "just ra0"

[group('run')]
rad:
    sh ./scripts/run_in_tmux.sh {{API_TMUX_SESSION_NAME}} "just ra0" --detached

[group('run')]
rw0:
    just pw
    cd {{WORKER_BIN_DIR}} && ./Backend.Service.Worker

[group('run')]
rw:
    sh ./scripts/run_in_tmux.sh {{WORKER_TMUX_SESSION_NAME}} "just rw0"

[group('run')]
rwd:
    sh ./scripts/run_in_tmux.sh {{WORKER_TMUX_SESSION_NAME}} "just rw0" --detached

[group('run')]
rd:
    just rad
    just rwd

# Stops API running in tmux
[group('run')]
stop-api:
    sh ./scripts/stop_tmux_process.sh {{API_TMUX_SESSION_NAME}} Backend.Service.Api

# Stops Worker running in tmux
[group('run')]
stop-worker:
    sh ./scripts/stop_tmux_process.sh {{WORKER_TMUX_SESSION_NAME}} Backend.Service.Worker

# Stops ALL services running in tmux
[group('run')]
stop:
    just stop-api
    just stop-worker

# Danger! Resets db to backed up initial one!
[group('manage')]
db-reset:
    @sh -eu -c 'printf "This will RESET Explorers STORAGE. Enter password to continue: "; stty -echo; read PASSWORD; stty echo; echo; [ "$PASSWORD" = "iddqd" ] || { echo "❌ Access denied."; exit 1; }; echo "✅ Proceeding..."'
    @date "+🕓 Started at %Y-%m-%d %H:%M:%S"
    echo "🔪 Killing active connections to DB..."
    PGPASSWORD={{DB_PWD}} docker exec -i {{PG_CONTAINER}} psql -U {{DB_USER}} -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='{{DB_NAME}}';"
    echo "🧹 Dropping DB..."
    PGPASSWORD={{DB_PWD}} docker exec -i {{PG_CONTAINER}} dropdb --username={{DB_USER}} {{DB_NAME}} >> {{DB_NAME}}.{{TIMESTAMP}}.log 2>&1
    echo "📦 Creating DB..."
    PGPASSWORD={{DB_PWD}} docker exec -i {{PG_CONTAINER}} createdb --username={{DB_USER}} {{DB_NAME}} >> {{DB_NAME}}.{{TIMESTAMP}}.log 2>&1
    echo "♻️  Restoring DB from {{DB_STATE_ZERO_BACKUP}}..."
    PGPASSWORD={{DB_PWD}} cat {{DB_STATE_ZERO_BACKUP}} | docker exec -i {{PG_CONTAINER}} pg_restore -U {{DB_USER}} -d {{DB_NAME}} -Fc >> {{DB_NAME}}.{{TIMESTAMP}}.log 2>&1
    @date "+✅ Done at %Y-%m-%d %H:%M:%S"

# Clones db
[group('manage')]
db-clone:
    @sh -eu -c 'printf "This will DESTROY current db clone and close all connections to db. Enter password to continue: "; stty -echo; read PASSWORD; stty echo; echo; [ "$PASSWORD" = "iddqd" ] || { echo "❌ Access denied."; exit 1; }; echo "✅ Proceeding..."'
    @date "+🕓 Started at %Y-%m-%d %H:%M:%S"
    echo "🔪 Killing active connections to DB..."
    PGPASSWORD={{DB_PWD}} docker exec -i {{PG_CONTAINER}} psql -U {{DB_USER}} -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='{{DB_CLONE_NAME}}';" >> {{DB_CLONE_NAME}}.{{TIMESTAMP}}.log 2>&1
    PGPASSWORD={{DB_PWD}} docker exec -i {{PG_CONTAINER}} psql -U {{DB_USER}} -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='{{DB_NAME}}';" >> {{DB_CLONE_NAME}}.{{TIMESTAMP}}.log 2>&1
    echo "🧹 Dropping DB clone..."
    PGPASSWORD={{DB_PWD}} docker exec -i {{PG_CONTAINER}} psql -U {{DB_USER}} -c "DROP DATABASE IF EXISTS \"{{DB_CLONE_NAME}}\";" >> {{DB_CLONE_NAME}}.{{TIMESTAMP}}.log 2>&1
    echo "📦 Creating DB..."
    PGPASSWORD={{DB_PWD}} docker exec -i {{PG_CONTAINER}} psql -U {{DB_USER}} -c "CREATE DATABASE \"{{DB_CLONE_NAME}}\" TEMPLATE \"{{DB_NAME}}\";" >> {{DB_CLONE_NAME}}.{{TIMESTAMP}}.log 2>&1
    @date "+✅ Done at %Y-%m-%d %H:%M:%S"

# Clones db
[group('manage')]
db-restore-from-clone:
    @sh -eu -c 'printf "This will RESET Explorers STORAGE. Enter password to continue: "; stty -echo; read PASSWORD; stty echo; echo; [ "$PASSWORD" = "iddqd" ] || { echo "❌ Access denied."; exit 1; }; echo "✅ Proceeding..."'
    @date "+🕓 Started at %Y-%m-%d %H:%M:%S"
    echo "🔪 Killing active connections to DBs..."
    PGPASSWORD={{DB_PWD}} docker exec -i {{PG_CONTAINER}} psql -U {{DB_USER}} -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='{{DB_CLONE_NAME}}';" >> {{DB_NAME}}.{{TIMESTAMP}}.log 2>&1
    PGPASSWORD={{DB_PWD}} docker exec -i {{PG_CONTAINER}} psql -U {{DB_USER}} -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='{{DB_NAME}}';" >> {{DB_NAME}}.{{TIMESTAMP}}.log 2>&1
    echo "🧹 Dropping DB..."
    PGPASSWORD={{DB_PWD}} docker exec -i {{PG_CONTAINER}} psql -U {{DB_USER}} -c "DROP DATABASE IF EXISTS \"{{DB_NAME}}\";" >> {{DB_NAME}}.{{TIMESTAMP}}.log 2>&1
    echo "📦 Creating DB..."
    PGPASSWORD={{DB_PWD}} docker exec -i {{PG_CONTAINER}} psql -U {{DB_USER}} -c "CREATE DATABASE \"{{DB_NAME}}\" TEMPLATE \"{{DB_CLONE_NAME}}\";" >> {{DB_NAME}}.{{TIMESTAMP}}.log 2>&1
    @date "+✅ Done at %Y-%m-%d %H:%M:%S"

# Backs up db
[group('manage')]
db-backup:
    PGPASSWORD={{DB_PWD}} docker exec -i {{PG_CONTAINER}} pg_dump -Z 9 -Fc -U {{DB_USER}} -d {{DB_NAME}} > {{DB_BACKUP_DIR}}/{{DB_NAME}}.{{TIMESTAMP}}.bak 2> {{DB_BACKUP_DIR}}/{{DB_NAME}}.{{TIMESTAMP}}.error-log.log

# Exports all known addresses
[group('manage')]
db-export-addresses-all:
    PGPASSWORD={{DB_PWD}} docker exec -i {{PG_CONTAINER}} psql -U {{DB_USER}} -d {{DB_NAME}} -c "\COPY (SELECT \"ADDRESS\" FROM \"Addresses\" WHERE \"ADDRESS\" != 'NULL' ORDER BY \"ADDRESS\" COLLATE \"C\") TO STDOUT WITH CSV" > addresses.csv

# Exports all known users addresses
[group('manage')]
db-export-addresses-users:
    PGPASSWORD={{DB_PWD}} docker exec -i {{PG_CONTAINER}} psql -U {{DB_USER}} -d {{DB_NAME}} -c "\COPY (SELECT \"ADDRESS\" FROM \"Addresses\" WHERE \"ADDRESS\" like 'P%' ORDER BY \"ADDRESS\" COLLATE \"C\") TO STDOUT WITH CSV" > addresses.csv

# Import addresses from a CSV file into the Addresses table, avoiding duplicates
[group('manage')]
import-addresses FILE:
    sh ./scripts/import_addresses.sh {{FILE}}

# Apply migrations to main db
[group('manage')]
db-migrations-apply:
    PHA_EXPLORER_DB_HOST=localhost \
    PHA_EXPLORER_DB_PORT={{DB_HOST_PORT}} \
    PHA_EXPLORER_DB_NAME={{DB_NAME}} \
    PHA_EXPLORER_DB_USER={{DB_USER}} \
    PHA_EXPLORER_DB_PWD={{DB_PWD}} \
    dotnet ef database update -v --project Database.Main/Database.Main.csproj

[group('manage')]
find-extra-v1-addresses:
    sh scripts/find_extra_v1_addresses.sh {{FUNGIBLE_BALANCES_V1_EXPORT}}