#!/usr/bin/env bash
set -euo pipefail
if [ -f .env ]; then
  export $(grep -v '^#' .env | xargs)
fi
export COMPOSE_PROJECT_NAME="explorer-backend-worker-${DEPLOY_ENV:-production}"
docker compose up -d --force-recreate --remove-orphans
