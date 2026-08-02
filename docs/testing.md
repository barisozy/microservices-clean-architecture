# Test System Usage Guide

This document explains how to run the repository's unit, integration, and contract tests locally, in Visual Studio, and in CI. The primary IDE-independent entry point is the cross-platform `eng/ECommerce.TestRunner` .NET CLI.

## Quick start

Open PowerShell at the repository root:

```bash
dotnet run --project eng/ECommerce.TestRunner -- --suite Unit
```

Generate and open an HTML coverage report:

```bash
dotnet run --project eng/ECommerce.TestRunner -- --suite Coverage --open-report
```

Normal unit test runs do not collect coverage; coverage is intentionally a separate command.

## Requirements

- Install the .NET SDK compatible with `global.json`.
- Run Docker Desktop or another reachable Docker daemon for integration tests.
- Install the `ReportGenerator` global tool for HTML coverage reports.
- Fine Code Coverage is an optional Visual Studio extension.

```powershell
dotnet --version
dotnet tool install --global dotnet-reportgenerator-globaltool
dotnet tool update --global dotnet-reportgenerator-globaltool
```

## Test suites

| Suite | Solution filter | Category | Infrastructure | Purpose |
|---|---|---|---|---|
| Unit | `ECommerce.UnitTests.slnf` | `Unit` | None | Domain and Application behavior |
| Integration | `ECommerce.IntegrationTests.slnf` | `Integration` | Docker/Testcontainers | Postgres, RabbitMQ, Valkey, and service integration |
| Contract | `ECommerce.ContractTests.slnf` | `Contract` | No Docker required | Consumer/provider contracts |

`ECommerce.sln` is the repository's single authoritative solution. `.slnf` files are lightweight filters over that solution, not separate solutions.

## Test commands

```bash
# Unit tests (the default suite)
dotnet run --project eng/ECommerce.TestRunner -- --suite Unit

# Integration tests; Docker must be running
dotnet run --project eng/ECommerce.TestRunner -- --suite Integration

# Contract tests; generated Pact files are written to artifacts/pacts
dotnet run --project eng/ECommerce.TestRunner -- --suite Contract

# All tests, in Unit -> Integration -> Contract order
dotnet run --project eng/ECommerce.TestRunner -- --suite All

# Coverage, with an optional browser report
dotnet run --project eng/ECommerce.TestRunner -- --suite Coverage
dotnet run --project eng/ECommerce.TestRunner -- --suite Coverage --open-report
```

The script stops when a suite fails. `All` does not generate coverage. Testcontainers manages container creation, readiness, and cleanup.

The HTML report is `artifacts/coverage/local/index.html`. Use `--configuration Debug` when needed. If packages are already restored, `--no-restore` skips restoration. The runner restores the repository-local ReportGenerator tool automatically for coverage runs.

## Run a specific test

In Visual Studio Test Explorer, right-click a test and select `Run` or `Debug`. From the CLI:

```powershell
dotnet test .\tests\Order.UnitTests\Order.UnitTests.csproj `
  --configuration Release `
  --filter "FullyQualifiedName~CreateOrder" `
  --settings .\.runsettings `
  --disable-build-servers -m:1
```

List tests with `dotnet test .\ECommerce.UnitTests.slnf --list-tests --disable-build-servers -m:1`.

## Coverage workflow

Coverlet is the coverage provider. `coverage.runsettings` configures the `XPlat Code Coverage` collector and Cobertura format; the Microsoft `Code Coverage` collector is not used.

```text
xUnit tests -> Coverlet -> coverage.cobertura.xml -> ReportGenerator -> index.html
```

- **Line coverage:** Percentage of executable lines that ran.
- **Branch coverage:** Percentage of conditional branches that ran.
- **Method coverage:** Percentage of methods run at least once.

High line coverage alone does not prove test quality. Review error paths, boundary values, branch coverage, and behavioral assertions.

The core coverage report includes `*.Domain`, `*.Application`, and `Audit`. It excludes test assemblies, generated contracts, `ECommerce.AppHost`, EF migrations, generated C# files, and code marked with `ExcludeFromCodeCoverage`. Infrastructure and API behavior is primarily verified by integration tests.

