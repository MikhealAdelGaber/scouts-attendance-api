# ── Build stage ────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and restore (cached layer)
COPY . .
RUN dotnet restore src/ScoutsAttendance.API/ScoutsAttendance.API.csproj

# Publish
RUN dotnet publish src/ScoutsAttendance.API/ScoutsAttendance.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Runtime stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Railway injects PORT at runtime; default to 8080 for local testing
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ScoutsAttendance.API.dll"]
