#!/bin/bash

OUT_FOLDER=./publish
OUT_BIN_FOLDER=$OUT_FOLDER/bin
NET_SUBFOLDER=net9.0

dotnet build ExplorerBackend.sln

sh publish-worker.sh

cp -r $OUT_BIN_FOLDER/Plugins ./Backend.Service.Worker/bin/$NET_SUBFOLDER
