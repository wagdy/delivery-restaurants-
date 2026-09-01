# One-off SQL Server service for Railway, dedicated to otantik-delivery only.
#
# Wraps the official SQL Server image to fix a Railway-specific volume permission issue:
# the image runs as a non-root "mssql" user (uid 10001) by default, but a fresh Railway
# volume mount is root-owned, so SQL Server can't create /var/opt/mssql/.system and
# crash-loops with "Access Denied". SQL Server on Linux supports running as root (just
# logs a warning) - simplest fix is to not drop privileges, since this container isn't
# exposed publicly anyway (no public domain; only the api service reaches it over
# Railway's private network). Same fix otantik-loyalty's railway-mssql/Dockerfile uses.
#
# Pinned to 2019, not 2022: 2022-latest crashes on startup here with a SQLPAL stack
# overflow in NUMA node initialization (a known SQL Server-on-Linux bug triggered by
# Railway containers reporting a very high visible CPU count). otantik-loyalty's
# identical setup on Railway uses 2019 and is proven stable.
FROM mcr.microsoft.com/mssql/server:2019-latest
USER root
