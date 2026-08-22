# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Development Commands

### Running the Full Stack (Recommended)
```bash
cd src/AppointMe.Aspire && dotnet run
```
This starts all services via .NET Aspire: SQL Server, Keycloak, Mailpit, API, and Frontend.

### Backend Only
```bash
dotnet build AppointMe.sln        # Build entire solution
dotnet run --project src/AppointMe.Api  # Run API
```

### Frontend Only
```bash
cd src/AppointMe.Frontend
npm install
npm run dev          # Development server (port 5173)
npm run build        # Production build
npm run lint         # ESLint
npm run generate:api # Regenerate API client from OpenAPI spec (see /regenerate-api skill)
```

### Regenerating the frontend API client after backend contract changes

After any change that alters the OpenAPI surface — new/renamed/removed endpoint, changed `*Request.cs`/`*Response.cs` shape, changed route or HTTP verb, changed auth/permission attributes on an endpoint — invoke the `/regenerate-api` skill **in the same task**. It waits for the API to be reachable on `https://localhost:7233`, runs `npm run generate:api`, and reports the diff on `src/AppointMe.Frontend/src/api/`. The backend must be restarted (Aspire dashboard → restart `appointme-api`, or relaunch `dotnet run` in `src/AppointMe.Aspire`) so orval reads the new contract — a reachable-but-stale backend silently produces a stale client.

Do **not** invoke `/regenerate-api` for backend-only changes that don't affect the contract (internal handlers, domain events, EF config, migrations, tests).

### Testing
```bash
dotnet test                                    # All tests
dotnet test src/CRM/AppointMe.Crm.Tests        # Single test project
dotnet test --filter "FullyQualifiedName~TestName"  # Single test
```

### Template package (`dotnet new appointme`)

This subsection documents how **this repository** — the upstream source of the
`dotnet new appointme` template — packages itself. It is not about a project
generated from the template: generation strips `.template.config/` and never
emits `templates/**`, so skip this subsection if you're reading this file inside
a generated project.

The repo root **is** the template source — `templates/AppointMe.Templates.csproj`
packs it directly into the nupkg, so there is no separate copied tree to keep in
sync. That project is deliberately excluded from `AppointMe.sln`/`AppointMe.slnx`;
`dotnet build`/`dotnet test` are unaffected by it.

Two independent exclusion layers decide what ships, and they are not
interchangeable:

- **Pack-time** — the `None` item's `Exclude` glob in
  `templates/AppointMe.Templates.csproj` decides what enters the nupkg.
- **Generation-time** — `sources[].modifiers[].exclude` in
  `.template.config/template.json` decides what lands in a generated project's
  output.

```bash
./templates/smoke-test.sh   # pack, install locally, generate, build + test the output
```

That script is the gate: it packs, installs the package locally, generates a
project, builds and tests the generated solution, lints and builds its frontend,
and structurally compares `git ls-files` against the packed nupkg in both
directions. CI runs it on every PR (`.github/workflows/template.yml`) and
publishes on a `v*` tag.

**Never replace a bare occurrence of the lowercase brand token.** It is chosen to
be a strict prefix of the domain word `appointment` — the aggregate, the
`/appointments` route, and the `appointments.statistics:view` permission all
share its first nine letters. `.template.config/template.json` matches it only
when immediately followed by a punctuation guard, such as `appointme-` or
`appointme.` (see the `tok*` symbols there) — never bare — precisely so
`appointment` itself is never touched. Adding a new bare use of the brand token,
or a new PascalCase `AppointMe-prefixed` identifier, needs a matching guarded
symbol added to `template.json`, followed by another run of
`templates/smoke-test.sh`. The same applies to the PascalCase family: a new
follower character falls out of every bucket.

`is_withheld()` in `templates/smoke-test.sh` hand-maintains a mirror of the
`Exclude` list in `templates/AppointMe.Templates.csproj` — changing one without
the other breaks the manifest-parity check. This has already caused a failure
once during development; it remains the single easiest way to break the harness.

