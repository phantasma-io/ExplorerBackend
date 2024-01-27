#!/bin/bash

cp ./config/*.json .

cd ./bin/

dotnet Backend.Service.DataFetcher.dll
