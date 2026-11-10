#!/bin/bash
# Runs automatically the first time the container creates its database.
# It runs every script in /opt/weather-sql (= ./database/sql) in name order, connected
# as the application user, so the tables and package are owned by WEATHER_APP.
#
# Re-run by hand at any time with:
#   docker exec weather-oracle bash /container-entrypoint-initdb.d/01_create_schema.sh
set -euo pipefail

for script in /opt/weather-sql/*.sql; do
  echo "==> Running ${script}"
  sqlplus -s -L "${APP_USER}/${APP_USER_PASSWORD}@//localhost/FREEPDB1" "@${script}"
done