## Architecture

This is a **modular monolithic** .NET 10 application following Domain-Driven Design principles.

### Module Structure
```
src/
├── AppointMe.Api/           # ASP.NET Core API entry point
├── AppointMe.Aspire/        # .NET Aspire orchestrator for local dev
├── AppointMe.Shared/        # Shared domain abstractions and value objects
├── Identity/AppointMe.Identity/           # User auth module (Keycloak)
├── Organizations/AppointMe.Organizations/ # Companies & employees
├── CRM/AppointMe.Crm/                     # Customer management
├── Booking/AppointMe.Booking.Contracts/   # Booking contracts (TBD)
└── AppointMe.Frontend/      # React/Vite/TypeScript frontend
```

### Key Patterns

**Bounded Contexts**: Each module has its own `DbContext` with separate database schema:
- `IdentityDbContext` (schema: "identity")
- `OrganizationsDbContext` (schema: "organizations")
- `CrmDbContext` (schema: "crm")

**Vertical Slice Architecture**: Inside each module, code is organized by feature/use case rather than by technical layer. Each slice owns everything required to serve that use case and lives in its own folder named after the action (verb-first, CRUD-style for projections).

Example layout:
```
Customers/
├── Customer.cs                       # aggregate root
├── CustomerId.cs
├── RegisterCustomer/                 # one slice per use case
│   ├── RegisterCustomer.cs           # domain factory (extension on aggregate)
│   ├── RegisterCustomerCommand.cs
│   ├── RegisterCustomerCommandHandler.cs
│   ├── RegisterCustomerEndpoint.cs
│   └── RegisterCustomerRequest.cs
├── UpdateCustomer/
├── DeleteCustomer/
├── GetCustomers/                     # queries are slices too
└── Database/                         # shared EF config / Dapper queries
```

Slice rules:
- Event handlers live in the slice whose domain operation they invoke (e.g. `CustomerRegisteredEventHandler` sits in `CreateAttendee/` because it calls `Attendee.Create`), **not** in a shared `Handlers/` folder.
- Domain operations on aggregates are expressed as extension methods co-located with the slice (`extension(Aggregate)` for factories, `extension(Aggregate instance)` for mutators) — see `RegisterCustomer.cs`, `UpdateCustomer.cs`, `DeleteCustomer.cs`.
- Use CRUD-style verbs (`Create`, `Update`, `Delete`) for projection/synchronization slices, and domain-specific verbs (`Register`, `Schedule`, `Cancel`) for slices that own business intent on the source aggregate.
- Shared infrastructure (EF type configurations, Dapper queries reused across slices, ID types, the aggregate itself) lives alongside the slices in the parent feature folder, not inside any one slice.

**Endpoint Convention**: Implement `IEndpoint` interface. Endpoints are auto-discovered via Scrutor.

**Module Registration**: Each module exposes extension methods:
```csharp
.AddIdentityModule()
.AddCompaniesModule()
.AddCrmModule()
```

**Domain Events**: Aggregate roots inherit from `AggregateRoot` and raise `IDomainEvent` events. Wolverine handles async messaging. The transport is chosen by `Wolverine:Transport` (`AppointMe.Api/Wolverine/WolverineHostBuilderExtensions.cs`):

- **`SqlDurable`** — the default, and what local dev and devtest run (`appsettings.json`, `appsettings.Devtest.json`). Broker-less: durable local queues on top of the SQL outbox, so no Service Bus emulator or other broker is needed and in-flight messages survive a restart. Do not swap this for non-durable local queues — that loses messages on every restart/deploy.
- **`AzureServiceBus`** — the recommended production transport. Requires an `AppointMeMessaging` connection string; Wolverine auto-provisions queues on startup. The SQL outbox stays active as the durability layer either way.

`infra/README.md` (Messaging section) documents what to provision when switching production to Service Bus.

