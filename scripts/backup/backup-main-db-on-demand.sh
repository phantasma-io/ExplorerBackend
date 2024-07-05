#!/bin/bash

. ./config.sh

dbuser=$LOCAL_DB_USERNAME
dbpwd=$LOCAL_DB_PASSWORD
database=explorer-backend
pgcontainer=postgres
dir=./
timestamp=$(date "+%Y%m%d%H%M")

docker exec -t $pgcontainer pg_dump -c -U $dbuser $database > $dir/$database.$timestamp.sql
7z a -mx=9 $dir/$database.$timestamp.7z $dir/$database.$timestamp.sql
rm $dir/$database.$timestamp.sql

