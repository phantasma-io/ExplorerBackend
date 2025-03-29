# Explorer Backend
A backend for Phantasma explorer

## Installation

### User

Create user 'pha'. All following steps will be using this user and home directory '/home/pha'.

### Docker

Install Docker according to the following instruction: https://docs.docker.com/engine/install/

Create folder for containers:
```
mkdir /home/pha/docker
```

### PostgreSQL

Create folder for PostgreSQL:
```
mkdir /home/pha/docker/postgresql
```

Create /home/pha/docker/postgresql/docker-compose.yml with the following content, replacing `CHANGE_ME` with real values:

```
services:
  postgres:
    image: postgres:16
    hostname: postgres
    container_name: postgres
    shm_size: 4gb
    expose:
      - 5432
    ports:
      - "5432:5432"
    environment:
      POSTGRES_USER: CHANGE_ME
      POSTGRES_PASSWORD: CHANGE_ME
    volumes:
      - ./postgres-db:/var/lib/postgresql/data
      - ./postgresql.conf:/etc/postgresql/postgresql.conf
    restart: unless-stopped
    networks:
     - postgresql
    command: postgres -c config_file=/etc/postgresql/postgresql.conf

networks:
   postgresql:
      name: postgresql-network
```

Get PostgreSQL configuration file from the container and put it here:
/home/pha/docker/postgresql/postgresql.conf

To copy configuration from a running container following command can be used:
```
docker cp `docker ps -aqf "name=^postgres$"`:/var/lib/postgresql/data/postgresql.conf /home/pha/docker/postgresql/
```

This tool can be used to get optimized settings for postgres: https://pgtune.leopard.in.ua/
Place optimized settings at the start of postgresql.conf.

Start PostgreSQL with following commands:
```
docker compose pull
docker compose up -d
```

### EFCore tools

Install dotnet-sdk-8.0 using following instructions:

https://learn.microsoft.com/dotnet/core/install/linux

Install Entity framework tools using following command:
```
dotnet tool install --global dotnet-ef
```

### Backend: Step 1: Prepare folders

Create following folders:
```
mkdir -p /home/pha/docker/explorer-backend/api
mkdir -p /home/pha/docker/explorer-backend/database-migration
mkdir -p /home/pha/docker/explorer-backend/worker
```

Copy content of ExplorerBackend/Backend.Service.Api/Docker folder into /home/pha/docker/explorer-backend/api.

Copy content of ExplorerBackend/Backend.Service.Worker/Docker folder into /home/pha/docker/explorer-backend/worker.

Copy backend's configuration file ExplorerBackend/explorer-backend-config.json to 'api/config' and 'worker/config' folders, ensuring that DatabaseConfiguration->Main section of config contains correct settings, specifically database user name and password.

Create link to config for database-migration folder using command:
```
ln -s /home/pha/docker/explorer-backend/worker/config/explorer-backend-config.json /home/pha/docker/explorer-backend/database-migration/explorer-backend-config.json
```

Copy files ExplorerBackend/database-api-cache-update.sh and ExplorerBackend/database-update.sh into /home/pha/docker/explorer-backend/database-migration.

### Backend: Step 2: Create databases

On machine with installed dotnet-sdk-8.0 run following command to publish database migrations:
```
ExplorerBackend/publish-db-migrations.sh
```

Copy ExplorerBackend/publish/bin to target machine to folder /home/pha/docker/explorer-backend/database-migration/bin.

Switch to /home/pha/docker/explorer-backend/database-migration/ folder and create new databases using following commands:

```
sh database-api-cache-update.sh
sh database-update.sh
```

### Backend: Step 3: Finish deployment

Add files /home/pha/docker/explorer-backend/api/.env and /home/pha/docker/explorer-backend/worker/.env with the following content:
```
# Github branch to be used
BUILD_BRANCH=main
```
where 'main' is main github branch of backend project which we use for production deployment.

