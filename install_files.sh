#!/bin/bash

sudo mkdir -p /opt/ExplorerBackend/
sudo rm -r /opt/ExplorerBackend/*

cd publish
sudo cp -r * /opt/ExplorerBackend/

cd ../overwrite
sudo cp * /opt/ExplorerBackend/
