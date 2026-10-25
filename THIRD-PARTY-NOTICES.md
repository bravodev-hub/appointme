# Third-Party Notices

AppointMe itself is released under the [MIT License](./LICENSE). It is built on
open-source components that remain under **their own** licenses.

## Do you need to ship these license files?

For the way AppointMe consumes them — as package dependencies pulled by NuGet and
npm at build time — you are **not** required to copy each dependency's license
text into this repository. Those packages travel with their own licenses through
the package managers.

The obligations apply when you **redistribute** those components, for example in a
published binary or a Docker image. Permissive licenses (MIT, Apache-2.0, ISC,
BSD) then require you to **preserve the original copyright and license notices**.
This file provides that attribution and flags the one dependency with copyleft
terms (Hangfire, LGPL-3.0) so you can make an informed choice.

> This is a curated summary, not an exhaustive transitive manifest, and is not
> legal advice. For a complete, authoritative list (including transitive
> dependencies), generate one from the lockfiles — see
> [Generating a complete report](#generating-a-complete-report).

## Backend (.NET / NuGet)

| License | Components |
|---|---|
| **MIT** | .NET runtime & ASP.NET Core, Entity Framework Core, `Microsoft.Extensions.*`, `Microsoft.Data.SqlClient`, `Microsoft.AspNetCore.Authentication.*`, `Microsoft.AspNetCore.OpenApi`, Asp.Versioning, .NET Aspire (`Aspire.Hosting.*`), `Azure.Identity`, `Azure.Extensions.AspNetCore.DataProtection.Blobs`, `Microsoft.Graph`, Bogus, Scrutor, Wolverine (`WolverineFx.*`), `Schick.Keycloak.RestApiClient`, `Microsoft.NET.Test.Sdk`, `Microsoft.Extensions.TimeProvider.Testing`, coverlet |
| **Apache-2.0** | Dapper & `Dapper.SqlBuilder`, OpenTelemetry (`OpenTelemetry.*`), xUnit |
| **LGPL-3.0** *(or commercial)* | Hangfire (`Hangfire.Core`, `Hangfire.AspNetCore`, `Hangfire.SqlServer`) — see note below |

### Note on Hangfire (LGPL-3.0)

Hangfire is dual-licensed under **LGPL-3.0** or a **commercial license**. Using it
as an unmodified library — which is exactly how AppointMe references it (a NuGet
package, dynamically linked) — is permitted in commercial and proprietary
applications at no cost. The copyleft obligation is triggered only if you
**modify Hangfire's own source**; in that case you must release those
modifications under the LGPL or obtain a commercial license from
[hangfire.io](https://www.hangfire.io/pricing/). Linking your own proprietary code
against it imposes no such obligation.

## Frontend (npm)

| License | Components |
|---|---|
| **MIT** | React & React-DOM, Vite, Tailwind CSS, Radix UI (`@radix-ui/*`), TanStack (Query / Table / Pacer / Hotkeys), FullCalendar (`@fullcalendar/react`, `@fullcalendar/core`, `@full-ui/headless-calendar`), axios, Zod, Recharts, react-hook-form, react-router-dom, sonner, clsx, class-variance-authority, tailwind-merge, nuqs, cmdk, vaul, embla-carousel, next-themes, orval, Vite+ (bundles Oxlint, Oxfmt, Vitest) |
| **Apache-2.0** | TypeScript |
| **ISC** | lucide-react |

## Bundled local-development services (containers)

Run by .NET Aspire for local development only; they are **not** redistributed as
part of AppointMe's source:

| Component | License |
|---|---|
| Keycloak | Apache-2.0 |
| Mailpit | MIT |
| Microsoft SQL Server | Microsoft proprietary EULA (Developer edition is free for development/test) |

## Generating a complete report

For an authoritative manifest of every dependency, including transitive ones,
generate it from the lockfiles:

```bash
# .NET — list every package license across the solution
dotnet tool install --global dotnet-project-licenses
dotnet-project-licenses --input AppointMe.sln --output-format markdown

# Frontend — list every npm package license
cd src/AppointMe.Frontend
npx license-checker-rseidelsohn --summary
```
