#!/bin/bash

OUT_FOLDER=./publish
OUT_BIN_FOLDER=$OUT_FOLDER/bin
NET_SUBFOLDER=net9.0

rm -r --force $OUT_FOLDER
mkdir --parents $OUT_BIN_FOLDER
mkdir --parents $OUT_BIN_FOLDER/Plugins # Not used by API atm

if [ -f "Backend.Service.Api/bin/$NET_SUBFOLDER/Backend.Service.Api" ]; then
    cp -a Backend.Service.Api/bin/$NET_SUBFOLDER/Backend.Service.Api $OUT_BIN_FOLDER
elif [ -f "Backend.Service.Api/bin/$NET_SUBFOLDER/Backend.Service.Api.exe" ]; then
    cp -a Backend.Service.Api/bin/$NET_SUBFOLDER/Backend.Service.Api.exe $OUT_BIN_FOLDER
else
    echo "No service binary found"
    exit 1
fi

cp -a Backend.Commons/bin/$NET_SUBFOLDER/*.dll $OUT_BIN_FOLDER
cp -a Backend.Commons/bin/$NET_SUBFOLDER/*.pdb $OUT_BIN_FOLDER
cp -a Backend.Commons/bin/$NET_SUBFOLDER/*.json $OUT_BIN_FOLDER

cp -a Backend.Service.Api/bin/$NET_SUBFOLDER/*.dll $OUT_BIN_FOLDER
cp -a Backend.Service.Api/bin/$NET_SUBFOLDER/*.pdb $OUT_BIN_FOLDER
cp -a Backend.Service.Api/bin/$NET_SUBFOLDER/*.json $OUT_BIN_FOLDER
cp -a Backend.Service.Api/bin/$NET_SUBFOLDER/*.xml $OUT_BIN_FOLDER

cp -a explorer-backend-config.json $OUT_FOLDER
cp -a start-api-service.sh $OUT_FOLDER

mkdir -p $OUT_FOLDER/img 