| Output | Location |
|---|---|
| Unit TRX | `artifacts/test-results/unit` |
| Integration TRX | `artifacts/test-results/integration` |
| Contract TRX | `artifacts/test-results/contract` |
| Raw coverage | `artifacts/test-results/coverage` |
| HTML report | `artifacts/coverage/local/index.html` |
| Pact files | `artifacts/pacts` |

Generated artifacts are not committed to Git. The script removes only the previous result directory for the selected suite.

## Visual Studio

Open `ECommerce.sln` for normal development or `ECommerce.UnitTests.slnf` for unit tests only. Use `Test -> Test Explorer`; normal Test Explorer runs are not expected to produce coverage.

Fine Code Coverage is optional. Install it from `Extensions -> Manage Extensions`, restart Visual Studio, and use the repository's `finecodecoverage-settings.xml` so it uses Coverlet. On incompatible IDE versions, use `dotnet run --project eng/ECommerce.TestRunner -- --suite Coverage --open-report` instead.

Do not use `Test -> Analyze Code Coverage` in this repository. It may invoke the Microsoft profiler and produce misleading `Empty results generated`, `No binaries were instrumented`, or `%0.00` output. Use the standard Coverlet command.

## Common issues

- **Empty coverage or `%0.00`:** Run `dotnet run --project eng/ECommerce.TestRunner -- --suite Coverage --open-report` and use `artifacts/coverage/local/index.html` as the authoritative report.
- **ReportGenerator restore failure:** Verify NuGet access, run `dotnet tool restore`, then rerun Coverage.
- **Docker connection failure:** Start Docker Desktop, wait for the engine, verify daemon access, and rerun `-Suite Integration`.
- **SDK not found:** Compare `dotnet --version` with `global.json` and install the requested .NET 10 SDK feature band. Do not alter `global.json` for a local machine.
- **Stale report:** Rerun Coverage and hard-refresh the browser.

## Test-writing rules

- Put Domain and Application behavior tests in the relevant `*.UnitTests` project.
- Put real Postgres, RabbitMQ, Valkey, HTTP host, and cross-service behavior in `ECommerce.IntegrationTests`.
- Put consumer/provider compatibility tests in `ECommerce.ContractTests`.
- Unit tests must not depend on Infrastructure.
- Test names must describe behavior and the expected result.
- Do not add superficial tests that only assert `true` or fail to observe production behavior.
- Keep shared-container integration tests in the shared `IntegrationTests` xUnit collection; run only fully isolated tests in parallel.

Common xUnit, Coverlet, and test SDK packages are managed through `tests/Directory.Build.props`. New test projects must end with `.UnitTests`, `.IntegrationTests`, or `.ContractTests`; otherwise the build intentionally fails and no suite category is assigned.

## CI behavior

`.github/workflows/test-coverage.yml` restores the solution, runs unit tests with Coverlet, runs integration and contract tests, generates the core assembly report, fails below `%80` line coverage, and uploads TRX, Cobertura, and HTML reports as 14-day GitHub Actions artifacts.

The local script and CI use the same solution filters, coverage settings, and core assembly filter.

## Configuration files

| File | Responsibility |
|---|---|
| `eng/ECommerce.TestRunner` | Cross-platform, typed entry point for local tests |
| `.config/dotnet-tools.json` | Pinned repository-local ReportGenerator tool |
| `.runsettings` | Standard IDE/CLI test settings without coverage |
| `coverage.runsettings` | Coverlet, Cobertura, and exclusion settings |
| `finecodecoverage-settings.xml` | Optional Fine Code Coverage settings |
| `tests/Directory.Build.props` | Central test packages and shared test properties |
| `tests/xunit.runner.json` | xUnit execution and parallelism settings |
| `ECommerce.*Tests.slnf` | Suite-based solution filters |
| `.github/workflows/test-coverage.yml` | CI tests, coverage gate, and artifact upload |
