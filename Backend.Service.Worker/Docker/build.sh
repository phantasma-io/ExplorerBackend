#!/usr/bin/env bash

# Export env vars
export $(grep -v '^#' .env | xargs)

echo "Building worker image from local repo..."

docker compose build "$@"
