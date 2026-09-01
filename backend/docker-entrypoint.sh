#!/bin/sh
# A fresh Railway Volume mounts root-owned, but the app runs as the non-root "app"
# user (see Dockerfile) - chown the uploads directory on every start (not just once,
# so this also repairs a volume left root-owned by an earlier deploy), then drop from
# root to "app" to actually run the app.
set -e

mkdir -p /app/wwwroot/uploads
chown -R app:app /app/wwwroot/uploads

exec su-exec app dotnet RestaurantDelivery.Api.dll
