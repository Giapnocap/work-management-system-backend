FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src
COPY WorkManagementSystem.csproj ./
RUN dotnet restore WorkManagementSystem.csproj

FROM restore AS build
COPY . .
RUN dotnet build WorkManagementSystem.csproj \
    --configuration Release \
    --no-restore \
    --warnaserror

FROM build AS migration-build
RUN dotnet tool install dotnet-ef \
    --version 8.0.20 \
    --tool-path /tools
RUN /tools/dotnet-ef migrations bundle \
    --configuration Release \
    --no-build \
    --output /migration/efbundle

FROM build AS publish
RUN dotnet publish WorkManagementSystem.csproj \
    --configuration Release \
    --no-build \
    --no-restore \
    --output /app/publish \
    -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime-base
WORKDIR /app
RUN mkdir -p /app/Uploads /app/logs \
    && chown -R app:app /app
USER app
ENV ASPNETCORE_HTTP_PORTS=8080

FROM runtime-base AS migrations
COPY --from=migration-build --chown=app:app /migration/efbundle ./efbundle
COPY --from=build --chown=app:app /src/appsettings.json ./appsettings.json
ENTRYPOINT ["./efbundle"]

FROM runtime-base AS runtime
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
USER app
COPY --from=publish --chown=app:app /app/publish .
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=5s --start-period=15s --retries=5 \
    CMD curl --fail --silent --show-error --output /dev/null http://127.0.0.1:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "WorkManagementSystem.dll"]
