# Repository Guidelines

## Project Structure & Module Organization
`PredictiveBudget.sln` is the solution entry point. Source lives under `src/`:

- `src/WebBudget/`: legacy ASP.NET Core Blazor Server app. Ignore this project for new work unless explicitly asked to touch it.
- `src/PredictiveBudget.Web/`: active MudBlazor-based Blazor Web App for the Raspberry Pi-hosted local UI.
- `src/PredictiveBudget.Domain/`: domain model and forecasting logic (`BudgetPlans/`, `Forecasting/`, `Common/`).
- `src/PredictiveBudget.Application/`: application-layer interfaces and future feature orchestration.

Ignore `bin/`, `obj/`, `.dotnet/`, and local SQLite outputs under `src/PredictiveBudget.Web/App_Data/`. The legacy web project currently includes a local SQLite file (`src/WebBudget/Budget.db`) for development.

## Build, Test, and Development Commands
- `dotnet build PredictiveBudget.sln`: build all projects from the repo root.
- `dotnet run --project src/PredictiveBudget.Web/PredictiveBudget.Web.csproj`: start the active Blazor app locally.
- `dotnet watch run --project src/PredictiveBudget.Web/PredictiveBudget.Web.csproj`: run with live reload during UI work.
- `dotnet test`: run automated tests when test projects are added.

Default local launch settings use `http://localhost:5112` in Development.

## Coding Style & Naming Conventions
Follow existing C# conventions in the repo:

- Use 4-space indentation and file-scoped namespaces.
- Keep nullable reference types enabled and avoid suppressions unless necessary.
- Use `PascalCase` for types and public members, `camelCase` for locals/parameters, and prefix interfaces with `I`.
- Match the current folder-to-namespace layout, for example `PredictiveBudget.Domain.Forecasting`.
- Use centralized NuGet package management from `Directory.Packages.props` for non-legacy projects. Do not migrate `src/WebBudget/` into this setup unless explicitly requested.
- Build new UI and persistence work in `src/PredictiveBudget.Web/` unless the task explicitly targets the legacy project.

There is no repo-wide formatter or `.editorconfig` yet, so keep changes consistent with surrounding files and use standard `dotnet format` conventions if you format manually.

## Testing Guidelines
There are currently no dedicated test projects in the solution. Add new tests as separate `*.Tests` projects under `src/` or a top-level `tests/` folder. Name test files after the subject under test, for example `ForecastEngineTests.cs`, and cover domain forecasting rules plus service-level regressions before changing business logic.

## Commit & Pull Request Guidelines
Recent commits use short, imperative summaries such as `Reorganize Domain App` and `Added some Interfaces`. Keep commit titles concise, focused on one change, and in plain English.

Pull requests should include:

- a short description of the behavior change
- linked issue or task, if one exists
- screenshots for UI changes in `src/WebBudget/Pages`
- notes on database, config, or migration impacts
