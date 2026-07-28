# AbpDevTools

## Verify First
- CI in `.github/workflows/dotnet.yml` only runs `dotnet restore` and `dotnet build --no-restore`; the test step is commented out. For code changes, run `dotnet test tests/AbpDevTools.Tests/AbpDevTools.Tests.csproj` locally.
- Focused test pattern: `dotnet test tests/AbpDevTools.Tests/AbpDevTools.Tests.csproj --filter FullyQualifiedName~RunCommand_DiscoveryTests`
- Use raw `dotnet test` for repo verification. The product command `abpdev test` is app behavior and only discovers `.sln`/`.slnx` files.
- There is no separate repo lint/typecheck/codegen pipeline. `dotnet build` and `dotnet test` are the real checks.
- Docs are built with DocFX: `dotnet tool update -g docfx` then `docfx docs/docfx.json`. `docs/docfx.json` outputs to the root `_site` directory.

## Build And Pack
- Trust `src/AbpDevTools/AbpDevTools.csproj` over the README for target frameworks: Debug builds only `net10.0`; Release packs `net8.0;net9.0;net10.0`.
- Local dev build: `dotnet build AbpDevTools.sln`
- Pack the tool exactly like CI/publish does: `dotnet pack ./src/AbpDevTools/AbpDevTools.csproj -c Release --include-symbols --include-source -o ./nupkg`
- `pwsh install.ps1` is the end-to-end local tool install path: it runs `pack.ps1`, uninstalls the global tool, then reinstalls `AbpDevTools` from `./nupkg` with `--prerelease`.

## Code Structure
- `src/AbpDevTools/Program.cs` is the CLI composition root.
- New commands are not auto-discovered. After creating a command, add its type to the hard-coded `commands` array in `Startup.BuildServices()` or the CLI will never expose it.
- Non-command services/config classes use AutoRegisterInject: add `[RegisterTransient]` or `[RegisterSingleton]` and let `services.AutoRegisterFromAbpDevTools()` wire them up.
- `tests/AbpDevTools.Tests` is the only test project. Tests are unit-style and mostly use temp directories/mocks, so they do not need Docker or real ABP apps.

## Config And Discovery Quirks
- Global tool config lives under `%AppData%/abpdev` as YAML files such as `tools-configuration.yml` and `replacements.yml`. Legacy JSON config is auto-migrated on read.
- Repo/project-local overrides live in `abpdev.yml`. `PrepareCommand` creates it; `RunCommand` loads the nearest root file from `WorkingDirectory` or its parents; per-project loads also search ancestor folders via `LocalConfigurationManager`.
- Do not build new behavior on `RunConfiguration`. It is `[Obsolete]`, and `RunnableProjectsProvider` deletes `run-configuration.yml` on startup.
- Runnable project discovery is now heuristic-based: any `*.csproj` with sibling `Program.cs` is runnable. Migrate fallback also scans `Program.cs` and top-level `*Module.cs` for `--migrate-database`.

## Current Baseline
- `dotnet test tests/AbpDevTools.Tests/AbpDevTools.Tests.csproj` currently passes, but expect existing warnings: duplicate `FluentAssertions` references in the test csproj, xUnit version resolution warnings, and many nullable/obsolete warnings in both projects.
