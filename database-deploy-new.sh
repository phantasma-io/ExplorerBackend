#!/bin/bash

sudo systemctl stop data-fetcher.service
sudo systemctl stop api-service.service

# Drop
dotnet ef database drop --project ./Database.ApiCache/Database.ApiCache.csproj
dotnet ef database drop --project ./Database.Main/Database.Main.csproj

# Recreate

./database-migrations-api-cache-recreate.sh
./database-migrations-recreate.sh

# This script is used to deploy the database to the server.
dotnet ef database update --project ./Database.ApiCache/Database.ApiCache.csproj
dotnet ef database update --project ./Database.Main/Database.Main.csproj

