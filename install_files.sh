#!/bin/bash

sudo systemctl stop data-fetcher.service
sudo systemctl stop api-service.service

sudo mkdir -p /opt/ExplorerBackend/
sudo rm -r /opt/ExplorerBackend/*

cd publish
sudo cp -r * /opt/ExplorerBackend/

cd ../overwrite
sudo cp -r * /opt/ExplorerBackend/

cd ..
sudo cp data-fetcher.service /usr/lib/systemd/system/
sudo cp api-service.service /usr/lib/systemd/system/
sudo systemctl daemon-reload

sudo systemctl start data-fetcher.service
sudo systemctl start api-service.service

#sudo systemctl enable data-fetcher.service
#sudo systemctl enable api-service.service
