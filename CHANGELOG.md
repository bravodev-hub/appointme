# Changelog

All notable changes to AppointMe are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] — 2026-09-27

The **template** release. AppointMe is now installable as a `dotnet new` template from
nuget.org, so a new project starts as a renamed copy of the whole foundation instead of a
fork:

```bash
dotnet new install BravoDev.AppointMe.Templates::1.2.0
dotnet new appointme -n Contoso.Booking
```

### Added

- **`dotnet new appointme` template package** (`BravoDev.AppointMe.Templates`). The repo root
  is the template source, so the package always ships the current app. Generation renames the
  namespaces, projects, solution, database, Keycloak realm, container and Aspire resource
  names, the demo account and the frontend API client to your project name. The domain word
  `appointment` is left alone. Generated projects get their own README; this repo's changelog,
  release media and deployment config are not included.
- **Template smoke test** (`templates/smoke-test.sh`), run in CI on every PR. It packs the
  template, installs it, generates a project, then builds, tests, lints and builds the
  generated solution and frontend. It also checks the package contents against `git ls-files`,
  checks that the realm file name matches the realm it contains, and fails the pack on
  NU5123 (package paths too long once installed).
- **Dev-certificate check at AppHost startup.** The Aspire AppHost stops with the exact fix
  command when the ASP.NET Core HTTPS development certificate is missing or untrusted,
  instead of starting the stack and failing later with an opaque 500 on login. On Linux it
  only warns.
- **Wolverine tracing and metrics** are exported through OpenTelemetry.

### Changed

- **Releases are tag-based.** `main` is the development branch: pushes and PRs build and test
  but no longer deploy. Pushing a `v*` tag deploys the devtest demo and publishes the template
  package from the same commit. The GitHub `devtest` and `nuget` environments accept `v*` tags
  only. The demo footer now shows the release version.
- **nuget.org publishing uses Trusted Publishing (OIDC).** No long-lived API key is stored;
  the publish job exchanges its GitHub OIDC token for a one-hour, single-use key.
- **The API gets its Keycloak URL from Aspire.** The authority and admin URLs come from
  Keycloak's endpoint reference, so they follow whichever scheme Keycloak actually serves.
  The fixed URL in `appsettings.Development.json` is still used when running without Aspire.
- **The AppHost has only the `https` launch profile.** IDEs can no longer default to the
  plain-HTTP profile.
- **Dependency refresh** — .NET Aspire 13.5, Wolverine 6.40, EF Core and ASP.NET Core 10.0.12,
  OpenTelemetry 1.19, Microsoft.Data.SqlClient 7.1, Microsoft Graph 6.7, Asp.Versioning 10.2,
  Dapper 2.1.89, Hangfire 1.8.25 and the Keycloak admin client 26.7. ASPIRE010 (Aspire CLI
  bundle) is suppressed; the dashboard and orchestrator still come from NuGet packages.

### Fixed

- The `Microsoft.OpenApi` security pin (GHSA-v5pm-xwqc-g5wc) is removed.
  `Microsoft.AspNetCore.OpenApi` 10.0.12 already requires a patched 2.12+, and the pin was
  blocking the upgrade.
- Resolved the high-severity `System.Security.Cryptography.Xml` advisories reported on
  restore (NU1903).

## [1.1.0] — 2026-08-22

The **dashboard** release. AppointMe now ships a business-analytics surface on top of the
booking data it already owns — built the same way as the rest of the app, so it doubles as a
worked example of a read-heavy vertical slice: Dapper reads, calculators unit-tested in
isolation, its own permissions, and a generated typed client on the frontend.

### Added

