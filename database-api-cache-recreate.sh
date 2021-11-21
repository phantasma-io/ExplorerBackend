#!/bin/bash

PGPASSWORD=masterkey dropdb --username=postgres explorer-backend-api-cache

dotnet ef database update --project ./bin/Database.ApiCache/Database.ApiCache.csproj