# syntax=docker/dockerfile:1
# Multi-stage build for the Ticket System API (.NET 8).

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files first for layer caching.
COPY global.json Directory.Build.props ./
COPY src/TicketSystem.Domain/TicketSystem.Domain.csproj src/TicketSystem.Domain/
COPY src/TicketSystem.Application/TicketSystem.Application.csproj src/TicketSystem.Application/
COPY src/TicketSystem.Infrastructure/TicketSystem.Infrastructure.csproj src/TicketSystem.Infrastructure/
COPY src/TicketSystem.Api/TicketSystem.Api.csproj src/TicketSystem.Api/

RUN dotnet restore src/TicketSystem.Api/TicketSystem.Api.csproj

COPY . .
RUN dotnet publish src/TicketSystem.Api/TicketSystem.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Secure defaults: bind explicitly and default to Production. Compose overrides to Development.
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# aspnet:8.0 provides $APP_UID (non-root). Do NOT run as root.
USER $APP_UID

ENTRYPOINT ["dotnet", "TicketSystem.Api.dll"]
