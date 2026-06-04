  # ── CivicOps Command API — Multi-stage Dockerfile ──────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore (layer caching)
COPY ["src/CivicOps.Api/CivicOps.Api.csproj", "src/CivicOps.Api/"]
COPY ["src/CivicOps.Application/CivicOps.Application.csproj", "src/CivicOps.Application/"]
COPY ["src/CivicOps.Domain/CivicOps.Domain.csproj", "src/CivicOps.Domain/"]
COPY ["src/CivicOps.Infrastructure/CivicOps.Infrastructure.csproj", "src/CivicOps.Infrastructure/"]
RUN dotnet restore "src/CivicOps.Api/CivicOps.Api.csproj"

# Copy everything and build
COPY . .
WORKDIR "/src/src/CivicOps.Api"
RUN dotnet build "CivicOps.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CivicOps.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ── Runtime image ──────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install curl for health checks + libgdiplus for PDF generation
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl libgdiplus libc6-dev \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r civicops && useradd -r -g civicops civicops
COPY --from=publish /app/publish .
RUN mkdir -p /app/logs && chown -R civicops:civicops /app

USER civicops
EXPOSE 80
EXPOSE 443

ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

HEALTHCHECK --interval=30s --timeout=10s --retries=3 --start-period=40s \
    CMD curl -f http://localhost:80/health || exit 1

ENTRYPOINT ["dotnet", "CivicOps.Api.dll"]
