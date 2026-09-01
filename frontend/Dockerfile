# syntax=docker/dockerfile:1
#
# Build context must be the `frontend/` directory (on Railway: set this service's
# Root Directory to `frontend`).
#   docker build -f Dockerfile -t otantik-delivery-web .

# ---- Build ----
FROM node:22-alpine AS build
WORKDIR /app

# Install first - this layer only invalidates when package*.json changes, not on
# every source edit.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build -- --configuration production

# ---- Serve ----
FROM nginx:1.27-alpine AS runtime

COPY nginx.conf /etc/nginx/conf.d/default.conf
# The esbuild-based "application" builder (angular.json) always nests output under a
# browser/ subfolder, regardless of outputPath.
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html

EXPOSE 80
