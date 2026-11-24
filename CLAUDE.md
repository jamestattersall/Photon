# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Photon is a minimal web UI for browsing data copied from a legacy **Proton** clinical system into an MSSQL database in EAV format. The copy is produced by the `ProtonImport/` console project in this repo (formerly the separate Proton2Mssql repo). Screen layouts, captions, menus, tables and fields are all driven by Proton metadata in the database, and the UI mimics Proton's terminal look and keyboard handling. Fields/reports that depend on Quark (Proton's built-in reporting language) are not supported.

## Commands

```
dotnet build Photon.csproj
dotnet run --project Photon.csproj --launch-profile http    # http://localhost:5178, Development env
dotnet run --project Photon.csproj --launch-profile https   # https://localhost:7185
```

- Both projects target **net11.0**. There is no test project and no JS build/lint step.
- `ProtonImport/` is a separate console project (the Proton → MSSQL importer, EF Core; see `ProtonImport/readme.md`), built via `Photon.sln` (`dotnet build Photon.sln`). Because `Photon.csproj` sits at the repo root, it excludes `ProtonImport/**` via `DefaultItemExcludes` — keep that, or the web project will try to compile the importer's sources.
- Swagger UI is served at `/swagger`; `Photon.http` has sample requests.
- A real MSSQL database is required: connection string `ConnectionStrings:default` in `appsettings.json`. Logs go to `Logs/api-log-<date>.txt` (Serilog, file sink only).

## Architecture

**Backend (single project, minimal APIs):**
- `Program.cs` — all wiring and every endpoint. Routes use comma-separated path params (e.g. `/ViewValues/{viewId},{entityId},{page}`, `/Indexes/{indexTypeId},{page},{nRows}/{searchterm}`); the frontend builds these URLs, so change both sides together.
- `ProtonRepository.cs` — Dapper over `Microsoft.Data.SqlClient`, one connection per call, raw SQL against the Proton-derived schema (`EntityTypes`, `Views`, `ViewAttributes`, `Attributes`, `Indexes`, `ValueNumbers`, `ValueLongTexts`, `Menus`, `UserStarters`, …) plus DB-side objects such as the `GetViewValues` / `GetEntityIdLine` table functions and the `AttributeUse` stored procedure. DTO classes live at the bottom of this file. Proton passwords in `UserStarters` are stored XOR-31 obfuscated (`Decrypt`).
- `ProcessUpdates` (`POST /BatchUpdate`) currently only stages edits into a `BatchStaging` table; it does not yet apply them.
- `WindowsAuthHelper.cs` — AD/LDAPS credential validation and role lookup (with local-machine fallback). Contains several alternative implementations; `Program.cs` uses `ValidateAdCredentialsLdaps`, `GetUserRoles`, and `GetClaimName`.

**Auth flow (two layers):**
1. `GET /user` and `POST /login` require Negotiate (Windows auth). If `AuthSettings:RequireNetworkLogin` is true, `/login` additionally validates a typed network username/password against AD (default domain `UHB`); otherwise it uses the Negotiate identity.
2. The user must hold one of `AuthSettings:AllowedRoles`, **and** supply a Proton password matching a `UserStarters` row (which supplies their start menu/entity type/privileges).
3. On success a `WinAuthSession` HTTP-only cookie (sliding expiry) is issued; all data endpoints `RequireAuthorization()` against the cookie scheme and return 401/403 rather than redirecting.

**Frontend (`wwwroot/`, no build step):**
- `index.html` + `main.js` use **petite-vue** (vendored, minified `petite-vue.js`) with a single `reactive` `app` object mounted on `<body v-scope="app">`; keyboard handling is the `keydown` method. `graphics.js` renders graphs of numeric dated values.
- API calls go through `FetchWrapper` using `BASE_URL = new URL('.', window.location.href)`, so the app works when hosted under an IIS sub-application path — keep API URLs relative (no leading `/`).
- Static files are served with no-cache headers and `MapFallbackToFile("index.html")`.

## Deployment notes (from readme)

Runs standalone via Kestrel (`Photon.exe`) or under IIS. For IIS in-process hosting, publish without single-file. The app pool identity needs access to the MSSQL database.
