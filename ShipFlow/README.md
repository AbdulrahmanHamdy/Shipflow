# ShipFlow

Smart Logistics and Shipment Management Platform — **backend only** (ASP.NET Core Web API, .NET 8, Clean Architecture,
Modular Monolith). No frontend code belongs in this repository.

> **Status:** Phase 1 (Domain) implemented on top of Phase 0 (Foundation); no persistence or API endpoints beyond `/health` yet. This is not a production-ready system. See [PROJECT_STATUS.md](PROJECT_STATUS.md).

## Layout

```
src/
  ShipFlow.Domain/           Entities, value objects, rules (no external dependencies)
  ShipFlow.Application/      Feature-organized use cases, interfaces
  ShipFlow.Infrastructure/   Persistence, identity, external services
  ShipFlow.API/              Composition root, HTTP endpoints
tests/
  ShipFlow.Domain.Tests/
  ShipFlow.Application.Tests/
  ShipFlow.API.IntegrationTests/
docs/                        ARCHITECTURE, DECISIONS, API_CONTRACTS, TESTING, SETUP
```

## Quick start

```bash
dotnet build ShipFlow.sln
dotnet run --project src/ShipFlow.API
curl http://localhost:5177/health
dotnet test ShipFlow.sln
```

Full instructions: [docs/SETUP.md](docs/SETUP.md). Prerequisite: .NET SDK 8.0.

## Project documents

- [PROJECT_STATUS.md](PROJECT_STATUS.md) — what is done, verified results, known issues
- [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) — phase roadmap
- [CHANGELOG.md](CHANGELOG.md)
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [docs/DECISIONS.md](docs/DECISIONS.md),
  [docs/API_CONTRACTS.md](docs/API_CONTRACTS.md), [docs/TESTING.md](docs/TESTING.md)

## Security

Never commit real secrets. Use `.env.example` as a template; `.env` is git-ignored.
