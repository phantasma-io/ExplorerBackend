# Explorer Backend
A backend for Phantasma explorer

## Sources structure
File / Folder | Description
------------- | -------------
Backend.Api.Client | Library used by plugins for REST calls
Backend.Database | Everything that related to database and its objects, including EF model and migrations
Backend.Plugins | Folder with plugins for fetching blockchains and NFTs data
Backend.Plugins/Blockchain.Phantasma | Phantasma blockchain plugin, retrieves all necessary data from blockchain and stores it in the database
Backend.Plugins/Nft.TTRS | 22 Racing series NFT plugin, retrieves all necessary data from 22 series site and stores it in the database
Backend.Service.Api | 1st of 2 backend services, provides endpoints for frontend
Backend.Service.DataFetcher | 2nd of 2 backend services, runs available blockchain and NFT plugins to fetch necessary data and save it in the database
Backend.PluginEngine | Plugin engine library used by both backend services, provides plugin mechanisms and interfaces
Backend.PostgreSQLConnector | Library for raw SQL querying of PastgresSQL database, can be used with current EF database implementation
clean.sh | Development script, cleans sources from binaries and temporal VS files. Close VS before running
database-migrations-recreate.sh | Development script, recreates "Migrations" folder in Database.Main, preserving previous dates in migrations file names
database-recreate.sh | Deployment script, drops "explorer-backend" database and creates it using migrations in Database.Main/Migrations. Database user "postgres" and password "masterkey" are used in script.
Backend.ExplorerBackend.sln | MSVS solution for explorer backend
explorer-backend-config.json | Default backend configuration file, should be placed on same level as backend's bin folder
README.md | This readme file
start-api-service.sh | Starts API service (can be replaced with the service files)
start-data-fetcher.sh | Starts Data Fetcher service (can be replaced with the service files)
api-service.service | systemd service file for the API service
data-fetcher.service | systemd service file for the Data Fetcher service 
publish.sh | script to setup the folder sturcture we later deploy with
install_files.sh | stops the services, copies current version, copies publish and overwrite folder to /opt/explorer-backend, starts and enable services

*All scripts are designed to be used in OS Linux, or in OS Windows with MSYS distributive available in PATH environment variable*

## Windows installation

Download and install latest PostgreSQL distributive from here: https://www.postgresql.org/download/windows/

For Cyrillic Windows:
Edit file C:/postgres-data/postgresql.conf, where "C:/postgres-data" is a folder where Postgres data is stored.

Change lc_messages parameter to the following:

    lc_messages = 'en_US.UTF-8'

It will enable correct error reporting, otherwise error messages will consist of "?" symbols.

### Self-signed SSL certificate generation for test machine

    makecert -r -pe -n "CN=localhost" -sky exchange -sv cert-test.pvk cert-test.cer

*Use 'cert-test-pwd' password for test certificate*

    pvk2pfx -pvk cert-test.pvk -spc cert-test.cer -pfx cert-test.pfx -pi cert-test-pwd
    rm cert-test.pvk cert-test.cer

Place cert-test.pfx to backend bin folder.

*TO BE CONTINUED...*

## Debian 9/10 installation

Create backend folder /opt/explorer-backend and ensure it has correct permissions.

### PostgreSQL installation

Install latest PostgreSQL following this instruction (version 12 for example): https://www.postgresql.org/download/linux/debian/

Make sure to install postgresql-12 and postgresql-client-12 packages:

    apt-get install postgresql-12 postgresql-client-12

Edit confihuration file /etc/postgresql/12/main/pg_hba.conf: Change "ident" or "peer" parameter to "trust":

    local   all         all                               trust

Restart PostgreSQL server:

    /etc/init.d/postgresql reload

### New database user configuration (example, optional)

Add new system user:

    sudo adduser postgres_new

Add new database user:

    sudo su - postgres
    createuser postgres_new

Set new database user password:

    sudo -u postgres psql
    \password postgres_new
    <input user password>
    \q

Add new database user database creation:

    sudo -u postgres psql
    alter user postgres_new createdb;
    \q

### Dependencies installation

Install Entity Framework:

    sudo dotnet tool install --global dotnet-ef

### SSL certificate generation

Install certbot following instructions: https://certbot.eff.org/

If apache is running, stop and disable it:

    ps -e | grep apache
    sudo systemctl disable apache2 && sudo systemctl stop apache2

Ensure that ports 80 and 443 are not occupied:

    sudo netstat -tulpn | grep :80
    sudo netstat -tulpn | grep :443

Install certbot:

    sudo apt-get install certbot

Create certificate:

    sudo certbot certonly --standalone -d explorer.phantasma.io

Convert certificate:

    openssl pkcs12 -export -out /opt/cert-explorer-phantasma-io.pfx -inkey /etc/letsencrypt/live/explorer.phantasma.io/privkey.pem -in /etc/letsencrypt/live/explorer.phantasma.io/fullchain.pem -name "SSL Signed Certificate"

### SSL certificate renewal

Renew certificate:

    certbot renew

Convert certificate:

    openssl pkcs12 -export -out /opt/cert-explorer-phantasma-io.pfx -inkey /etc/letsencrypt/live/explorer.phantasma.io/privkey.pem -in /etc/letsencrypt/live/explorer.phantasma.io/fullchain.pem -name "SSL Signed Certificate"


### Backend installation

Unpack backend distributive in /opt/explorer-backend folder.

Install authbind and setup 443 port rights:

    sudo apt-get install authbind
    sudo touch /etc/authbind/byport/443
    sudo chmod 500 /etc/authbind/byport/443
    sudo chown <user> /etc/authbind/byport/443

Do same thing for port 80 to enable HTTP redirection.

Recreate database running script in /opt/explorer-backend folder (*make sure it has correct user name and password*):

    database-recreate.sh

Make sure that following scripts has execution rights:

    /opt/explorer-backend/start-data-fetcher.sh
    /opt/explorer-backend/start-api-service.sh

Edit crontab and add following lines:

    crontab -e

    @reboot  cd /opt/explorer-backend; screen -dmS ExplorerFetcherScreen bash -c './start-data-fetcher.sh; exec bash'
    @reboot  cd /opt/explorer-backend; screen -dmS ExplorerApiScreen bash -c './start-api-service.sh; exec bash'

Reboot machine to start backend services.

### Backend installation (alternative)

as backend/normal user

    cd <source_folder>
    dotnet build
    ./publish.sh

database update

    <TODO stop services>
    <TODO insert commands here>

as root

    ./install_files.sh
