# Connecto API

Connecto is a marketplace API designed to connect people who have needs with others who can offer relevant help, expertise, services, or support. The platform is centered around discovering and matching user needs with suitable offerings in a structured and scalable way.

## Overview

This API supports the core workflow of the Connecto platform:

- User profile creation and management
- Need creation and tracking
- Offering creation and tracking
- Match discovery and scoring
- Listing parsing for structured marketplace data
- Swagger/OpenAPI documentation for local development and testing

## Solution structure

The solution is organized into the following projects:

- `Connecto.Api` – ASP.NET Core Web API
- `Connecto.Application` – application services, business rules, calculators, and matching logic
- `Connecto.Domain` – entities, requests, responses, matching models, and shared domain concepts
- `Connecto.Infrastructure` – EF Core data access, database context, migrations, and seed data
- `Connecto.Tests.Unit` – unit tests covering the application logic

## Prerequisites

Before running the project locally, make sure you have:

- .NET 10 SDK
- SQL Server LocalDB or another SQL Server instance
- Visual Studio 2026 or another compatible .NET 10 development environment

## Local setup

1. Restore NuGet packages:

   ```bash
   dotnet restore
   ```

2. Confirm the development configuration:

   - Review `Connecto.Api/appsettings.Development.json`
   - Update the `ConnectionStrings:DefaultConnection` value to match your local database

3. Run the API:

   ```bash
   dotnet run --project Connecto.Api/Connecto.Api.csproj
   ```

4. Open the Swagger UI in a browser:

   - HTTPS: `https://localhost:7249/swagger`
   - HTTP: `http://localhost:5247/swagger`

## Configuration

The application uses the standard ASP.NET Core configuration pipeline:

- `appsettings.json` – base configuration
- `appsettings.Development.json` – local development overrides
- environment variables – production and deployment overrides

The project is configured to use the `Development` environment via `launchSettings.json`, which sets:

```json
"ASPNETCORE_ENVIRONMENT": "Development"
```

This ensures the local development configuration is automatically picked up during debugging.

## Database

The project uses Entity Framework Core with SQL Server.

### Current database configuration

- `ConnectoDbContext` is the primary EF Core context
- The default database connection is configured in `ConnectionStrings:DefaultConnection`
- Seed data is configured in `Connecto.Infrastructure/Services/Data/Seed/SeedData.cs`

### Migrations

To apply migrations locally:

```bash
dotnet ef database update --project Connecto.Infrastructure/Connecto.Infrastructure.csproj --startup-project Connecto.Api/Connecto.Api.csproj
```

To create a new migration:

```bash
dotnet ef migrations add <MigrationName> --project Connecto.Infrastructure/Connecto.Infrastructure.csproj --startup-project Connecto.Api/Connecto.Api.csproj
```

## Swagger / OpenAPI

Swagger is enabled in development mode so the API can be explored and tested without an external client.

Open the Swagger UI to:

- browse all endpoints
- inspect request and response models
- test API actions directly from the browser

## API structure

The API is organized around core domain flows:

- `api/user` – user profile and account operations
- `api/needs` – create and manage needs
- `api/offerings` – create and manage offerings
- `api/matches` – match previews and related information
- `api/discover` – discovery flows
- `api/listing-parse` – parse marketplace listing text into structured data

## Matching engine

The matching engine evaluates a need against an offering using weighted factors such as:

- requirements match
- skill overlap
- location fit
- budget alignment
- reputation
- experience
- availability

These factors are evaluated by dedicated calculators in `Connecto.Application/Internal/Calculators`.

## Deployment

The project is designed for standard ASP.NET Core hosting and can be deployed to environments such as:

- Azure App Service
- Azure Container Apps
- IIS
- other Windows or Linux servers hosting ASP.NET Core applications

### Deployment considerations

- use secure production configuration values
- avoid checking secrets into source control
- configure production connection strings and API keys in secure environment variables or secret stores
- enforce HTTPS in production

## Security notes

- Never commit production secrets or API keys
- Prefer environment variables, Azure Key Vault, or another secure secret store for sensitive values
- Keep the development connection string and local-only values in `appsettings.Development.json` only

## Project status

This repository is currently in active development. APIs and domain behavior may evolve as the product expands.

## Typical development workflow

1. Start the API locally with `dotnet run`
2. Open Swagger in the browser
3. Use the UI to test endpoints or call them with curl/Postman
4. Validate business logic via unit tests
5. Apply any required EF Core migrations before sharing schema changes

## Example endpoint usage

### Health / quick check

```bash
curl -X GET "https://localhost:7249/swagger"
```

### Create a user

```bash
curl -X POST "https://localhost:7249/api/user/create" \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "Jane",
    "middleName": "Marie",
    "lastName": "Doe",
    "email": "jane.doe@example.com",
    "address": "123 Main St",
    "city": "Auckland",
    "country": "New Zealand",
    "bio": "Interested in design and community support",
    "password": "SecurePass123!"
  }'
```

### Parse a listing

```bash
curl -X POST "https://localhost:7249/api/listing-parse" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Need a web developer",
    "description": "I need a developer to build a landing page and improve our booking flow."
  }'
```

## Contributor guidance

- Keep controllers thin and delegate business logic to application services
- Prefer DTOs and domain models over passing raw request objects across layers
- Validate input at the application layer before persisting to the database
- Keep matching rules and scoring logic in `Connecto.Application` rather than controller code
- Use unit tests for calculators and service logic;

## Monitoring and diagnostics

During development, watch for:

- unhandled exceptions in controllers
- nullable warnings in application services
- EF Core model mismatches between entities and migrations
- environment-specific configuration issues between local and production setups

## Useful commands

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project Connecto.Api/Connecto.Api.csproj
```

## License

This project does not currently include a project-specific license file. Review repository ownership and licensing requirements before redistribution or commercial use.
