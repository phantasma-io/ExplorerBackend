#!/bin/bash

OUT_FOLDER=./publish
OUT_BIN_FOLDER=$OUT_FOLDER/bin
NET_SUBFOLDER=net6.0

rm -r --force $OUT_FOLDER
mkdir --parents $OUT_BIN_FOLDER

cp -a GhostDevs.Commons/bin/$NET_SUBFOLDER/*.dll $OUT_BIN_FOLDER
cp -a GhostDevs.Commons/bin/$NET_SUBFOLDER/*.pdb $OUT_BIN_FOLDER
cp -a GhostDevs.Commons/bin/$NET_SUBFOLDER/*.json $OUT_BIN_FOLDER

cp -a GhostDevs.Commons $OUT_BIN_FOLDER/
rm -r $OUT_BIN_FOLDER/GhostDevs.Commons/bin
rm -r $OUT_BIN_FOLDER/GhostDevs.Commons/obj

cp -a GhostDevs.Service.Api/bin/$NET_SUBFOLDER/*.dll $OUT_BIN_FOLDER
cp -a GhostDevs.Service.Api/bin/$NET_SUBFOLDER/*.pdb $OUT_BIN_FOLDER
cp -a GhostDevs.Service.Api/bin/$NET_SUBFOLDER/*.json $OUT_BIN_FOLDER

cp -a GhostDevs.Service.DataFetcher/bin/$NET_SUBFOLDER/*.dll $OUT_BIN_FOLDER
cp -a GhostDevs.Service.DataFetcher/bin/$NET_SUBFOLDER/*.pdb $OUT_BIN_FOLDER
cp -a GhostDevs.Service.DataFetcher/bin/$NET_SUBFOLDER/*.json $OUT_BIN_FOLDER

mkdir --parents $OUT_BIN_FOLDER/Plugins
cp -a GhostDevs.Api.Client/bin/$NET_SUBFOLDER/GhostDevs.Api.Client.* $OUT_BIN_FOLDER/Plugins
cp -a GhostDevs.Plugins/Blockchain.Common/bin/Debug/$NET_SUBFOLDER/Blockchain.Common.* $OUT_BIN_FOLDER/Plugins
cp -a GhostDevs.Plugins/Blockchain.Common/*.json $OUT_FOLDER
cp -a GhostDevs.Plugins/Blockchain.Phantasma/bin/Debug/$NET_SUBFOLDER/Blockchain.Phantasma.* $OUT_BIN_FOLDER/Plugins
cp -a GhostDevs.Plugins/Blockchain.Phantasma/bin/Debug/$NET_SUBFOLDER/Phantasma.* $OUT_BIN_FOLDER
cp -a GhostDevs.Plugins/Blockchain.Phantasma/*.json $OUT_FOLDER
cp -a GhostDevs.Plugins/Nft.TTRS/bin/Debug/$NET_SUBFOLDER/Nft.TTRS.* $OUT_BIN_FOLDER/Plugins
cp -a GhostDevs.Plugins/Nft.TTRS/*.json $OUT_FOLDER
cp -a GhostDevs.Plugins/Price.CoinGecko/bin/Debug/$NET_SUBFOLDER/Price.CoinGecko.* $OUT_BIN_FOLDER/Plugins
cp -a GhostDevs.Plugins/Price.CoinGecko/*.json $OUT_FOLDER
cp -a GhostDevs.Plugins/Price.ExchangeRatesApiIo/bin/Debug/$NET_SUBFOLDER/Price.ExchangeRatesApiIo.* $OUT_BIN_FOLDER/Plugins
cp -a GhostDevs.Plugins/Price.ExchangeRatesApiIo/*.json $OUT_FOLDER

cp -a Database.Main $OUT_BIN_FOLDER/
rm -r $OUT_BIN_FOLDER/Database.Main/bin
rm -r $OUT_BIN_FOLDER/Database.Main/obj

cp -a Database.ApiCache $OUT_BIN_FOLDER/
rm -r $OUT_BIN_FOLDER/Database.ApiCache/bin
rm -r $OUT_BIN_FOLDER/Database.ApiCache/obj

cp -a explorer-backend-config.json $OUT_FOLDER
cp -a database-api-cache-recreate.sh $OUT_FOLDER
cp -a database-api-cache-update.sh $OUT_FOLDER
cp -a database-recreate.sh $OUT_FOLDER
cp -a database-update.sh $OUT_FOLDER
cp -a start-api-service.sh $OUT_FOLDER
cp -a start-data-fetcher.sh $OUT_FOLDER
