#!/bin/bash

rm --force -r Database.Main/Migrations

dotnet ef migrations add InitialCreate --project Database.Main/Database.Main.csproj

# Hack: Keep migration file names the same (for now, temporaly).
cd Database.Main/Migrations
mv *_InitialCreate.cs 20220812000000_InitialCreate.cs
mv *_InitialCreate.Designer.cs 20220812000000_InitialCreate.Designer.cs
sed -i 's/Migration("[0-9]\+_InitialCreate/Migration("20220812000000_InitialCreate/g' 20220812000000_InitialCreate.Designer.cs
