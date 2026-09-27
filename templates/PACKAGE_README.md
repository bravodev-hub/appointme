# AppointMe template

A `dotnet new` template for a production-grade **modular-monolith .NET 10 + React 19
SaaS foundation**: multi-tenancy, OIDC auth, CQRS, domain events, durable messaging,
an auto-discovered permission system, a business dashboard, and a one-command
.NET Aspire local stack.

Try the app first in the [live demo](https://app.appointme.dev/api/v1/login/demo). No sign-up needed.

## Create a project

```bash
dotnet new install BravoDev.AppointMe.Templates
dotnet new appointme -n Contoso.Booking
```

`-n` must be a dotted .NET identifier such as `Contoso.Booking`. It becomes the
namespaces, project and solution names, the database, the Keycloak realm, and the
container and Aspire resource names. The domain vocabulary (`Appointment`,
`/appointments`, …) is left alone.

## Run it

You need the .NET 10 SDK, Docker, and Node.js 22.

```bash
dotnet dev-certs https --trust          # one-time, per machine
cd Contoso.Booking/src/Contoso.Booking.Aspire
dotnet run
```

Aspire starts SQL Server, Keycloak, Mailpit, the API and the frontend. It also
applies the migrations and seeds demo data. The app comes up on https://localhost:5173.
The generated `README.md` lists the seeded demo login, which is named after your project.

## What's inside

- **Modular monolith**: Identity, Organizations, CRM and Booking bounded contexts,
  each with its own `DbContext` and schema, organized by vertical slice.
- **Auth**: OIDC with JWT Bearer for the API and cookies for browser flows.
  Keycloak runs locally, and Entra External ID is used for the Azure deployment.
- **Multi-tenancy**: company resolution per request, with tenant filters on both the
  EF Core and the Dapper paths.
- **CQRS + DDD**: EF Core aggregates and domain events for writes, Dapper for reads,
  and Wolverine messaging over a durable SQL transport.
- **Permissions**: auto-discovered and role-based, with per-company overrides.
- **Typed frontend**: React 19, TanStack Query hooks, and TypeScript types generated
  from the OpenAPI spec.
- **Deployment**: Bicep IaC for Azure App Service, SQL, Key Vault and Container
  Registry, plus GitHub Actions CI with OIDC.

## Links

- [Source and documentation](https://github.com/bravodev-hub/appointme)
- [Changelog](https://github.com/bravodev-hub/appointme/blob/main/CHANGELOG.md)
- [Issues](https://github.com/bravodev-hub/appointme/issues)
