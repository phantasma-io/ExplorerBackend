#!/bin/bash

PGPASSWORD=masterkey dropdb --username=postgres explorer-backend

dotnet ef database update --project ./bin/Database.Main/Database.Main.csproj