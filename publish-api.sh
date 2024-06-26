#!/bin/bash

OUT_FOLDER=./publish
OUT_BIN_FOLDER=$OUT_FOLDER/bin
NET_SUBFOLDER=net8.0
CONFIGURATION=Release

rm -r --force $OUT_FOLDER
mkdir --parents $OUT_BIN_FOLDER
mkdir --parents $OUT_BIN_FOLDER/Plugins # Not used by API atm

cp -a Backend.Commons/bin/$CONFIGURATION/$NET_SUBFOLDER/*.dll $OUT_BIN_FOLDER
cp -a Backend.Commons/bin/$CONFIGURATION/$NET_SUBFOLDER/*.pdb $OUT_BIN_FOLDER
cp -a Backend.Commons/bin/$CONFIGURATION/$NET_SUBFOLDER/*.json $OUT_BIN_FOLDER

cp -a Backend.Service.Api/bin/$CONFIGURATION/$NET_SUBFOLDER/*.dll $OUT_BIN_FOLDER
cp -a Backend.Service.Api/bin/$CONFIGURATION/$NET_SUBFOLDER/*.pdb $OUT_BIN_FOLDER
cp -a Backend.Service.Api/bin/$CONFIGURATION/$NET_SUBFOLDER/*.json $OUT_BIN_FOLDER
cp -a Backend.Service.Api/bin/$CONFIGURATION/$NET_SUBFOLDER/*.xml $OUT_BIN_FOLDER

cp -a explorer-backend-config.json $OUT_FOLDER
cp -a start-api-service.sh $OUT_FOLDER

mkdir -p $OUT_FOLDER/img 
