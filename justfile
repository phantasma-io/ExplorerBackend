[private]
just:
    just -l

set dotenv-load
API_TMUX_SESSION_NAME := env("API_TMUX_SESSION_NAME")
WORKER_TMUX_SESSION_NAME := env("WORKER_TMUX_SESSION_NAME")
API_BIN_DIR := env("API_BIN_DIR")
WORKER_BIN_DIR := env("WORKER_BIN_DIR")
RELEASE_MODE := env("RELEASE_MODE")

DB_PORT := env("DB_PORT")
DB_USER := env("DB_USER")
DB_PWD := env("DB_PWD")
DB_NAME := env("DB_NAME")
DB_CLONE_NAME := env("DB_CLONE_NAME")
PG_CONTAINER := env("PG_CONTAINER")
DB_STATE_ZERO_BACKUP := env("DB_STATE_ZERO_BACKUP")

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
    sh ./scripts/run_api_in_tmux.sh

[group('run')]
rw0:
    just pw
    cd {{WORKER_BIN_DIR}} && ./Backend.Service.Worker

[group('run')]
rw:
    sh ./scripts/run_worker_in_tmux.sh

# Stops API running in tmux
[group('run')]
stop-api:
    sh -u -c 'tmux has-session -t {{API_TMUX_SESSION_NAME}} 2>/dev/null || exit 0; pid=$(tmux list-panes -t {{API_TMUX_SESSION_NAME}} -F "#{pane_pid}"); [ -n "$pid" ] && kill -s INT "$pid" && tmux kill-session -t {{API_TMUX_SESSION_NAME}}'

# Stops Worker running in tmux
[group('run')]
stop-worker:
    sh -u -c 'tmux has-session -t {{WORKER_TMUX_SESSION_NAME}} 2>/dev/null || exit 0; pid=$(tmux list-panes -t {{WORKER_TMUX_SESSION_NAME}} -F "#{pane_pid}"); [ -n "$pid" ] && kill -s INT "$pid" && tmux kill-session -t {{WORKER_TMUX_SESSION_NAME}}'

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

# Exports all known addresses
[group('manage')]
db-export-addresses-all:
    PGPASSWORD={{DB_PWD}} docker exec -i {{PG_CONTAINER}} psql -U {{DB_USER}} -d {{DB_NAME}} -c "\COPY (SELECT \"ADDRESS\" FROM \"Addresses\" WHERE \"ADDRESS\" != 'NULL' ORDER BY \"ADDRESS\" COLLATE \"C\") TO STDOUT WITH CSV" > addresses.csv

# Exports all known users addresses
[group('manage')]
db-export-addresses-users:
    PGPASSWORD={{DB_PWD}} docker exec -i {{PG_CONTAINER}} psql -U {{DB_USER}} -p {{DB_PORT}} -d {{DB_NAME}} -c "\COPY (SELECT \"ADDRESS\" FROM \"Addresses\" WHERE \"ADDRESS\" like 'P%' ORDER BY \"ADDRESS\" COLLATE \"C\") TO STDOUT WITH CSV" > addresses.csv

[group('manage')]
db-check-missing:
    sh scripts/find_extra_v1_addresses.sh {{FUNGIBLE_BALANCES_V1_EXPORT}}