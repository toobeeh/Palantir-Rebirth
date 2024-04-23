FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

ARG GRPC_VALMAR_URL="http://localhost:1234"
ENV GRPC_VALMAR_URL "${GRPC_URL}"

ARG GRPC_IMAGEGEN_URL="http://localhost:1234"
ENV GRPC_IMAGEGEN_URL "${GRPC_IMAGEGEN_URL}"

ARG DISCORD_TOKEN="abcd1234"
ENV DISCORD_TOKEN "${DISCORD_TOKEN}"

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release

# build grpc lib
WORKDIR /src
COPY ["Valmar-Client/Valmar-Client.csproj", "Valmar-Client/"]
RUN dotnet restore "Valmar-Client/Valmar-Client.csproj"
COPY ./Valmar-Client ./Valmar-Client
WORKDIR "/src/Valmar-Client"
RUN dotnet build "Valmar-Client.csproj" -c $BUILD_CONFIGURATION -o /app/build

# build core
WORKDIR /src
COPY ["Palantir-Commands/Palantir-Commands.csproj", "Palantir-Commands/"]
RUN dotnet restore "Palantir-Commands/Palantir-Commands.csproj"
COPY ./Palantir-Commands ./Palantir-Commands
WORKDIR "/src/Palantir-Commands"
RUN dotnet build "Palantir-Commands.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Palantir-Commands.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Copy the entrypoint script
COPY Palantir-Commands/.docker/entrypoint.sh .
USER root
RUN chown -R $APP_UID /app/Configuration
RUN chmod +x entrypoint.sh
USER $APP_UID

ENTRYPOINT ["/app/entrypoint.sh"]
CMD ["sh", "-c", "dotnet Palantir-Commands.dll"]
