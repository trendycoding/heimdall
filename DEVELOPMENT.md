# Heimdall Access — Local Development Guide

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for SQL Server, Azurite, and optional Redis)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [EF Core CLI tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) (`dotnet tool install --global dotnet-ef`)

## Getting Started

### 1. Start the local environment

```bash
docker compose up -d
```

This starts:
- **SQL Server 2022** on `localhost:1433` (sa / `Heimdall_Dev_P@ss1`)
- **Azurite** (Azure Storage emulator) on ports 10000–10002
- **Redis** (optional, use `docker compose --profile with-redis up -d` to include)

### 2. Run database migrations

Migrations are applied **automatically** when the API starts in the Development environment. To run them manually:

```bash
dotnet ef database update --project src/Heimdall.Infrastructure --startup-project src/Heimdall.Api
```

To add a new migration after modifying entity configurations:

```bash
dotnet ef migrations add <MigrationName> --project src/Heimdall.Infrastructure --startup-project src/Heimdall.Api --output-dir Persistence/Migrations
```

### 3. Run the API

```bash
dotnet run --project src/Heimdall.Api
```

The API launches at `https://localhost:7001` (and `http://localhost:5001`). Swagger UI is available at `/swagger` in Development mode.

### 4. Run tests

```bash
dotnet test
```

To run a specific test project:

```bash
dotnet test tests/Heimdall.Domain.Tests
dotnet test tests/Heimdall.Application.Tests
dotnet test tests/Heimdall.Infrastructure.Tests
```

## Connection Strings

The Development connection string is configured in `src/Heimdall.Api/appsettings.Development.json`. Default:

```
Server=localhost,1433;Database=HeimdallAccess;User Id=sa;Password=Heimdall_Dev_P@ss1;TrustServerCertificate=True;MultipleActiveResultSets=true
```

## Troubleshooting

| Issue | Solution |
|-------|----------|
| SQL Server container not starting | Ensure Docker has at least 2 GB RAM allocated |
| Port 1433 in use | Stop other SQL instances or change the port mapping in `docker-compose.yml` |
| Migration fails | Ensure SQL Server is healthy: `docker compose ps` should show `healthy` |
| EF tools not found | Install with `dotnet tool install --global dotnet-ef` |
