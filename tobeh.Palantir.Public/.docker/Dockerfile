FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

ARG GRPC_VALMAR_URL="http://localhost:1234"
ENV GRPC_VALMAR_URL "${GRPC_VALMAR_URL}"

ARG GRPC_IMAGEGEN_URL="http://localhost:1234"
ENV GRPC_IMAGEGEN_URL "${GRPC_IMAGEGEN_URL}"

ARG DISCORD_TOKEN="abcd1234"
ENV DISCORD_TOKEN "${DISCORD_TOKEN}"

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release

# build command lib
WORKDIR /src
COPY ["tobeh.Palantir.Commands/tobeh.Palantir.Commands.csproj", "tobeh.Palantir.Commands/"]
RUN dotnet restore "tobeh.Palantir.Public/tobeh.Palantir.Commands.csproj"
COPY ./tobeh.Palantir.Commands ./tobeh.Palantir.Commands
WORKDIR "/src/tobeh.Palantir.Commands"
RUN dotnet build "tobeh.Palantir.Commands.csproj" -c $BUILD_CONFIGURATION -o /app/build

# build public
WORKDIR /src
COPY ["tobeh.Palantir.Public/tobeh.Palantir.Public.csproj", "tobeh.Palantir.Public/"]
RUN dotnet restore "tobeh.Palantir.Public/tobeh.Palantir.Public.csproj"
COPY ./tobeh.Palantir.Public ./tobeh.Palantir.Public
WORKDIR "/src/tobeh.Palantir.Public"
RUN dotnet build "tobeh.Palantir.Public.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "tobeh.Palantir.Public.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Copy the entrypoint script
COPY tobeh.Palantir.Public/.docker/entrypoint.sh .
USER root
RUN chown -R $APP_UID /app/Configuration
RUN chmod +x entrypoint.sh
USER $APP_UID

ENTRYPOINT ["/app/entrypoint.sh"]
CMD ["sh", "-c", "dotnet tobeh.Palantir.Public.dll"]
