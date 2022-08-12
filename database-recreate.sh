#!/bin/bash

PGPASSWORD=masterkey dropdb --username=postgres explorer-backend

#GPASSWORD=postgres dropdb --username=postgres explorer-backend

#dotnet ef database update --project ./bin/Database.Main/Database.Main.csproj
dotnet ef database update --project ./Database.Main/Database.Main.csproj
