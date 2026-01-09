# Export env vars
if [ -f .env ]; then
  export $(grep -v '^#' .env | xargs)
fi

# Avoid compose project name collisions across stacks.
export COMPOSE_PROJECT_NAME="explorer-backend-worker-${DEPLOY_ENV:-production}"

CONTAINER_NAME="explorer-backend-worker-${DEPLOY_ENV:-production}"
podman rm -f "$CONTAINER_NAME" >/dev/null 2>&1 || true

podman-compose up -d --force-recreate --remove-orphans
