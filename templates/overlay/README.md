# AppointMe

A modular-monolith .NET 10 + React 19 multi-tenant SaaS foundation, generated
from a production-grade template.

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

The template renamed identifiers for you, but these carry placeholder values:

- `src/AppointMe.Api/appsettings.Devtest.example.json` — copy to
  `appsettings.Devtest.json` and fill in your Entra tenant, client id, base URL,
  super-admin email and demo account. It is the deployment config; nothing works
  without it. (`appsettings.Devtest.json` itself is never shipped — it would leak
  real secrets — so the `.example.json` file is what you actually get.)
- `infra/cloudflare-worker/wrangler.jsonc` — the `routes` pattern and `zone_name`
  point at a domain derived from your project name, which almost certainly is not
  yours.
- `.github/workflows/devtest.yml` — expects the Azure OIDC secrets listed in
  `infra/README.md` section 5. It fails until you set them, and does nothing
  harmful in the meantime.

## License

MIT — see `LICENSE`. Third-party notices are in `THIRD-PARTY-NOTICES.md`.
