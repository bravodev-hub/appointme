# Changelog

All notable changes to AppointMe are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Dependabot.** Weekly NuGet, npm and GitHub Actions updates, with minor and patch bumps
  grouped into one PR per ecosystem and a one-week cooldown on new releases (security
  updates are neither grouped nor delayed). Aspire packages get a PR of their own, since
  the AppHost SDK version has to move with them by hand, and Vite+ is excluded because its
  three pinned entries in `package.json` have to change together.

### Changed

- **Dependencies refreshed and all open npm and NuGet security advisories cleared.** .NET
  10.0.13, Aspire 13.6.4, Wolverine 6.53, OpenTelemetry 1.20; on the frontend Vite+ 1.4,
  React 19.3 and React Router 8.5 among in-range bumps. `System.Security.Cryptography.Pkcs`
  is pinned to 10.0.13 because SqlClient and the Aspire SQL Server host still pull in a
  vulnerable version.
- **Tests run on xUnit.net v3** (`xunit.v3.mtp-off` 4.0, `xunit.runner.visualstudio` 4.0),
  replacing the deprecated v2 `xunit` package. Test projects are now executables
  (`OutputType` `Exe`) and still run through VSTest, so `dotnet test` and its output are
  unchanged. The `-mtp-off` package keeps VSTest working on the .NET 10 SDK, since v3
  defaults to Microsoft Testing Platform; switching to that is a separate step.
- **Recharts 3** (3.10) for the dashboard trend chart, with the vendored shadcn `chart`
  component updated to its Recharts 3 version. `react-is` is now an explicit dependency
  pinned to the React version, as Recharts requires; the version npm picked on its own was
  React 18's. Charts gain Recharts 3's keyboard-accessible layer by default.
- **The OpenAPI document is generated at build time.** Every `dotnet build` of the API writes
  `src/AppointMe.Api/openapi.json` (committed), and `npm run generate:api` builds the client
  from that file, so regenerating no longer needs a running backend or the TLS workaround.
  The build step runs the app's startup in the `Codegen` environment with no database and
  Wolverine as an in-memory mediator; the shipped Wolverine handler code is generated as
  before.
- **FullCalendar 7** for the appointments calendar. Styling moves from the `fc-*` CSS overrides,
  which v7 no longer supports, to FullCalendar's shadcn theme component
  (`components/ui/event-calendar-views.tsx`) plus a few overrides that keep the existing
  look. The now-indicator line also shows in dark mode now. Named time zones resolve
  through Temporal, so `@fullcalendar/luxon3` and `luxon` are gone, and the toolbar drives
  the calendar through `useCalendarController`.
- **TanStack Table 9** for the Customers and Team tables. The shared `DataTable` components
  are typed against one `dataTableFeatures` set (row selection, column visibility, manual
  pagination), registered explicitly so only those features are bundled.
- **`temporal-polyfill` 1.0**, which uses the browser's native Temporal where available and
  only polyfills elsewhere. Also `@tanstack/react-pacer` 0.24 and `@tanstack/react-hotkeys`
  0.14 (both now ESM-only), and `@types/node` now matches the Node 24 runtime.
- **Aspire hosts the frontend with `Aspire.Hosting.JavaScript`**, replacing the deprecated
  `Aspire.Hosting.NodeJs` and the CommunityToolkit Node.js extensions. It installs packages
  with `npm install --no-save`, so starting Aspire no longer rewrites `package-lock.json`.
- **Node 24 for CI and the Docker build** (and `.node-version`); the lockfile is now written
  by npm 11. Node 22.18+ still works locally.
- **Local SQL Server image is 2025-CU10** (was CU1), and Mailpit is pinned to v1.32.0
  instead of `latest`, in both the Aspire host and `compose.yaml`.
- **Frontend toolchain is now Vite+.** `vp` replaces the separate Vite, ESLint, Prettier
  and `tsc` setup: lint (Oxlint) and format (Oxfmt) config live in `vite.config.ts`, and
  `npm run check` format-checks, lints and type-checks in one pass — which CI now runs.
  Formatting is enforced for the first time, and every Oxlint correctness rule — including
  the React Compiler checks, `exhaustive-deps` and type-aware unhandled-promise detection —
  now fails CI (six existing fire-and-forget calls are marked `void`). Requires Node 22.18+
  (or 24.11+).
- **Generated API client is now `src/api/client.ts`** (and `client.schemas.ts`), imported
  as `@/api/client`, instead of the brand-named `appointme.ts`. A brand-named client made
  `dotnet new` renames change the length of every import line that references it, so
  generated projects could fail the now-enforced format check depending on their name.

### Fixed

- **`limit` and `page` are optional again on paginated endpoints** (`/customers`, `/team`).
  Their defaults were property initializers, which minimal-API `[AsParameters]` binding
  ignores, so omitting either was a bad request. They now default to 10 and 1, and the
  generated client types them as optional.
- **Request binding errors return 400, not 500.** A missing or unparsable parameter or a
  malformed JSON body raises `BadHttpRequestException`, which the catch-all handler turned
  into a 500 in Development. A dedicated handler now answers with the exception's own
  status code as ProblemDetails, which also gives Kestrel limits such as an oversized body
  their real status (413) in every environment.
- **First load no longer starts with a 403.** With no company remembered yet (a new browser)
  or a remembered company the user no longer belongs to, the app rendered with a fallback
  company while requests still carried a missing or stale `X-Company-Id`, so `/me/access`
  failed and was retried about a second later. The app now waits until the selected company
  is stored before rendering.

## [1.2.1] — 2026-09-27

A packaging-only follow-up to 1.2.0. The generated project is unchanged.

### Added

- **Package README on nuget.org.** The package page now explains what the template is,
  how to install and run it, the prerequisites, and what's inside, with links to the source,
  changelog and issues. It is a dedicated `templates/PACKAGE_README.md`, not the generated
  project's README, because nuget.org cannot resolve that file's repo-relative links.
- **Project URL** in the package metadata, pointing at the GitHub repository.

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

[1.2.1]: https://github.com/bravodev-hub/appointme/compare/v1.2.0...v1.2.1
[1.2.0]: https://github.com/bravodev-hub/appointme/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/bravodev-hub/appointme/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/bravodev-hub/appointme/releases/tag/v1.0.0
