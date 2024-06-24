FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

ARG GRPC_URL="http://localhost:1234"
ENV GRPC_URL "${GRPC_URL}"

ARG PALANTIR_DISCORD_TOKEN="abcd1234"
ENV PALANTIR_DISCORD_TOKEN "${PALANTIR_DISCORD_TOKEN}"

ARG SERVANT_DISCORD_TOKEN="abcd1234"
ENV SERVANT_DISCORD_TOKEN "${SERVANT_DISCORD_TOKEN}"

ARG PATREON_TOKEN="abcd1234"
ENV PATREON_TOKEN "${PATREON_TOKEN}"

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release

# build core
WORKDIR /src
COPY ["tobeh.Palantir.Core/tobeh.Palantir.Core.csproj", "tobeh.Palantir.Core/"]
RUN dotnet restore "tobeh.Palantir.Core/tobeh.Palantir.Core.csproj"
COPY ./tobeh.Palantir.Core ./tobeh.Palantir.Core
WORKDIR "/src/tobeh.Palantir.Core"
RUN dotnet build "tobeh.Palantir.Core.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "tobeh.Palantir.Core.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Copy the entrypoint script
COPY tobeh.Palantir.Core/.docker/entrypoint.sh .
USER root
RUN chown -R $APP_UID /app/Configuration
RUN chmod +x entrypoint.sh
USER $APP_UID

ENTRYPOINT ["/app/entrypoint.sh"]
CMD ["sh", "-c", "dotnet tobeh.Palantir.Core.dll"]
