# =============================================================================
#  FinanceTracker by WebCherry - container image
#
#  Build:  docker build -t webcherry/financetracker .
#  Run:    docker run -p 8080:8080 -v financetracker-data:/data webcherry/financetracker
#
#  Two stages: the SDK image compiles the app, then only the published output is copied
#  into the much smaller ASP.NET runtime image. No source code or SDK ships to production.
#
#  Hardening: runs as the built-in non-root "app" user; the database and data-protection
#  keys live on the /data volume so they survive redeploys.
#  Tip: pin both FROM lines by digest (image@sha256:...) and let Dependabot bump them.
# =============================================================================

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached unless the project file changes), then copy the rest and publish.
COPY PersonalFinanceTracker.Web/PersonalFinanceTracker.Web.csproj PersonalFinanceTracker.Web/
RUN dotnet restore PersonalFinanceTracker.Web/PersonalFinanceTracker.Web.csproj

COPY PersonalFinanceTracker.Web/ PersonalFinanceTracker.Web/
RUN dotnet publish PersonalFinanceTracker.Web/PersonalFinanceTracker.Web.csproj \
    --configuration Release --no-restore --output /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__AppDbContext="Data Source=/data/finance.db" \
    DataProtection__KeysPath=/data/keys \
    ReverseProxy__Enabled=false
# On a cloud host that puts HTTPS in front of the container (Azure Container Apps, App Service,
# Fly.io, Render...), set ReverseProxy__Enabled=true there so real client IPs are used.

# /data must be writable by the non-root user (UID 1654 in the official .NET images).
RUN mkdir -p /data/keys && chown -R app:app /data

COPY --from=build /app .

USER app
VOLUME ["/data"]
EXPOSE 8080

# Health: point your platform's HTTP probe at GET /healthz (the runtime image has no curl).
ENTRYPOINT ["dotnet", "PersonalFinanceTracker.Web.dll"]
