FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /source

COPY . .

ARG BUILD_CONFIGURATION=Release
ENV BUILD_CONFIGURATION=$BUILD_CONFIGURATION

RUN dotnet restore
RUN dotnet build -c $BUILD_CONFIGURATION

# use publish script to move files to correct directory
RUN chmod +x *.sh
RUN ./publish-worker.sh

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

WORKDIR /app

COPY --from=build /source/publish .
COPY Backend.Service.Worker/Docker/build/start-worker-service-docker.sh start-worker-service-docker.sh

RUN chmod +x *.sh
