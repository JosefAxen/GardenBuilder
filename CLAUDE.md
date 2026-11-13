# CLAUDE.md

Drömträdgård: customers design their garden in 3D from stock items and send a quote request. Read
[docs/design.md](docs/design.md) before larger changes; it is the source of truth for scope and rules.

## Commands

- Run everything locally: `dotnet run --project src/AppHost` (needs Docker running and `npm install` in `src/web`)
- Build: `dotnet build GardenBuilder.slnx`
- All .NET tests: `dotnet test --solution GardenBuilder.slnx` (Microsoft.Testing.Platform, xUnit v3)
- One test project: `dotnet test --project tests/Unit/GardenBuilder.UnitTests.csproj`
- Web: `npm --prefix src/web run dev | build | lint`
- Package versions live in `Directory.Packages.props` (central package management). Never put versions in a csproj.
- Add a migration (dotnet-ef is a local tool, run `dotnet tool restore` once):
  `dotnet ef migrations add <Name> --project src/Modules/<Module> --startup-project src/Api --context <Module>DbContext --output-dir Infrastructure/Migrations`
- Migrations are applied by `src/Migrator`, which runs before the API and worker (Aspire waits for it). Never call
  `Migrate()` from the API or the worker. Tables and columns are snake_case (EFCore.NamingConventions).

## Language

- Code, identifiers, comments and commit messages in English.
- Everything the user sees is in Swedish. Prices in SEK including VAT.

## Backend rules

- Modular monolith. Each module in `src/Modules/<Name>` owns its data in its own PostgreSQL schema.
- A module never references another module. Cross-module calls go through interfaces in `src/Contracts`.
  `tests/Architecture` enforces this.
- Inside a module, code is organized per feature: `Features/<FeatureName>/` holds endpoint, handler and validation.
  See `src/Modules/Catalog/Features/ListArticles` for the pattern. Module types are `internal` by default.
- Each module exposes `Add<Name>Module` and `Map<Name>Endpoints` and is wired in `src/Api/Program.cs`.
- Business rules go in domain objects (mostly Design and Pricing), not in endpoints.
- Money is `Money` (decimal + currency), never float. Lengths in meters. Times in UTC. Ids are UUID v7 (`Ids.New()`).
- Prices are calculated only on the server. The client only displays them.
- Only the Worker calls AI providers. AI output is validated by plain code before it touches a design.
- Treat warnings as errors (set in `Directory.Build.props`).

## Frontend rules

- The scene document is the core contract. Editor state lives in the zustand store
  (`src/web/src/editor/sceneStore.ts`); React Three Fiber renders from it. No state only in three.js objects.
- Every scene change is a command (add, move, replace, remove) so undo/redo works.
- Scene coordinates: x = east, y = north, meters. In three.js the ground is the XZ plane and north is -Z.
