#!/bin/bash
set -e
sed -i "s!GRPC_URL_SED_PLACEHOLDER!$GRPC_URL!g" /app/Configuration/appsettings.json
sed -i "s!DISCORD_TOKEN_SED_PLACEHOLDER!$DISCORD_TOKEN!g" /app/Configuration/appsettings.json
sed -i "s!PATREON_TOKEN_SED_PLACEHOLDER!$PATREON_TOKEN!g" /app/Configuration/appsettings.json

# Start the main process
exec "$@"