- **Dashboard** — a new `/dashboard` route summarising a company's booking business over a
  selectable period.
  - **Four KPI cards** — appointments (with cancellations), revenue booked, chair
    utilization (booked vs. bookable hours), and returning-client rate. Each card shows a
    delta against the comparison period.
  - **Trend chart** — appointments, revenue, cancellations, or new customers, bucketed by
    day / week / month and overlaid with the previous period.
  - **Bookings by staff** — per-provider booking counts and utilization, so an overloaded or
    idle provider is obvious at a glance.
  - **Peak hours heatmap** — average bookings per hour-of-day by weekday over the last four
    weeks.
  - **Period picker** — today, yesterday, this/last week, this month, quarter, or year, with
    an optional "compare to previous period" mode. The selection lives in the URL, so a view
    is shareable and survives a reload.
- **Dashboard API** — two new Booking endpoints (`GET /api/v1/booking/dashboard/stats`,
  `GET /api/v1/booking/dashboard/peak-hours`) and one CRM endpoint
  (`GET /api/v1/crm/dashboard/new-customers`), all reading through Dapper with the tenant
  predicate applied, and all range/bucket maths covered by unit tests.
- **Statistics permissions** — `appointments.statistics:view` and `customers.statistics:view`,
  auto-discovered like every other permission and wired into the default grant policies. The
  dashboard degrades per permission: a user with only one of them sees only the widgets that
  permission covers.
- **Shared bucketing primitives** — `StatsBucket` and `StatsBucketing` in `AppointMe.Shared`,
  so day/week/month bucketing is defined once and reused by both modules.
- **Covering index for dashboard queries** — a composite
  `IX_Appointments_CompanyId_Start` (including `End`, `Status`, `ProviderId`, `AttendeeId`),
  added as raw SQL because the index spans an owner scalar and an owned-type property that
  EF's fluent `HasIndex` cannot express together.
- **Administration menu** — a super-admin-only sidebar section (cross-tenant, config-driven),
  currently surfacing the background-jobs dashboard.
- **Demo data top-up jobs** — recurring jobs that keep the demo tenant's appointments and
  customers rolling forward, so the dashboard always has a populated window to render.
- **Build version in the footer** — the frontend now shows the build it was produced from,
  with the commit SHA passed into the image at build time via `APP_VERSION`.

### Changed

- **CI/CD is GitHub-only.** The GitLab pipeline is gone; GitHub Actions covers build, test,
  frontend lint, CodeQL (now v4), and a gitleaks secret scan on every push and PR. The
  `infra/` README was rewritten around GitHub OIDC setup for people cloning the repo.
- **Devtest infrastructure is cheaper.** Azure Service Bus was dropped in favour of
  Wolverine's `SqlDurable` transport, the app-service plan defaults to F1, the SQL SKU matches
  the live Basic tier, and Log Analytics ingestion is capped at 0.5 GB/day. A Cloudflare Worker
  host-rewrite proxy provides a custom domain on the free tier, with the API honouring
  `X-Original-Host` as the forwarded public hostname.
- **Dependency refresh** — React Router 8, and updates to axios, TanStack Query, lucide-react,
  orval, and Vite.
- **Dashboard layout works on phone screens** — KPI cards reflow, the trend-chart series
  toggles collapse into a select, and the wide widgets stack.

### Fixed

- Resolved flagged vulnerable dependencies.
- Added the missing `DialogDescription` to the schedule-appointment dialog, clearing Radix's
  `aria-describedby` console warning.
- Corrected input field names on the sign-up form.
- Added the missing `employees:manage_owners` permission label.
- Cleared pre-existing ESLint errors now that CI runs frontend lint, and split context hooks
  and row-action cells into sibling files to satisfy the fast-refresh convention.

## [1.0.0] — 2026-06-10

Initial public release of the AppointMe modular-monolith foundation: Identity, Organizations,
CRM, and Booking bounded contexts; hybrid JWT/cookie OIDC auth; multi-tenancy; CQRS with EF
Core writes and Dapper reads; Wolverine domain events over a durable SQL transport; an
auto-discovered permission system; a typed React frontend generated from the OpenAPI spec; and
a one-command .NET Aspire local stack.

[1.2.0]: https://github.com/bravodev-hub/appointme/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/bravodev-hub/appointme/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/bravodev-hub/appointme/releases/tag/v1.0.0
