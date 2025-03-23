#!/bin/bash

OUT_FOLDER=./publish
OUT_BIN_FOLDER=$OUT_FOLDER/bin
NET_SUBFOLDER=net9.0

rm -r --force $OUT_FOLDER
mkdir --parents $OUT_BIN_FOLDER

cp -a Backend.Commons/bin/$NET_SUBFOLDER/*.dll $OUT_BIN_FOLDER
cp -a Backend.Commons/bin/$NET_SUBFOLDER/*.pdb $OUT_BIN_FOLDER
cp -a Backend.Commons/bin/$NET_SUBFOLDER/*.json $OUT_BIN_FOLDER

if [ -f "Backend.Service.Worker/bin/$NET_SUBFOLDER/Backend.Service.Worker" ]; then
    cp -a Backend.Service.Worker/bin/$NET_SUBFOLDER/Backend.Service.Worker $OUT_BIN_FOLDER
elif [ -f "Backend.Service.Worker/bin/$NET_SUBFOLDER/Backend.Service.Worker.exe" ]; then
    cp -a Backend.Service.Worker/bin/$NET_SUBFOLDER/Backend.Service.Worker.exe $OUT_BIN_FOLDER
else
    echo "No service binary found"
    exit 1
fi

cp -a Backend.Service.Worker/bin/$NET_SUBFOLDER/*.dll $OUT_BIN_FOLDER
cp -a Backend.Service.Worker/bin/$NET_SUBFOLDER/*.pdb $OUT_BIN_FOLDER
cp -a Backend.Service.Worker/bin/$NET_SUBFOLDER/*.json $OUT_BIN_FOLDER

mkdir --parents $OUT_BIN_FOLDER/Plugins
cp -a Backend.Api.Client/bin/$NET_SUBFOLDER/Backend.Api.Client.* $OUT_BIN_FOLDER/Plugins
cp -a Backend.Plugins/Blockchain.Common/bin/$NET_SUBFOLDER/Blockchain.Common.* $OUT_BIN_FOLDER/Plugins
cp -a Backend.Plugins/Blockchain.Common/*.json $OUT_FOLDER
cd Backend.Plugins/Blockchain.Phantasma
dotnet publish
cd -
cp -a Backend.Plugins/Blockchain.Phantasma/bin/$NET_SUBFOLDER/Blockchain.Phantasma.* $OUT_BIN_FOLDER/Plugins
cp -a Backend.Plugins/Blockchain.Phantasma/bin/$NET_SUBFOLDER/publish/Phantasma.* $OUT_BIN_FOLDER
cp -a Backend.Plugins/Blockchain.Phantasma/*.json $OUT_FOLDER
cp -a Backend.Plugins/Nft.TTRS/bin/$NET_SUBFOLDER/Nft.TTRS.* $OUT_BIN_FOLDER/Plugins
cp -a Backend.Plugins/Nft.TTRS/*.json $OUT_FOLDER
cp -a Backend.Plugins/Price.CoinGecko/bin/$NET_SUBFOLDER/Price.CoinGecko.* $OUT_BIN_FOLDER/Plugins
cp -a Backend.Plugins/Price.CoinGecko/*.json $OUT_FOLDER
cp -a Backend.Plugins/Price.ExchangeRatesApiIo/bin/$NET_SUBFOLDER/Price.ExchangeRatesApiIo.* $OUT_BIN_FOLDER/Plugins
cp -a Backend.Plugins/Price.ExchangeRatesApiIo/*.json $OUT_FOLDER

cp -a Backend.Plugins/Blockchain.Img/bin/$NET_SUBFOLDER/Blockchain.Img.* $OUT_BIN_FOLDER/Plugins
cp -a Backend.Plugins/Blockchain.Img/*.json $OUT_FOLDER

cp -a explorer-backend-config.json $OUT_FOLDER
cp -a start-worker-service.sh $OUT_FOLDER

mkdir -p $OUT_FOLDER/img 
