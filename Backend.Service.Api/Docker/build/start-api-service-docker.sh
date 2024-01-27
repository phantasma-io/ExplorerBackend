#!/bin/bash

cp ./config/*.json .

cd ./bin/

dotnet Backend.Service.Api.dll