**CQRS Pattern**:
- Writes use Entity Framework Core
- Reads use Dapper with `IDbConnectionFactory`

**Authentication**: Hybrid auth scheme (JWT Bearer for API calls, Cookies for browser flows) with Keycloak.

**Strongly-Typed IDs**: Each module defines its own ID types wrapping `Guid` (e.g. `CustomerId`, `EmployeeId`, `AttendeeId`, `ServiceProviderId`). Cross-module projections use module-local ID types, not source module IDs.

**Value Objects**: Value objects like `PersonName`, `DateOfBirth`, `Email`, `LongString` live in `AppointMe.Shared/Domain/Common`. They expose two kinds of constructors:

- The **primary constructor** (`new DateOfBirth(value)`, `new Email(value)`, `new PersonName(...)`) is unvalidated. Reserve it for trusted paths only — EF Core materialization via `HasConversion`, Dapper projections, and similar infrastructure code.
- **Validating factories** live in a companion `…Factory` static class as extension methods on the value object's type. Always construct value objects from untrusted input (HTTP commands, event payloads from other modules, etc.) via these factories — never via the primary constructor.
  - `Create(...)` — validates and throws `ValidationException` on invalid input.
  - `CreateOrNull(...)` — returns `null` when the input is null, otherwise delegates to `Create`.

Example (from `RegisterCustomerCommandHandler`):
```csharp
var customer = Customer.Register(
    companyId: companyId,
    name: PersonName.Create(command.FirstName, command.LastName),
    dateOfBirth: DateOfBirth.CreateOrNull(command.DateOfBirth, timeProvider),
    gender: Gender.ParseOrNull(command.Gender),
    email: Email.CreateOrNull(command.Email),
    registrationDate: timeProvider.GetUtcNow()
);
```

The same rule applies inside event handlers that build projections from cross-module events — the payload is untrusted, so reconstruct value objects via `Create` / `CreateOrNull`, not `new`.

**Permission Auto-Discovery**: Permissions are auto-discovered by assembly scanning. Define permissions as `public static readonly Permission` fields on static classes ending with `Permissions`. Register via `.AddPermissions(assembly)` which also auto-discovers `IDefaultGrantPolicy` implementations via Scrutor.

### Naming Conventions

**Async suffix**: In async-only APIs, asynchronous behavior is implicit; the `Async` suffix is used only to disambiguate from synchronous alternatives.

**Lambda parameters**: Use full, descriptive names — not one- or two-letter abbreviations. Prefer `appointment => appointment.Id` over `a => a.Id`, `customer => customer.Name` over `c => c.Name`. Applies especially to EF `IEntityTypeConfiguration`, LINQ chains, and converter lambdas. Exceptions: single-char names are fine when the lambda is purely structural and the identifier carries no domain meaning (e.g. `x => x` in a trivial selector), but default to the full name.

**Test methods**: Use `snake_case` with a `should_` or descriptive verb prefix that reads as a sentence describing the expected behavior. Test classes are non-sealed. Example:
```csharp
public class PermissionResolverTests
{
    [Fact]
    public void should_deny_permission_when_any_role_denies() { ... }

    [Fact]
    public void should_return_empty_when_no_roles() { ... }
}
```

### Tech Stack
- **Backend**: .NET 10, C# 14, EF Core 10, Wolverine 5.9, Dapper
- **Frontend**: React 19, TypeScript 5.8, Vite 7, Tailwind CSS 4, TanStack Query
- **Infrastructure**: SQL Server 2025, Keycloak, Mailpit (local dev); Azure Service Bus for production messaging

## Local Development Services

When running via Aspire:
- **SQL Server**: localhost:60740 (sa/Password1)
- **Keycloak**: http://localhost:8082 (admin/admin)
- **Mailpit SMTP**: localhost:1026, Web UI: http://localhost:8026
- **Frontend**: https://localhost:5173