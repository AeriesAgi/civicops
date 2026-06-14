# syntax=docker/dockerfile:1

# ─────────────────────────────────────────────────────────────────────────────
# CivicOps Command — production container (ASP.NET Core, .NET 10)
# Multi-stage: restore+publish on the SDK image, run on the slim ASP.NET runtime.
# The platform injects $PORT; the app binds to it (see Program.cs). Band runs in
# Simulation mode by default, so the hosted demo works with zero secrets.
# ─────────────────────────────────────────────────────────────────────────────

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore as its own layer for better caching.
COPY CivicOps.csproj ./
RUN dotnet restore CivicOps.csproj

# Copy the rest and publish. enterprise-platform/ is excluded from compilation by
# the csproj, and from the build context by .dockerignore.
COPY . .
RUN dotnet publish CivicOps.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    PORT=8080

EXPOSE 8080
ENTRYPOINT ["dotnet", "CivicOps.dll"]
