FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# required for cloning with ssh
RUN apt update
RUN apt install -y ssh

WORKDIR /source

# setup ssh id_rsa keys to clone private repo
COPY id_rsa /root/.ssh/id_rsa
COPY id_rsa.pub /root/.ssh/id_rsa.pub
COPY ssh-config /root/.ssh/config
RUN chmod 600 /root/.ssh/id_rsa
RUN chmod 600 /root/.ssh/id_rsa.pub
# add github to known hosts
RUN ssh-keyscan -t rsa github.com >> ~/.ssh/known_hosts
# clone explorer backend and checkout out single branch
ARG BUILD_BRANCH
RUN git clone -b $BUILD_BRANCH --single-branch git@github.com:phantasma-io/ExplorerBackend.git

# create new source directory
WORKDIR /source/ExplorerBackend
RUN git submodule update --init --recursive

# restore project dependencies
RUN dotnet restore

# build project
RUN dotnet build

FROM build AS publish

# allow execution off all shell scripts
RUN chmod +x *.sh

# use publish script to move files to correct directory
# TODO: Add this routine to docker file
RUN ./publish-api.sh

FROM base AS final

# switch into publish folder
WORKDIR /app

COPY --from=publish /source/ExplorerBackend/publish .

# Copy start scripts, which moves config files mounted to the correct directory
COPY start-api-service-docker.sh start-api-service-docker.sh

# allow execution of these start scripts
RUN chmod +x *.sh
