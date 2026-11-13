# GardenBuilder – Drömträdgård

A web application where customers upload photos of their garden, get it rebuilt as a stylized 3D scene, receive
AI design suggestions from the company's own stock, shape their own design and send a quote request. A salesperson
opens the same design in admin and sends the binding quote.

The full design is in [docs/design.md](docs/design.md) (Swedish).

## Prerequisites

- .NET SDK 10 (see `global.json`)
- Node.js 26 LTS
- Docker Desktop (PostgreSQL and the storage emulator run in containers, and integration tests use Testcontainers)

## Run locally

Aspire starts PostgreSQL, the Azure Storage emulator, the API, the worker and the web client with one command:

```bash
cd src/web && npm install && cd ../..
dotnet run --project src/AppHost
```

The Aspire dashboard opens in the browser and links to every service. The web client runs on Vite and proxies
`/api` to the API.

## Test

```bash
dotnet test --solution GardenBuilder.slnx
```

```bash
npm --prefix src/web run lint
```

## Structure

```text
src/
  Api/              Host for the API and the built web client, composes the modules
  Worker/           Host for background jobs (the only process that calls AI providers)
  AppHost/          Aspire, local environment
  ServiceDefaults/  OpenTelemetry, health checks, resilience
  Modules/          Garden, Catalog, Design, Pricing, Leads, Ai, Identity
  Contracts/        Interfaces and data objects between modules
  SharedKernel/     Money, length, area, polygon
  web/              React + TypeScript + React Three Fiber
    src/app/          Customer flow
    src/editor/       3D editor
    src/admin/        Admin
    src/api/          API client and scene types
tests/              Unit, Integration, Architecture, E2E
infra/              Bicep
assets/             Tools for optimizing 3D models
evals/              Test photos and expected AI results
docs/               Design, decisions and prompts
```
