---
name: regenerate-api
description: Regenerate the AppointMe frontend API client (orval/TanStack Query hooks + TypeScript schemas) after the backend OpenAPI surface changes. Use this whenever backend endpoints, request/response DTOs, or other contract-affecting types are added, removed, or modified — e.g. after editing files under src/AppointMe.Api, *Endpoint.cs, *Request.cs, *Response.cs, or anything else that affects the OpenAPI document. The skill runs `npm run generate:api` (which builds the API, writing src/AppointMe.Api/openapi.json, then runs orval) and reports the diff on the spec and on src/api/.
---

# regenerate-api

Regenerate `src/AppointMe.Frontend/src/api/client.ts` and `client.schemas.ts` from the build-time OpenAPI document. No running backend is needed: `npm run generate:api` builds `AppointMe.Api`, whose build writes `src/AppointMe.Api/openapi.json` (via `Microsoft.Extensions.ApiDescription.Server`), and then runs orval against that file. Orval uses `clean: true`, so the generated files are fully overwritten on each run.

## When to use

Invoke this skill after any change that alters the OpenAPI contract:
- New / renamed / removed endpoint (`*Endpoint.cs`, anything implementing `IEndpoint`)
- Changed request or response shape (`*Request.cs`, `*Response.cs`, DTOs)
- Changed route, HTTP verb, status codes, or auth attributes on an endpoint
- Changes to value objects exposed through DTOs (e.g. `PersonName`, `Email`)
- Permission attribute changes that affect the generated metadata

Do **not** invoke it for backend-only changes that don't touch the contract (internal handlers, domain events, EF config, migrations, tests).

## Preconditions

- The .NET SDK can build the solution (the script builds the API first).
- `npm` is available in `src/AppointMe.Frontend/`. This project is npm-based (`package-lock.json`, no `yarn.lock`) — never invoke `yarn` here, it would write a competing lockfile.

## Procedure

### 1. Capture the pre-state

```bash
git status --short src/AppointMe.Api/openapi.json src/AppointMe.Frontend/src/api/
```

### 2. Build the spec and regenerate the client

```bash
cd src/AppointMe.Frontend && npm run generate:api
```

The script runs `dotnet build ../AppointMe.Api/AppointMe.Api.csproj -c Release --nologo -v q && orval`. It builds in Release so it never collides with an API that Aspire is running from `bin/Debug` (Windows locks a running app's DLLs); the spec doesn't depend on the configuration. If the build fails, orval doesn't run.

Watch for:
- A build failure in the `GenerateOpenApiDocuments` step — it runs the app's startup; read the exception it prints (see Known pitfalls).
- Orval warnings about unresolved `$ref`s — usually indicates a backend serialization issue (a type missing `[JsonSerializable]` or a polymorphic type without a discriminator).
- Formatter errors — the `afterAllFilesWrite` hook runs `vp fmt` (Oxfmt); failure here means orval produced invalid output.

### 3. Report the diff and follow-ups

```bash
git diff --stat src/AppointMe.Api/openapi.json src/AppointMe.Frontend/src/api/
```

The spec diff is the contract change; the `src/api/` diff is what it means for the frontend. An empty spec diff after a contract-affecting edit means the change isn't visible in the OpenAPI document — investigate before going on.

Then check whether existing call sites still type-check against the new client:

```bash
cd src/AppointMe.Frontend && npx vp check --no-fmt --no-lint
```

This type-checks the whole project (it follows the root `tsconfig.json` references) and prints `pass: Found no type errors…` when the call sites match.

If it reports errors, those are **call sites that need updating to match the new contract** — fix them in the same task; don't leave the codebase in a broken state.

## Known pitfalls

- **Build-time generation runs the app's startup.** The `GenerateOpenApiDocuments` build step launches `Program.cs` against a mock server and starts the host. `CodeGenerationDetection.IsGeneratingOpenApiDocument()` detects this run: the app then uses the `Codegen` environment (placeholder settings in `appsettings.Codegen.json`), skips Hangfire and the database migrations, and runs Wolverine as an in-memory mediator. New startup code that needs real configuration, a database, or another service must be gated the same way, or the build fails.
- **Commit `openapi.json` with the client.** It is the source the client was generated from, and its diff is the reviewable contract change.
- **Orval alone**: `npx orval` regenerates from the committed spec without building — only useful when the spec is already current.
- **Don't hand-edit `src/api/client.ts` or `src/api/client.schemas.ts`** — orval's `clean: true` mode deletes them on each run.
- **Mutator path**: the generated client imports `apiClient` from `src/lib/axios.ts`. If you ever change that path, update `orval.config.ts` too.
- **The runtime document still exists**: `https://localhost:7233/openapi/v1.json` serves the same contract in Development (plus a `servers` entry from the request host); the client is no longer generated from it.

## Out of scope

- This skill does not commit the regenerated files; report the diff and let the user/parent task decide.
