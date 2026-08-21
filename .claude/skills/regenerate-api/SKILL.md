---
name: regenerate-api
description: Regenerate the AppointMe frontend API client (orval/TanStack Query hooks + TypeScript schemas) after the backend OpenAPI surface changes. Use this whenever backend endpoints, request/response DTOs, or other contract-affecting types are added, removed, or modified — e.g. after editing files under src/AppointMe.Api, *Endpoint.cs, *Request.cs, *Response.cs, controllers, or anything that affects the OpenAPI JSON at https://localhost:7233/openapi/v1.json. The skill waits for the backend to be reachable, then runs `npm run generate:api` and reports the diff on src/api/.
---

# regenerate-api

Regenerate `src/AppointMe.Frontend/src/api/appointme.ts` and `appointme.schemas.ts` from the live backend OpenAPI document. The frontend uses orval with `clean: true`, so the generated files are fully overwritten on each run.

## When to use

Invoke this skill after any change that alters the OpenAPI contract:
- New / renamed / removed endpoint (`*Endpoint.cs`, anything implementing `IEndpoint`)
- Changed request or response shape (`*Request.cs`, `*Response.cs`, DTOs)
- Changed route, HTTP verb, status codes, or auth attributes on an endpoint
- Changes to value objects exposed through DTOs (e.g. `PersonName`, `Email`)
- Permission attribute changes that affect the generated metadata

Do **not** invoke it for backend-only changes that don't touch the contract (internal handlers, domain events, EF config, migrations, tests).

## Preconditions

- Aspire (or at least the `AppointMe.Api` project) must be running and serving the new code.
- The OpenAPI endpoint is `https://localhost:7233/openapi/v1.json` (self-signed cert, that's expected).
- `npm` is available in `src/AppointMe.Frontend/`. This project is npm-based (`package-lock.json`, no `yarn.lock`) — never invoke `yarn` here, it would write a competing lockfile.

The orval script in `package.json` sets `NODE_TLS_REJECT_UNAUTHORIZED=0` for the self-signed cert — don't add `-k` flags anywhere else.

## Procedure

Run these steps in order. Do not skip the readiness check — running orval against a stale backend silently produces a stale client.

### 1. Verify the backend is up and serving fresh code

The backend must be **restarted** after the contract change for orval to see it. Aspire's default project setup does not hot-reload new endpoint signatures.

Check reachability:

```bash
curl -k -s -o /dev/null -w "%{http_code}" https://localhost:7233/openapi/v1.json
```

Interpret:
- `200` — backend is reachable. Continue, **but** verify with the user that they restarted the API since the contract change (or restart it yourself if you own the terminal). A reachable-but-stale backend is the #1 cause of confusing diffs from this skill.
- `000` / connection refused — backend is not running. Stop and ask the user to start (or restart) Aspire:
  ```
  cd src/AppointMe.Aspire && dotnet run
  ```
  Or, if Aspire is already running, restart the `appointme-api` resource from the Aspire dashboard.

### 2. Wait for the API to become ready

Poll `/openapi/v1.json` until it returns 200, with a sane timeout (60s is enough for a warm restart; cold starts after `dotnet build` can take longer):

```bash
for i in $(seq 1 60); do
  code=$(curl -k -s -o /dev/null -w "%{http_code}" https://localhost:7233/openapi/v1.json)
  if [ "$code" = "200" ]; then echo "ready after ${i}s"; break; fi
  sleep 1
done
```

If it never reaches 200, stop and surface the user's Aspire logs — don't run orval.

### 3. Capture the pre-state for a meaningful diff

Before regeneration, note the current state of the generated files so you can show the user what changed:

```bash
cd src/AppointMe.Frontend
git status --short src/api/
```

### 4. Run the generator

```bash
cd src/AppointMe.Frontend && npm run generate:api
```

Watch for:
- Orval warnings about unresolved `$ref`s — usually indicates a backend serialization issue (a type missing `[JsonSerializable]` or a polymorphic type without a discriminator).
- Prettier errors — the `afterAllFilesWrite` hook runs `prettier --write`; failure here means orval produced invalid output.

### 5. Report the diff and follow-ups

```bash
cd src/AppointMe.Frontend && git diff --stat src/api/
```

Then check whether existing call sites still type-check against the new client:

```bash
cd src/AppointMe.Frontend && npx tsc -b
```

`tsc` **must** be pointed at a project. The root `tsconfig.json` is solution-style (`files: []` + `references`), so a bare `tsc --noEmit` checks nothing and exits 0 — it will happily "pass" a client that no longer matches its call sites. Use `-b` (what `npm run build` runs) or `-p tsconfig.app.json` for app sources only. No `--ignoreDeprecations` flag is needed; see the frontend CLAUDE.md.

If `tsc` reports errors, those are **call sites that need updating to match the new contract** — fix them in the same task; don't leave the codebase in a broken state.

## Known pitfalls

- **Stale reachable backend**: the most common failure mode. The endpoint responds 200, orval succeeds, but the OpenAPI doc still describes the old contract because the API process wasn't restarted. If the diff looks empty or doesn't reflect your edits, this is why — restart Aspire and rerun.
- **Don't hand-edit `src/api/appointme.ts` or `src/api/appointme.schemas.ts`** — orval's `clean: true` mode deletes them on each run.
- **Mutator path**: the generated client imports `apiClient` from `src/lib/axios.ts`. If you ever change that path, update `orval.config.ts` too.
- **HTTPS cert**: dev uses mkcert via `vite-plugin-mkcert`. The `NODE_TLS_REJECT_UNAUTHORIZED=0` env in the `generate:api` script bypasses verification for orval's fetch — don't remove it.

## Out of scope

- This skill does not start, stop, or restart Aspire. The user owns that terminal.
- This skill does not commit the regenerated files; report the diff and let the user/parent task decide.
