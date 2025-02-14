#!/bin/bash

OUT_FOLDER=./publish
OUT_BIN_FOLDER=$OUT_FOLDER/bin
NET_SUBFOLDER=net9.0

rm -r --force $OUT_FOLDER
mkdir --parents $OUT_BIN_FOLDER

cp -a Backend.Commons $OUT_BIN_FOLDER/
rm -r $OUT_BIN_FOLDER/Backend.Commons/bin
rm -r $OUT_BIN_FOLDER/Backend.Commons/obj

cp -a Database.ApiCache $OUT_BIN_FOLDER/
rm -r $OUT_BIN_FOLDER/Database.ApiCache/bin
rm -r $OUT_BIN_FOLDER/Database.ApiCache/obj

cp -a Database.Main $OUT_BIN_FOLDER/
rm -r $OUT_BIN_FOLDER/Database.Main/bin
rm -r $OUT_BIN_FOLDER/Database.Main/obj
