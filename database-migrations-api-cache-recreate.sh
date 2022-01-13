#!/bin/bash

rm --force -r Database.ApiCache/Migrations

dotnet ef migrations add InitialCreate --project Database.ApiCache/Database.ApiCache.csproj

# Hack: Keep migration file names the same (for now, temporaly).
cd Database.ApiCache/Migrations
mv *_InitialCreate.cs 20211120000000_InitialCreate.cs
mv *_InitialCreate.Designer.cs 20211120000000_InitialCreate.Designer.cs
sed -i 's/Migration("[0-9]\+_InitialCreate/Migration("20211120000000_InitialCreate/g' 20211120000000_InitialCreate.Designer.cs
