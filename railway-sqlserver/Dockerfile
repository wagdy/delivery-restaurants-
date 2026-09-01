# One-off SQL Server service for Railway, dedicated to otantik-delivery only.
#
# Wraps the official SQL Server image to fix a Railway-specific volume permission issue:
# the image runs as a non-root "mssql" user (uid 10001) by default, but a fresh Railway
# volume mount is root-owned, so SQL Server can't create /var/opt/mssql/.system and
# crash-loops with "Access Denied". SQL Server on Linux supports running as root (just
# logs a warning) - simplest fix is to not drop privileges, since this container isn't
# exposed publicly anyway (no public domain; only the api service reaches it over
# Railway's private network). Same fix otantik-loyalty's railway-mssql/Dockerfile uses.
FROM mcr.microsoft.com/mssql/server:2022-latest
USER root
