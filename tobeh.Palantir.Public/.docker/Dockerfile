FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release

# build command lib
WORKDIR /src
COPY ["tobeh.Palantir.Commands/tobeh.Palantir.Commands.csproj", "tobeh.Palantir.Commands/"]
RUN dotnet restore "tobeh.Palantir.Commands/tobeh.Palantir.Commands.csproj"
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

CMD ["sh", "-c", "dotnet tobeh.Palantir.Public.dll"]