Launch both API and Worker services from corresponding folders /home/pha/docker/explorer-backend/api and /home/pha/docker/explorer-backend/worker by either using sh script 'deploy.sh'
```
deploy.sh
```
or by running commands
```
docker compose pull
docker compose up -d
```

### Nginx

Switch to 'root' user or use sudo.

Install Nginx package.

Place list of Cloudflare IP addresses into this file:

/etc/nginx/cloudflare-allow.conf

Content for this file can be obtained here: https://www.cloudflare.com/ips

Set these rights:
```
chmod 644 /etc/nginx/cloudflare-allow.conf
```

Place phantasma.info certificate and certificate key into following locations:

/etc/ssl/certs/cf-phantasma.info.pem

/etc/ssl/private/cf-phantasma.info.key

Set rights for these files

```
chmod 644 /etc/ssl/certs/cf-phantasma.info.pem
chmod 600 /etc/ssl/private/cf-phantasma.info.key
```

Add following file:
```
/etc/nginx/sites-available/explorer-api
```

with the following content:
```
server {
    listen        443 ssl;

    include /etc/nginx/cloudflare-allow.conf;
    deny all;

    server_name   api-explorer.phantasma.info;
    ssl_certificate     /etc/ssl/certs/cf-phantasma.info.pem;
    ssl_certificate_key /etc/ssl/private/cf-phantasma.info.key;
    ssl_protocols       TLSv1 TLSv1.1 TLSv1.2;
    ssl_ciphers         HIGH:!aNULL:!MD5;
    location / {
        proxy_pass         http://127.0.0.1:9000;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}
```

Set these rights:
```
chmod 644 /etc/nginx/sites-available/explorer-api
```

Create link with the following command:
```
ln -s /etc/nginx/sites-available/explorer-api /etc/nginx/sites-enabled/explorer-api
```

Test Nginx configuration using command:
```
sudo nginx -t
```

Restart Nginx to apply changes:
```
sudo /etc/init.d/nginx restart
```

### Test backend API

To test if deployment was successful, following url can be used:

https://api-explorer.phantasma.info/

## Sources structure
File / Folder | Description
------------- | -------------
Backend.Api.Client | Library used by plugins for REST calls
Backend.Database | Everything that related to database and its objects, including EF model and migrations
Backend.Plugins | Folder with plugins for fetching blockchains and NFTs data
Backend.Plugins/Blockchain.Phantasma | Phantasma blockchain plugin, retrieves all necessary data from blockchain and stores it in the database
Backend.Plugins/Nft.TTRS | 22 Racing series NFT plugin, retrieves all necessary data from 22 series site and stores it in the database
Backend.Service.Api | 1st of 2 backend services, provides endpoints for frontend
Backend.Service.Worker | 2nd of 2 backend services, runs available blockchain and NFT plugins to fetch necessary data and save it in the database
Backend.PluginEngine | Plugin engine library used by both backend services, provides plugin mechanisms and interfaces
Backend.PostgreSQLConnector | Library for raw SQL querying of PastgresSQL database, can be used with current EF database implementation
clean.sh | Development script, cleans sources from binaries and temporal VS files. Close VS before running
database-migrations-recreate.sh | Development script, recreates "Migrations" folder in Database.Main, preserving previous dates in migrations file names
database-recreate.sh | Deployment script, drops "explorer-backend" database and creates it using migrations in Database.Main/Migrations. Database user "postgres" and password "masterkey" are used in script.
Backend.ExplorerBackend.sln | MSVS solution for explorer backend
explorer-backend-config.json | Default backend configuration file, should be placed on same level as backend's bin folder
README.md | This readme file
start-api-service.sh | Starts API service (can be replaced with the service files)
start-worker.sh | Starts Worker service (can be replaced with the service files)
api-service.service | systemd service file for the API service
data-fetcher.service | systemd service file for the Worker service 
publish.sh | script to setup the folder sturcture we later deploy with
install_files.sh | stops the services, copies current version, copies publish and overwrite folder to /opt/explorer-backend, starts and enable services

*All scripts are designed to be used in OS Linux, or in OS Windows with MSYS distributive available in PATH environment variable*
