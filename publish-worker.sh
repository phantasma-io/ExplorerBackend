#!/bin/bash

OUT_FOLDER=./publish
OUT_BIN_FOLDER=$OUT_FOLDER/bin
NET_SUBFOLDER=net8.0
CONFIGURATION=Release

rm -r --force $OUT_FOLDER
mkdir --parents $OUT_BIN_FOLDER

cp -a Backend.Commons/bin/$CONFIGURATION/$NET_SUBFOLDER/*.dll $OUT_BIN_FOLDER
cp -a Backend.Commons/bin/$CONFIGURATION/$NET_SUBFOLDER/*.pdb $OUT_BIN_FOLDER
cp -a Backend.Commons/bin/$CONFIGURATION/$NET_SUBFOLDER/*.json $OUT_BIN_FOLDER

cp -a Backend.Service.DataFetcher/bin/$CONFIGURATION/$NET_SUBFOLDER/*.dll $OUT_BIN_FOLDER
cp -a Backend.Service.DataFetcher/bin/$CONFIGURATION/$NET_SUBFOLDER/*.pdb $OUT_BIN_FOLDER
cp -a Backend.Service.DataFetcher/bin/$CONFIGURATION/$NET_SUBFOLDER/*.json $OUT_BIN_FOLDER

mkdir --parents $OUT_BIN_FOLDER/Plugins
cp -a Backend.Api.Client/bin/$CONFIGURATION/$NET_SUBFOLDER/Backend.Api.Client.* $OUT_BIN_FOLDER/Plugins
cp -a Backend.Plugins/Blockchain.Common/bin/$CONFIGURATION/$NET_SUBFOLDER/Blockchain.Common.* $OUT_BIN_FOLDER/Plugins
cp -a Backend.Plugins/Blockchain.Common/*.json $OUT_FOLDER
cd Backend.Plugins/Blockchain.Phantasma
dotnet publish
cd -
cp -a Backend.Plugins/Blockchain.Phantasma/bin/$CONFIGURATION/$NET_SUBFOLDER/Blockchain.Phantasma.* $OUT_BIN_FOLDER/Plugins
cp -a Backend.Plugins/Blockchain.Phantasma/bin/$CONFIGURATION/$NET_SUBFOLDER/publish/Phantasma.* $OUT_BIN_FOLDER
cp -a Backend.Plugins/Blockchain.Phantasma/*.json $OUT_FOLDER
cp -a Backend.Plugins/Nft.TTRS/bin/$CONFIGURATION/$NET_SUBFOLDER/Nft.TTRS.* $OUT_BIN_FOLDER/Plugins
cp -a Backend.Plugins/Nft.TTRS/*.json $OUT_FOLDER
cp -a Backend.Plugins/Price.CoinGecko/bin/$CONFIGURATION/$NET_SUBFOLDER/Price.CoinGecko.* $OUT_BIN_FOLDER/Plugins
cp -a Backend.Plugins/Price.CoinGecko/*.json $OUT_FOLDER
cp -a Backend.Plugins/Price.ExchangeRatesApiIo/bin/$CONFIGURATION/$NET_SUBFOLDER/Price.ExchangeRatesApiIo.* $OUT_BIN_FOLDER/Plugins
cp -a Backend.Plugins/Price.ExchangeRatesApiIo/*.json $OUT_FOLDER

cp -a Backend.Plugins/Blockchain.Img/bin/$CONFIGURATION/$NET_SUBFOLDER/Blockchain.Img.* $OUT_BIN_FOLDER/Plugins
cp -a Backend.Plugins/Blockchain.Img/*.json $OUT_FOLDER

cp -a explorer-backend-config.json $OUT_FOLDER
cp -a start-data-fetcher.sh $OUT_FOLDER

mkdir -p $OUT_FOLDER/img 
