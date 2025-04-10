#!/bin/bash

dbproject=Database.Main

if [ -d "./$dbproject" ]; then
    dotnet ef database update --project ./$dbproject/$dbproject.csproj
else
    dotnet ef database update --project ./bin/$dbproject/$dbproject.csproj
fi
