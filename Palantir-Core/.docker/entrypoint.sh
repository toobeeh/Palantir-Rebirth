#!/bin/bash
set -e
sed -i "s!DB_DOMAIN_NAME_SED_PLACEHOLDER!$GRPC_URL!g" /app/appsettings.json
sed -i "s!DISCORD_TOKEN_SED_PLACEHOLDER!$DISCORD_TOKEN!g" /app/appsettings.json
sed -i "s!PATREON_TOKEN_SED_PLACEHOLDER!$PATREON_TOKEN!g" /app/appsettings.json

# Start the main process
exec "$@"