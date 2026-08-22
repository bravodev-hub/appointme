# Changelog

All notable changes to AppointMe are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[1.1.0]: https://github.com/bravodev-hub/appointme/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/bravodev-hub/appointme/releases/tag/v1.0.0
