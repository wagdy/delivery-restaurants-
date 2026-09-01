# syntax=docker/dockerfile:1
#
# Build context must be the `backend/` directory (on Railway: set this service's
# Root Directory to `backend`).
#   docker build -f Dockerfile -t otantik-delivery-api .

# ---- Build & publish ----
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy csproj files first and restore - this layer only invalidates when a project
# file changes, not on every source edit, so rebuilds stay fast.
COPY src/RestaurantDelivery.Core/RestaurantDelivery.Core.csproj src/RestaurantDelivery.Core/
COPY src/RestaurantDelivery.Infrastructure/RestaurantDelivery.Infrastructure.csproj src/RestaurantDelivery.Infrastructure/
COPY src/RestaurantDelivery.Api/RestaurantDelivery.Api.csproj src/RestaurantDelivery.Api/
RUN dotnet restore src/RestaurantDelivery.Api/RestaurantDelivery.Api.csproj

COPY src/ src/
RUN dotnet publish src/RestaurantDelivery.Api/RestaurantDelivery.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# musl-based Alpine needs ICU installed explicitly for culture-aware formatting (dates,
# currency) - omitting it is a common silent-crash trap. su-exec lets the entrypoint
# start as root just long enough to fix up the uploads volume's ownership, then drop to
# the base image's built-in non-root "app" user to actually run the app.
RUN apk add --no-cache icu-libs su-exec
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=build /app/publish .
COPY docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["/docker-entrypoint.sh"]
