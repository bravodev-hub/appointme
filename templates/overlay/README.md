# AppointMe

A modular-monolith .NET 10 + React 19 multi-tenant SaaS foundation, generated
from a production-grade template.

> Generated from a `dotnet new` template. The `-n <Name>` you passed must be a valid
> dotted .NET identifier — e.g. `Contoso.Booking` — because it becomes C#
> namespaces, project names, and folder paths. A non-dotted name such as
> `my-booking-app` (a common choice for kebab-case slugs) produces a solution
> that does not compile — regenerate with a dotted name if that's what happened
> here.

## Run it

```bash
cd src/AppointMe.Aspire && dotnet run
```

.NET Aspire starts SQL Server, Keycloak, Mailpit, the API and the frontend, applies
database migrations, and seeds demo data. The frontend comes up on
https://localhost:5173.

Prefer to run the backing services yourself? First, one-time setup so Keycloak's
HTTPS works outside Aspire — trust the ASP.NET Core dev certificate and export it
for Keycloak to read (`docker/keycloak/export-dev-cert.sh` does both steps, or run
them yourself):

```bash
dotnet dev-certs https --trust
dotnet dev-certs https --format PEM --no-password -ep docker/keycloak/certs/keycloak.crt
```

Then `docker compose up` brings up SQL Server, Keycloak and Mailpit on the same
ports Aspire uses; run `dotnet run --project src/AppointMe.Api`, and in
`src/AppointMe.Frontend`, `npm ci` followed by `npm run dev`.

## What's inside

- **Modular monolith** — Identity, Organizations, CRM and Booking, each a bounded
  context with its own `DbContext` and schema, organized by vertical slice.
- **Auth** — OIDC with a hybrid scheme: JWT Bearer for the API, cookies for browser
  flows. Keycloak locally, Entra External ID for the Azure deployment.
- **Multi-tenancy** — company resolution via header/claim, EF Core query filters on
  the command path, Dapper reads carrying the tenant predicate by convention.
- **CQRS + DDD** — EF Core aggregates and domain events for writes, Dapper for reads,
  Wolverine for async messaging over a durable SQL transport.
- **Permissions** — auto-discovered, role-based, with default grant policies.
- **Business dashboard** — KPIs, trends, staff load and a peak-hours heatmap.
- **Typed frontend** — TanStack Query hooks and TypeScript types generated from the
  backend OpenAPI spec via orval.
- **Deployment** — `infra/` holds Bicep IaC for Azure App Service, SQL, Key Vault,
  Container Registry and Application Insights, plus a Cloudflare Worker proxy.

`CLAUDE.md` documents the architecture, slice conventions and naming rules in depth.
`docs/identity-resolution.md` explains how the app separates identity from principal.

## Change these before deploying

The template renamed identifiers for you, but these carry placeholder values —
or, in one case, values that are actively wrong rather than merely unset:

- **`Directory.Build.props`** — `Company`, `Authors`, and `RepositoryUrl` are
  still BravoDev's. The rename only touches the upstream project's own brand
  tokens, and none of these three properties is made only of those
  (`RepositoryUrl` is the closest — its project-name segment did get renamed —
  but the host and org, `github.com/bravodev-hub`, did not). Left alone, every assembly you
  build embeds `Company=BravoDev`, `Authors=BravoDev`, and a `RepositoryUrl`
  pointing at BravoDev's GitHub org under your project's name. Unlike the
  other items in this list, this one is not a placeholder waiting to be
  filled in — it is incorrect metadata about who owns this code, and it ships
  in every build until you fix it.
- `src/AppointMe.Api/appsettings.Devtest.example.json` — copy to
  `appsettings.Devtest.json` and fill in your Entra tenant, client id, base URL,
  super-admin email and demo account. It is the deployment config; nothing works
  without it. (`appsettings.Devtest.json` itself is never shipped — it would leak
  real secrets — so the `.example.json` file is what you actually get.)
- `src/AppointMe.Api/appsettings.Development.json` — ships with fixed local
  Keycloak client secrets (`FrontendClientSecret`, `KeycloakAdmin.ClientSecret`)
  and the `Password1` / `AppointMe1` credential pair. These are already public
  in the upstream repo and only ever protect containers on your own machine —
  not new exposure — but every project generated from this template starts out
  sharing the exact same values. Rotate them before you rely on this
  environment for anything beyond local development.
- `infra/cloudflare-worker/wrangler.jsonc` — the `routes` pattern and `zone_name`
  point at a domain derived from your project name, which almost certainly is not
  yours.
- `.github/workflows/devtest.yml` — expects the Azure OIDC secrets listed in
  `infra/README.md` section 5. It fails until you set them, and does nothing
  harmful in the meantime.

`LICENSE` is *not* on this list on purpose: it still reads `Copyright (c) 2026
BravoDev`, and that is correct, not a leftover to delete. This project is a
derivative of BravoDev's MIT-licensed template, and the MIT license requires
every copy to retain the original copyright notice — removing it would violate
the license you're using. Add your own copyright line alongside it if you want
one; don't replace it.

## License

MIT — see `LICENSE`. Third-party notices are in `THIRD-PARTY-NOTICES.md`.
