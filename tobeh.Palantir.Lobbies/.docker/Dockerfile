FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

ARG GRPC_VALMAR_URL="http://localhost:1234"
ENV GRPC_VALMAR_URL "${GRPC_VALMAR_URL}"

ARG GRPC_IMAGEGEN_URL="http://localhost:1234"
ENV GRPC_IMAGEGEN_URL "${GRPC_IMAGEGEN_URL}"

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release

# build command lib
WORKDIR /src
COPY ["tobeh.Palantir.Commands/tobeh.Palantir.Commands.csproj", "tobeh.Palantir.Commands/"]
RUN dotnet restore "tobeh.Palantir.Commands/tobeh.Palantir.Commands.csproj"
COPY ./tobeh.Palantir.Commands ./tobeh.Palantir.Commands
WORKDIR "/src/tobeh.Palantir.Commands"
RUN dotnet build "tobeh.Palantir.Commands.csproj" -c $BUILD_CONFIGURATION -o /app/build

# build lobbies
WORKDIR /src
COPY ["tobeh.Palantir.Lobbies/tobeh.Palantir.Lobbies.csproj", "tobeh.Palantir.Lobbies/"]
RUN dotnet restore "tobeh.Palantir.Lobbies/tobeh.Palantir.Lobbies.csproj"
COPY ./tobeh.Palantir.Lobbies ./tobeh.Palantir.Lobbies
WORKDIR "/src/tobeh.Palantir.Lobbies"
RUN dotnet build "tobeh.Palantir.Lobbies.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "tobeh.Palantir.Lobbies.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Copy the entrypoint script
COPY tobeh.Palantir.Lobbies/.docker/entrypoint.sh .
USER root
RUN chown -R $APP_UID /app/Configuration
RUN chmod +x entrypoint.sh
USER $APP_UID

ENTRYPOINT ["/app/entrypoint.sh"]
CMD ["sh", "-c", "dotnet tobeh.Palantir.Lobbies.dll"]
