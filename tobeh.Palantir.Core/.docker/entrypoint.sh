#!/bin/bash
set -e
sed -i "s!ROLES_GRPC_URL_SED_PLACEHOLDER!$ROLES_GRPC_URL!g" /app/Configuration/appsettings.json
sed -i "s!GRPC_URL_SED_PLACEHOLDER!$GRPC_URL!g" /app/Configuration/appsettings.json
sed -i "s!PALANTIR_DISCORD_TOKEN_SED_PLACEHOLDER!$PALANTIR_DISCORD_TOKEN!g" /app/Configuration/appsettings.json
sed -i "s!SERVANT_DISCORD_TOKEN_SED_PLACEHOLDER!$SERVANT_DISCORD_TOKEN!g" /app/Configuration/appsettings.json
sed -i "s!PATREON_TOKEN_SED_PLACEHOLDER!$PATREON_TOKEN!g" /app/Configuration/appsettings.json

# Start the main process
exec "$@"