#!/bin/bash
set -e
sed -i "s!GRPC_VALMAR_URL_SED_PLACEHOLDER!$GRPC_VALMAR_URL!g" /app/Configuration/appsettings.json
sed -i "s!GRPC_IMAGEGEN_URL_SED_PLACEHOLDER!$GRPC_IMAGEGEN_URL!g" /app/Configuration/appsettings.json
sed -i "s!DISCORD_TOKEN_SED_PLACEHOLDER!$DISCORD_TOKEN!g" /app/Configuration/appsettings.json

# Start the main process
exec "$@"