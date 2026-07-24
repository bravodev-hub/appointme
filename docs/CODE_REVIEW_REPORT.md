# AppointMe — Code Quality & Robustness Review (QA / Security)

## Context

This is a **read-only assessment**, not a change request. You asked for a Principal-QA-level review of the AppointMe codebase in three phases (structural mapping → robustness/compliance sweep → remediation register), to be presented for evaluation **before** any code is changed. Nothing below has been applied; every item is a proposal with a concrete fix so you can triage first.

**Bottom line:** the codebase is well-architected and, on the whole, defensively sound. The tenant-isolation model, value-object validation discipline, parameterized data access, cookie hardening (`HttpOnly`/`Secure`/`SameSite`), and generic error handling are all correctly implemented. The review surfaced **31 verified issues** (from 39 raised; 8 were adversarially refuted). The material ones cluster into: **within-tenant privilege escalation**, an **unauthenticated admin dashboard** *(since fixed and removed from this report — see the remediation-status note in the register)*, a **reconciliation-job data-loss bug**, and a set of **defense-in-depth / configuration hardening** gaps. No anonymous data-breach or cross-tenant IDOR was found to be currently exploitable.

## Methodology & scope

- **Full inventory:** 793 tracked files — ~497 C#, ~176 TS/TSX, plus Bicep/Docker/config. All `src/` modules, `infra/`, `docker/`, and root config were in scope.
- **Two independent passes, cross-checked:** (1) a multi-agent review fanned out over 8 dimensions (auth/session, authz/IDOR, input validation, CSRF/verb-safety, transport headers, config/secrets, frontend, robustness); **every finding was then re-read by an adversarial verifier** that tried to refute it against the real code and corrected severities/anchors. (2) I independently read the highest-signal files (`Program.cs`, auth wiring, multi-tenancy middleware, permission resolver, representative query/command handlers, SQL builders, appsettings, infra Bicep) and confirmed the material findings by hand.
- **Confidence:** 28 findings CONFIRMED, 3 PLAUSIBLE (real code smell, impact partly mitigated), 8 REFUTED and dropped (see "Verified clean").

---

# PHASE 1 — Structural Map

**Stack.** .NET 10 / C# 14 modular monolith (single `AppointMe.Api` host) + React 19 / TypeScript / Vite SPA. EF Core 10 (writes) + Dapper (reads, CQRS). Wolverine 6 for in-process messaging/domain events. Hangfire (SQL-backed) for recurring jobs. OpenTelemetry. SQL Server 2022. Keycloak (default) or Entra External ID for identity. .NET Aspire orchestrates local dev.

**Bounded contexts** (each its own `DbContext` + SQL schema + vertical-slice feature folders, registered via `AddXModule`):

| Module | Project | DbContext / schema |
|---|---|---|
| Identity | `src/Identity/AppointMe.Identity` | `IdentityDbContext` (`identity`) |
| Organizations | `src/Organizations/AppointMe.Organizations` | `OrganizationsDbContext` (`organizations`) |
| CRM | `src/CRM/AppointMe.Crm` | `CrmDbContext` (`crm`) |
| Booking | `src/Booking/AppointMe.Booking` | `BookingDbContext` (`booking`) |

Auto-discovery via Scrutor: endpoints (`IEndpoint`), permissions (`*Permissions` static fields), migrations, recurring jobs. Wolverine discovers handlers per `[assembly: WolverineModule]`.

**Request pipeline** (`src/AppointMe.Api/Program.cs`): `UseForwardedHeaders` → (dev-only OpenAPI) → `UseHttpsRedirection` → `UseExceptionHandler` → `UseStaticFiles` → `UseAppointMeMultiTenancy` → `UseAuthentication` → `UseAuthorization` → Hangfire dashboard → `MapEndpoints` (`api/v1` group) → `MapFallbackToFile("index.html")`.

**Routing.** All endpoints implement `IEndpoint` and are mounted under `api/v1`. Business mutations correctly use POST/PUT/DELETE/PATCH; only `/login`, `/login/demo`, `/logout` are (anonymous) GETs. `AllowAnonymous`: `/login`, `/login/demo`, `/logout`, `/signup`, OpenAPI (dev), and the SPA fallback. All other endpoints inherit the **fallback authorization policy** (`RequireAuthenticatedUser` + `RegisteredUserRequirement`).

**Data access & tenancy.** Each `DbContext` applies global query filters — soft-delete and `CompanyId == currentCompany.CompanyId`. `currentCompany` is resolved per-request from the client `X-Company-Id` header (`CompanyResolutionMiddleware` + `HeaderCompanyDetection`). Reads go through Dapper repositories that also take an explicit `companyId`. SQL is built with `Dapper.SqlBuilder`/`ExtSqlBuilder`; search and pagination are **parameterized** (`@SearchToken{i}`, `@Limit`/`@Offset`) — no string concatenation of user input. Membership is enforced in `UserPrincipalFactory.Create`, which throws `AccessDenied` if the caller has no employee row in the header-selected company; this runs whenever a Wolverine handler injects `IPrincipal` (`PrincipalContextPolicy`).

**External integrations.** Keycloak / Entra External ID (OIDC code flow + JWT bearer, hybrid scheme); Keycloak Admin REST + Entra native-auth for the demo login; SMTP (Mailpit locally) for invitations; Azure Service Bus / SQL-durable Wolverine transport; Hangfire on SQL; DataProtection keys → Azure Blob (when configured) else local. Minimal file I/O (`export-dev-cert.sh`, static SPA assets).

---

# PHASE 2 & 3 — Remediation Register

Priority = **Immediate** (exploitable now / data loss) · **High** (serious, needs preconditions) · **Medium** · **Low** (hardening / defense-in-depth). Findings are de-duplicated (several were raised under multiple dimensions).

> **Remediation status (updated 2026-07-21).** The review is a point-in-time snapshot from 2026-07-08 (base commit `3f5228e`). Fully fixed findings have been **removed** from this register (and from Appendix B, whose numbering is preserved); partially fixed ones remain in place with a status note.
> - **H2 — Hangfire dashboard exposed at `/admin/jobs` with authorization disabled — FIXED and removed** (commits `a7d4043` → `e65bf41` → `868f278`, merged in PR #3 `0ac8d2a`, refined in `e35ba73`; was also Appendix B3). Verified implementation: the dashboard is mapped as a routed endpoint behind `.RequireAuthorization(HangfireDashboardPolicy.Name)`, moving enforcement into the ASP.NET Core authorization pipeline (the empty `DashboardOptions.Authorization = []` is now intentional). The `HangfireDashboard` policy requires an authenticated user plus `SuperAdminRequirement`; `SuperAdminAuthorizationHandler` resolves the caller via `IIdentityResolver` and succeeds only for a registered `UserIdentity` whose email is in the config-sourced `SuperAdminRegistry` (`Authentication:SuperAdmins`; production defaults to `[]` → deny-all, Development/Devtest allow only `demo@appointme.dev`). Covered by `SuperAdminAuthorizationHandlerTests` (9/9 passing). Residual (accepted): the dashboard registers in every non-codegen environment — safe under the deny-by-default allowlist; super-admin trust rests on the IdP-provided email at user registration.
> - **M6 — No optimistic-concurrency token on any aggregate (silent lost updates) — FIXED and removed** (commit `0bb7021` + follow-ups on 2026-07-17; was also Appendix B11). Every EF-mapped entity across all four DbContexts now carries a non-nullable rowversion concurrency token (`builder.Property<byte[]>("Version").IsRowVersion().IsRequired()`): Booking — `Appointment`, `Attendee`, `BookingCompany`, `ServiceProvider` (migrations `20260709104431_AddAppointmentRowVersion`, `20260717103055_AddBookingProjectionsRowVersion`); CRM — `Customer` (`20260717102104_AddCustomerRowVersion`); Organizations — `Employee`, `Company`, `EmployeeInvitation`, `RolePermissionOverride` (`20260717102457_AddEmployeeAndCompanyRowVersion`, `20260717103102_AddInvitationAndPermissionOverrideRowVersion`); Identity — `User` (`20260717103143_AddUserRowVersion`). All columns are `rowversion, nullable: false`. A global `ConcurrencyExceptionHandler` maps `DbUpdateConcurrencyException` (including Wolverine-wrapped inner exceptions) to `409 Conflict` with code `concurrency_conflict`; conflicts inside Wolverine event/reconciliation handlers surface as exceptions handled by Wolverine's retry policy instead of silent lost updates. Verified: 10/10 entities tokenized in the model snapshots, full test suite green (136 tests), no Dapper `SELECT *` reads affected.
> - **L1 — `RequireHttpsMetadata` insecure default — FIXED and removed** (commit `8f74451`, 2026-07-21; was also Appendix B23). Per B23's verified remediation: the code fallback in `AuthenticationExtensions.cs` is now `GetValue("Authentication:RequireHttpsMetadata", true)` (applied to both OIDC and JWT Bearer options); the hard-coded `false` was removed from base `appsettings.json`; and an explicit `"RequireHttpsMetadata": false` opt-out was added to `appsettings.Development.json` and `appsettings.Codegen.json` only (the two local/offline paths B23 identified — local Keycloak and the fake `http://codegen` authority). Devtest config and `infra/main.json` already set `true`, so deployed posture is unchanged — the code default now matches it. Any new hosted environment that forgets the setting now gets HTTPS-only metadata by default and fails closed. Covered by `AuthenticationExtensionsTests` (default-absent → `true` on both schemes; explicit `false` still honored), enabled via a new `InternalsVisibleTo("AppointMe.Api.Tests")`. Verified: full solution test suite green (151 tests). Note: the finding's cited line 32 had drifted to line 39 by fix time.
> - **L3 — Logout is an anonymous GET (logout CSRF) — FIXED and removed** (2026-07-21, working-copy change pending commit; was also Appendix B14/B19). `LogoutEndpoint` now maps `MapPost("/logout")` with `RequireAuthorization()`. POST alone would not have closed the hole — `SignOutAsync`'s cookie-deletion `Set-Cookie` is applied by the browser even when the request carried no cookie (per B14's verification) — so auth is required: a cross-site POST arrives cookieless under `SameSite=Lax` and is challenged before the handler runs. `RequireAuthorization()` deliberately applies the default policy (authenticated user) rather than the registered-user fallback policy, so a signed-in user who has not completed signup can still log out. Frontend: `nav-user.tsx` now uses a `lib/logout.ts` helper submitting a top-level form POST (replacing the `window.location.href` GET) so the OIDC end-session redirect chain still runs as a navigation; the orval client was regenerated (`logout` is now a POST mutation). Verified live against the running stack: cookieless `POST /api/v1/logout` → 302 OIDC login challenge with **no** `appointme.auth` deletion header (a forged request can no longer terminate a session); `GET /api/v1/logout` no longer performs any sign-out (it now yields the same harmless challenge-redirect class as the pre-existing `/login` GET); solution builds, full test suite green, frontend `tsc`/lint clean on changed files. Residual: cross-site protection rests on `SameSite=Lax` + required auth (legacy-browser caveat); the app-wide antiforgery posture remains tracked as open finding L4. A manual login → logout click-through in the browser is recommended, as curl cannot drive the full OIDC session.
> - **L5 — Pagination ceiling 1000 rows/request — FIXED and removed** (commit `d6abd85`, 2026-07-17; was also Appendix B17). `PaginationFilter` now clamps to `[1, 100]` (`MinLimit = 1`, `MaxLimit = 100`), capping both per-request row count and the cost of the `COUNT(*) OVER ()` window aggregate; the `MinLimit = 1` change also removes the degenerate `limit=0` page (rows-free `TotalCount` probe) flagged in B17's notes. Silent clamping was deliberately kept instead of a `[Range]` 400 — the option B17's verified remediation endorsed ("friendlier than rejecting; no controller/attribute change strictly required"); `PaginationRequest.Limit` still defaults to 10. Verified 2026-07-21 against the working tree: both paged read paths (`GetCustomers`, `GetTeam`) funnel through `PaginationFilter`, so no other call sites needed changes.
> - **L6 — Untrusted timezone id rehydrated via non-validating `FindSystemTimeZoneById` — FIXED and removed** (2026-07-17; was also Appendix B16/B30). `BookingCompanySynchronizer.Apply` now reconstructs the timezone from the cross-module `CompanySnapshot` via the validating factory `TimeZoneInfo.Create(snapshot.TimeZone)`, per the value-object convention — an unresolvable id raises a domain `ValidationException` with a clear message instead of an infrastructure `TimeZoneNotFoundException`. The two `FindSystemTimeZoneById` uses in EF `HasConversion` lambdas are intentionally unchanged (DB materialization is a trusted path). Additionally, a Wolverine failure policy (`options.OnException<ValidationException>().MoveToErrorQueue()` in `WolverineHostBuilderExtensions.cs`) dead-letters queued messages that fail validation instead of retrying them — validation failures are deterministic, so retries could never succeed; inline HTTP invocations are unaffected (still mapped to 400 by `ValidationExceptionHandler`). Verified: solution builds, full test suite green (136 tests).
> - **L7 — HSTS never configured — FIXED and removed** (commit `cee5004`, 2026-07-21; was also Appendix B21). Per the verified remediation: `builder.Services.AddHsts(...)` registered in `Program.cs` with `MaxAge = 365 days` and `IncludeSubDomains = true`, and `app.UseHsts()` added before `UseHttpsRedirection()`, gated to non-Development environments. `Preload` deliberately omitted until the team commits to submitting the domain (and all subdomains) to the browser preload list. Verified: solution builds, API test suite green; `Strict-Transport-Security` confirmed absent when running locally (Development-gated by design), so live confirmation of the header belongs in a deployed Devtest/production environment. Residual (tracked separately as B7/C1): the ForwardedHeaders allow-lists are cleared (`Program.cs:50-55`), so in production `X-Forwarded-Proto` must be trusted only from the real ingress for `Request.IsHttps` — and therefore HSTS emission — to be reliable.
> - **L8 — No security response headers / no CSP — FIXED and removed** (commit `4069fb7`, 2026-07-21; was also Appendix B22). New `SecurityHeadersMiddleware` (`src/AppointMe.Api/SecurityHeaders/`), wired via `UseAppointMeSecurityHeaders()` immediately after `UseForwardedHeaders()` so static assets, the SPA fallback, and API responses are all covered. Emits `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, and `Referrer-Policy: strict-origin-when-cross-origin` on every response; headers are applied in `Response.OnStarting` so they survive the `Response.Clear()` that `ExceptionHandlerMiddleware` performs before writing problem-details responses. CSP ships **report-only** (`Content-Security-Policy-Report-Only`: `default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'` — inline styles required by the `chart.tsx` `<style>` sink and Recharts; `img-src`/`font-src 'self' data:`; `connect-src 'self'; frame-ancestors 'none'; object-src 'none'; base-uri 'self'; form-action 'self'`); rename the header to the enforcing `Content-Security-Policy` once browser violation reports come back clean while exercising the SPA. Covered by `SecurityHeadersMiddlewareTests` (6/6 passing, including a survives-`Response.Clear()` regression test); verified live against the running Aspire stack — all four headers present on both SPA-fallback and API responses, no enforcing CSP header emitted. Residual (intentional): CSP is report-only until tuned; framing is already blocked by the enforcing `X-Frame-Options: DENY`, with `frame-ancestors` taking over when CSP enforcement flips.
> - All other findings remain open.

## HIGH

### H1 — Within-tenant privilege escalation: any role can be assigned, including `Owner` ⚠ needs design review
- **Files:** `src/Organizations/AppointMe.Organizations/Employees/UpdateEmployeeRoles/UpdateEmployeeRoles.cs:23` and `src/Organizations/AppointMe.Organizations/Invitations/InviteEmployee/InviteEmployee.cs:13`
- **Issue:** `Employee.UpdateRoles` and `EmployeeInvitation.Create` bind `Roles` straight from the request and assign them verbatim. The only guard is that *locked* roles can't be *removed*; nothing restricts which roles can be *added*. `RoleFactory.Create("Owner")` returns the real `Role.Owner` **SystemRole** singleton. So a user holding `EmployeePermissions.UpdateRoles` (default: Manager) can self-promote to `Owner`, and a user with `EmployeePermissions.Invite` (default: Manager) can invite an `Owner`. This bypasses the SystemRole protection that the permission-override path *does* enforce (`UpdatePermissions.ValidateGrants` throws `role_permissions_immutable` for any `SystemRole`).
- **Precondition (why High not Immediate):** attacker must already be a Manager in the tenant; not anonymous.
- **Remediation (mirror the existing `ValidateGrants` rule — reject non-configurable/system roles):**
  ```csharp
  // UpdateEmployeeRoles.UpdateRoles(...), after the empty check:
  var notAssignable = roles.Where(role => !Role.Configurable.Contains(role)).ToArray();
  if (notAssignable.Length > 0)
      throw new ValidationException(
          $"These roles cannot be assigned: {string.Join(", ", notAssignable.Select(r => r.Name))}",
          code: "role_not_assignable");
  ```
  ```csharp
  // InviteEmployee.Create(...), after distinctRoles empty check:
  var notAssignable = distinctRoles.Where(role => !Role.Configurable.Contains(role)).ToArray();
  if (notAssignable.Count > 0)
      throw new ValidationException(
          $"These roles cannot be assigned: {string.Join(", ", notAssignable.Select(r => r.Name))}",
          code: "role_not_assignable");
  ```
- **Design review:** decide the intended grant policy. The snippet above blocks `Owner` and any unknown/custom role (`Role.Configurable` = built-ins minus SystemRoles). If the rule should also be "an actor may only grant roles they themselves hold / roles at or below their level," that is a policy decision requiring the actor's principal to be threaded into the domain operation.

### H3 — `ReconcileServiceProviders` silently drops all remaining updates/deletes after the first failed record (data loss)
- **File:** `src/Booking/AppointMe.Booking/ServiceProviders/ReconcileServiceProviders/ReconcileServiceProvidersCommandHandler.cs:20-41`
- **Issue:** Unlike the Attendee/BookingCompany reconcilers (which re-query each entity inside `UpsertAsync` per iteration), this handler pre-loads the tracked `locals` list **once** and reuses those tracked instances across the loop. When any single record throws (e.g. `PersonName.Create` rejecting an empty name), the `catch` calls `dbContext.ChangeTracker.Clear()`, which **detaches every entity in `locals`**. All subsequent update/delete/restore operations then mutate detached instances and are never persisted — the projection silently diverges from source.
- **Remediation:** either re-query per iteration (match the Attendee/BookingCompany pattern), or don't clear the whole tracker on per-item failure. Simplest robust fix — reload `locals` after a failure, or scope work per item:
  ```csharp
  catch (Exception ex)
  {
      logger.LogError(ex, "Failed to reconcile service provider {Id}", /* id */);
      dbContext.ChangeTracker.Clear();
      locals = await LoadLocalsAsync(companyId, cancellationToken); // re-materialize tracked set
  }
  ```
  Preferred: refactor to the same per-entity re-query `UpsertAsync` shape the other two synchronizers use, so one bad record can't poison the batch.

## MEDIUM

### M1 — Demo login mints a full session via anonymous GET; enabled in the deployed Devtest config with a committed password
- **Files:** `src/AppointMe.Api/Authentication/DemoLogin/DemoLoginEndpoint.cs:23,51`; `src/AppointMe.Api/appsettings.Devtest.json:28-35`
- **Issue:** `/login/demo` is an `AllowAnonymous` **GET** that calls `SignInAsync` and establishes a full cookie session for the pre-provisioned demo user with **no caller-supplied credential** (server holds the password). It correctly returns `NotFound` when `Demo:Enabled` is false and only ever signs in the single demo user (not arbitrary users) — but `Demo:Enabled` is `true` in the deployed **Devtest** config, and the demo password (`AppointMe1`) is committed and is a real, working Entra credential. State-changing GET → login-CSRF/session-fixation (a victim can be silently logged into the shared demo identity).
- **Remediation:** gate the endpoint to non-deployed environments and/or change GET→POST; keep `Demo:Enabled=false` everywhere reachable from the internet; rotate the demo account password and stop committing it (see L2).
  ```csharp
  // DemoModeExtensions / endpoint mapping — only register when demo is truly intended and not internet-exposed
  if (env.IsDevelopment()) builder.MapPost("/login/demo", DemoLogin).AllowAnonymous();
  ```

### M2 — Auth cookie has no absolute lifetime and is never re-validated against the IdP
- **File:** `src/AppointMe.Api/Authentication/AuthenticationExtensions.cs:47-53`
- **Issue:** the cookie sets `Name`/`HttpOnly`/`SameSite`/`SecurePolicy` but no `ExpireTimeSpan`, no `SlidingExpiration` override, and no `OnValidatePrincipal`. It falls back to the framework default (**14-day sliding**). Since only the `id_token` is stored and never re-checked, the app session is decoupled from Keycloak/Entra: a user disabled or logged out at the IdP stays authenticated until the cookie lapses, and a stolen cookie is valid up to 14 days and self-renews.
- **Remediation:**
  ```csharp
  .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
  {
      options.Cookie.Name = "appointme.auth";
      options.Cookie.HttpOnly = true;
      options.Cookie.SameSite = SameSiteMode.Lax;
      options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
      options.ExpireTimeSpan = TimeSpan.FromHours(8);   // absolute-ish session bound
      options.SlidingExpiration = false;                 // or keep sliding with a hard cap
      // Optional hardening: options.Events.OnValidatePrincipal to re-check id_token exp / IdP session
  })
  ```

### M3 — `UseForwardedHeaders` trusts `X-Forwarded-*` from any client
- **File:** `src/AppointMe.Api/Program.cs:50-55,63`
- **Issue:** `XForwardedFor | XForwardedProto` are processed while `KnownIPNetworks.Clear()` + `KnownProxies.Clear()` empty the trusted-proxy allow-lists. ASP.NET Core disables its source-IP check when both lists are empty, so forwarded headers are honored from any peer. An attacker hitting the origin can spoof `X-Forwarded-Proto`/`For`, poisoning scheme detection and the client IP seen in logs/rate-limiting. *(Note: does not defeat the auth-cookie `Secure` flag — `CookieSecurePolicy.Always` is absolute — so severity is Medium, not High.)*
- **Remediation:** constrain to the real proxy. On Azure App Service, prefer the platform's `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` (which sets correct known networks) and remove the manual block, or set `KnownNetworks`/`KnownProxies` to the ingress CIDR:
  ```csharp
  builder.Services.Configure<ForwardedHeadersOptions>(options =>
  {
      options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
      options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("<ingress-subnet>"), <prefix>));
      // do NOT clear both lists in a way that disables the source check
  });
  ```

### M4 — SQL Server reachable from all Azure traffic (`0.0.0.0` firewall rule + public network access)
- **File:** `infra/modules/sql.bicep:34,53-60`
- **Issue:** `publicNetworkAccess: 'Enabled'` plus the `0.0.0.0`-`0.0.0.0` firewall rule ("Allow Azure services") makes the server network-reachable from any Azure-hosted resource, including other tenants. Data access still requires the admin credential (from Key Vault, not committed), so this is reachability, not a breach — hence Medium.
- **Remediation:** disable public access and use a private endpoint / VNet integration, or replace the `0.0.0.0` rule with the app's specific outbound IPs. Prefer Entra-only auth over SQL admin login where possible.

### M5 — Permission-override cache invalidation doesn't propagate across instances
- **File:** `src/Organizations/AppointMe.Organizations/Settings/Permissions/Infrastructure/RolePermissionOverridesCache.cs:30` (+ `SharedModule.cs:20`)
- **Issue:** `HybridCache` is registered bare (`AddHybridCache()`) with no L2/backplane, and the entry (`LocalCacheExpiration = 1h`) is authoritative for authorization. `InvalidateAsync` → `RemoveAsync` only evicts the local node. On scale-out, other instances serve **stale permissions for up to an hour** after an admin changes them (both over- and under-granting). Harmless at single-instance (current state) but the code explicitly anticipates replicas.
- **Remediation:** add a distributed L2 + backplane (e.g. Redis) so `RemoveAsync` propagates, or lower `LocalCacheExpiration` and document a single-instance constraint until then.
  ```csharp
  services.AddStackExchangeRedisCache(o => o.Configuration = redisConn);
  services.AddHybridCache(); // now backed by the distributed L2
  ```

## LOW (hardening / defense-in-depth)

### L2 — Secrets committed to source control
- **Files:** `src/AppointMe.Api/appsettings.Development.json:9,15,23,29`; `appsettings.Devtest.json:33`; also `compose.yaml:30,47-48`, `src/AppointMe.Aspire/Program.cs:5,18-19`, `appointme-realm.json`
- **Issue:** SQL `sa` password (`Password1`), Keycloak `FrontendClientSecret` and admin `ClientSecret`, and the demo password (`AppointMe1`) are committed. Most are local-dev-only (local Keycloak realm, local SQL), but the **Devtest demo password is a real Entra credential** and the repo path is `public/`. Committed OIDC client secrets are a standing leak.
- **Remediation:** move real secrets to user-secrets / Key Vault; keep only placeholders in committed files (there is already an `appsettings.Devtest.example.json`); rotate the Entra demo password and the Keycloak client secrets; confirm `.gitleaks.toml` isn't allow-listing these.

### L4 — No antiforgery/CSRF for cookie-authenticated mutations
- **File:** `src/AppointMe.Api/Authentication/AuthenticationExtensions.cs:51` (posture); no `AddAntiforgery` anywhere
- **Issue:** browser flows are cookie-authenticated; the only defense against cross-site state change is `SameSite=Lax` (no CSRF token, no CORS). This is an accepted posture for a same-origin SPA, so not actively exploitable, but it's single-layer.
- **Remediation:** either add ASP.NET antiforgery for cookie-authed endpoints, or require a custom header the server validates (e.g. enforce presence of `X-Company-Id`/a CSRF header on mutations), or explicitly document `SameSite=Lax` + same-origin as the accepted control.

### L9 — `AllowedHosts` wildcard
- **File:** `src/AppointMe.Api/appsettings.json:8`
- **Issue:** `"AllowedHosts": "*"` disables host filtering in all environments. Lower impact here because `X-Forwarded-Host` isn't processed and redirects use `Frontend:BaseUrl`, not the request host — but it's a missing control.
- **Remediation:** set the concrete host per deployed environment (e.g. `app.appointme.dev`).

### L10 — Key Vault: purge protection disabled + public network access
- **File:** `infra/modules/key-vault.bicep:27-28`
- **Issue:** `enablePurgeProtection: null` (off) and `publicNetworkAccess: 'Enabled'`. RBAC still gates access, so exposure is bounded, but a soft-deleted secret can be permanently purged within the retention window and the vault is internet-reachable.
- **Remediation:** `enablePurgeProtection: true` and restrict network access (private endpoint / firewall).

### L11 — `useCurrentUser` guard is dead code (fails open to anonymous)
- **File:** `src/AppointMe.Frontend/src/components/auth/current-user-context.tsx:5`
- **Issue:** the context is created with a non-null default (`{ isAuthenticated: false }`), so the `if (context === null) throw` guard can never fire. Using the hook outside its provider silently yields the anonymous default instead of throwing, masking a wiring bug (fails closed to a login redirect, so not a security issue — a robustness one).
- **Remediation:** `createContext<GetCurrentUserResponse | null>(null)` so the guard works, matching the sibling contexts.

### L12 — `SqlConnection` leaked if `OpenAsync` throws
- **File:** `src/AppointMe.Shared/Database/SqlConnectionFactory.cs:10-13`
- **Issue:** the connection is created and `OpenAsync` awaited before it's returned to the caller's `using`. If `OpenAsync` throws (cancellation mid-open, transient failure after a pooled connection is reserved), the instance is never disposed — under load this can exhaust the pool.
- **Remediation:**
  ```csharp
  var sqlConnection = new SqlConnection(connectionString);
  try { await sqlConnection.OpenAsync(cancellationToken); return sqlConnection; }
  catch { await sqlConnection.DisposeAsync(); throw; }
  ```

### L13 — Tenant isolation depends on every handler injecting `IPrincipal` (architectural fragility) ⚠ needs design review
- **File:** `src/AppointMe.Api/Wolverine/HandlerContext/CompanyContextBehavior.cs` + `CompanyResolutionMiddleware.cs`
- **Issue:** the active tenant is taken verbatim from `X-Company-Id` with **no membership check at the boundary**. The membership check only runs as a side effect of a handler injecting `IPrincipal` (`UserPrincipalFactory`). Every current HTTP-reachable tenant-data handler does so, so there's **no live exploit** — but the isolation guarantee is implicit: a future handler that reads tenant data without injecting `IPrincipal` (or any path bypassing the Wolverine bus) would silently leak cross-tenant.
- **Remediation:** enforce membership at the boundary independent of handler shape — e.g. a middleware/authorization requirement that resolves the principal (and thus validates membership) for every non-anonymous, company-scoped request, so isolation no longer depends on each handler remembering to inject `IPrincipal`.

---

## Items flagged for architectural realignment / manual design review
- **H1** — the role-grant authorization policy (who may grant which roles) is a product decision, not just a null-check.
- **L13** — make tenant-membership enforcement a boundary invariant rather than an emergent property of handler signatures.
- **M5** — latent until horizontal scale-out; decide whether to fix now or gate scale-out on it. *(M6, its former companion here, is done.)*

## Verified clean (checked and dismissed — recorded for completeness)
The following were investigated and found **not** to be defects (8 adversarially refuted + confirmations from my own pass): SQL injection in search/pagination (fully parameterized); value-object primary-constructor misuse in handlers (all `new` usages are inside the factories themselves); `DateTime.Now/UtcNow` misuse (TimeProvider used consistently); customer/appointment/team read + write handlers (all resolve `IPrincipal`, `Require` a permission, and scope by `companyId`); `/me` and `/invitations/pending` `IgnoreQueryFilters` usage (deliberate, self-scoped by `UserId`/`Email`); invitation acceptance IDOR (scoped to the caller's email; expiry + pending-status enforced); `GlobalExceptionHandler` (generic message, no stack-trace leakage); the `X-Company-Id` client-controlled header (server-enforced); `<Can>`/`usePermission` UI gating (server enforces independently); login `returnUrl` open-redirect (server restricts to relative paths); DataProtection ephemeral-key concern (cookie is `Secure`/`HttpOnly`); the chart `dangerouslySetInnerHTML` (developer-authored input only); `RolePermissionOverridesCache` thread-safety (uses stampede-safe `HybridCache`).

---

## Verification plan (after fixes are approved & applied)
1. **Build/tests:** `dotnet build AppointMe.sln` and `dotnet test` (unit suites under each `*.Tests`). Add regression tests: role-assignment rejects `Owner`/non-configurable roles (H1); reconciliation continues after a bad record (H3); `SqlConnectionFactory` disposes on cancelled open (L12).
2. **Run the stack:** `cd src/AppointMe.Aspire && dotnet run` (SQL, Keycloak, Mailpit, API, SPA).
3. **H1:** as a Manager, `PUT /api/v1/employees/{id}/roles` and `POST /api/v1/invitations` with `{"roles":["Owner"]}` → expect `400 role_not_assignable`.
4. **H3:** seed a source employee with an invalid name among several valid ones, run the service-provider reconciliation, confirm the valid ones still project.
5. **M1:** confirm `/login/demo` is unavailable in deployed configs and `Demo:Enabled=false`; changed to POST.
6. **Headers (L7/L8):** `curl -I https://localhost:7233/` → confirm `Strict-Transport-Security`, `X-Content-Type-Options`, `X-Frame-Options`, `Content-Security-Policy` present and the SPA still loads/charts render. *(L7 and L8 done 2026-07-21 — nosniff/DENY/Referrer-Policy verified live; CSP is emitted as `Content-Security-Policy-Report-Only` until tuned. `Strict-Transport-Security` is Development-gated, so verify it in a deployed environment.)*
7. **Contract:** none of these change the OpenAPI surface, so `/regenerate-api` is not required (H1 adds a validation error code only).

## Suggested sequencing
1. H1, H3 (correctness/security, low blast radius). *(H2 done.)*
2. M1, M2, L2 (auth/session/secrets hygiene). *(L1 done.)*
3. L9, L4 (transport/CSRF headers — one small middleware + config). *(L3, L7, L8 done.)*
4. L12, L11 (robustness one-liners). *(L5, L6 done.)*
5. M3, M4, M5, L10, L13 (infra + architectural — schedule with a design discussion). *(M6 done.)*


---

# Appendix A — Full Structural Map (verbatim from mapping agents)


<!-- section: Application Architecture & Stack -->

## Application Architecture & Stack

A **.NET 10 / C# 14 modular-monolith SaaS** (single deployable `AppointMe.Api` host) with a **React 19 SPA** frontend. One ASP.NET Core process hosts all bounded contexts; each context is a separate C# project/assembly with its own `DbContext`, SQL schema, and vertical-slice feature folders. Local orchestration is done with .NET Aspire.

Global build settings live in `/Users/maksak/projects/bravodev/public/appointme/Directory.Build.props`: `net10.0`, `LangVersion 14`, `ImplicitUsings` + `Nullable` enabled, `Company/Authors = BravoDev`. SDK pinned in `/Users/maksak/projects/bravodev/public/appointme/global.json` to `10.0.0` (`rollForward: latestMajor`, no prerelease).

---

### 1. Module / Bounded-Context Layout and Registration

Modules under `src/` (each an independent assembly with a `Configuration/<Name>Module.cs` extension and a `ModuleAssembly.cs` marker):

| Module | Project | DbContext / schema | `AddXModule` extension |
|---|---|---|---|
| Shared | `src/AppointMe.Shared` | (none — cross-cutting) | `AddSharedModule(IConfiguration)` |
| Identity | `src/Identity/AppointMe.Identity` | `IdentityDbContext` (`identity`) | `AddIdentityModule(IConfiguration)` |
| Organizations | `src/Organizations/AppointMe.Organizations` | `OrganizationsDbContext` (`organizations`) | `AddOrganizationsModule()` |
| CRM | `src/CRM/AppointMe.Crm` | `CrmDbContext` (`crm`) | `AddCrmModule()` |
| Booking | `src/Booking/AppointMe.Booking` | `BookingDbContext` (`booking`-schema) | `AddBookingModule()` |

Composition happens in `src/AppointMe.Api/Program.cs` (lines 41-46), in order:
```
.AddSharedModule(builder.Configuration)
.AddIdentityModule(builder.Configuration)
.AddOrganizationsModule()
.AddCrmModule()
.AddBookingModule();
```

**Common registration recipe** (each `*Module.cs`): register the module `DbContext` on SQL Server with a per-schema `__EFMigrationsHistory` table; `AddDatabaseMigration<TContext>()`; `AddEndpoints(ModuleAssembly.Instance)`; a Dapper `IDbConnectionFactory` (`SqlConnectionFactory` over the shared `AppointMeSql` connection string); `AddPermissions(ModuleAssembly.Instance)`; plus module-specific repositories/synchronizers/rehydration sources.
- `SharedModule` (`src/AppointMe.Shared/Configuration/SharedModule.cs`): registers Dapper type handlers, `FrontendOptions` (bound to `Frontend` config), `AddHybridCache()`, `TimeProvider.System`, and the `ConnectionStrings` singleton.
- `IdentityModule`: also registers `IUserIdentityRegistry` and switches identity provider on `Authentication:Provider` config (`Keycloak` default, or `EntraExternalId`).
- `OrganizationsModule`: owns the permission engine — `PermissionResolver`, `IOverrideConflictPolicy → DenyWinsPolicy`, `RolePermissionOverridesCache`, `UserPrincipalFactory`, plus company/employee rehydration sources.
- `CrmModule` / `BookingModule`: repositories, rehydration sources, demo seeders. Booking additionally calls `AddRecurringJobs(BookingModuleAssembly.Instance)` and registers reconciliation synchronizers/jobs (BookingCompany, ServiceProvider, Attendee).

**Auto-discovery mechanisms** (all via Scrutor assembly scanning, defined in `src/AppointMe.Shared`):
- **Endpoints** (`Endpoints/EndpointsServiceCollectionExtensions.cs`): `AddEndpoints(assembly)` scans for `IEndpoint` implementations (transient). `MapEndpoints()` (`Endpoints/EndpointsApplicationBuilderExtensions.cs`) builds an `api/v1` route group with an `ApiVersionSet` and calls `endpoint.MapEndpoint(group)` on each.
- **Permissions** (`Authorization/Permissions/PermissionsServiceCollectionExtensions.cs`): scans static `*Permissions` classes for `public static readonly Permission` fields (registered as singletons) and auto-registers `IDefaultGrantPolicy` implementations.
- **Migrations** (`Database/Migrations/DatabaseMigrations.cs`): `AddDatabaseMigration<TContext>()` registers a single `DatabaseMigrationService` `IHostedService` that runs `Database.MigrateAsync()` for every registered context on startup.
- **Recurring jobs** (`Jobs/JobsServiceCollectionExtensions.cs`): `AddRecurringJobs(assembly)` scans `IRecurringJobRegistrar` implementations.
- **Wolverine modules**: each module assembly is marked `[assembly: WolverineModule]` so Wolverine discovers its handlers.

---

### 2. Tech Stack (versions from `Directory.Packages.props`)

**Backend / infrastructure NuGet:**
- **.NET 10** runtime; **EF Core 10.0.9** (`Microsoft.EntityFrameworkCore(.SqlServer/.Relational/.Design)`), `Microsoft.Data.SqlClient` 7.0.2.
- **Wolverine 6.16.0** (`WolverineFx`, `.SqlServer`, `.EntityFrameworkCore`, `.AzureServiceBus`, `.RuntimeCompilation`) — async messaging / domain-event dispatch.
- **Dapper 2.1.79** + `Dapper.SqlBuilder` 2.1.66 — CQRS read side.
- **Hangfire 1.8.23** (`AspNetCore`, `Core`, `SqlServer`) — background/recurring jobs.
- **OpenTelemetry 1.16.0** (`Extensions.Hosting`, OTLP exporter, AspNetCore/Http/SqlClient instrumentation).
- **Auth**: `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.9, `.OpenIdConnect` 10.0.9, `Schick.Keycloak.RestApiClient` 26.5.7, `Azure.Identity` 1.21.0, `Microsoft.Graph` 6.2.0 (Entra path).
- **Data protection**: `Azure.Extensions.AspNetCore.DataProtection.Blobs` 1.5.3.
- **API**: `Asp.Versioning.Http` 10.0.0, `Microsoft.AspNetCore.OpenApi` 10.0.9, `Microsoft.Extensions.Caching.Hybrid` 10.7.0.
- **DI scanning**: `Scrutor` 7.0.0. **Test data**: `Bogus` 35.6.5. **Testing**: `xunit` 2.9.3, `Microsoft.NET.Test.Sdk` 18.7.0, `EntityFrameworkCore.InMemory` 10.0.9, `TimeProvider.Testing` 10.7.0, `coverlet.collector` 6.0.4.
- **Aspire hosting** 13.4.6 (`AppHost`, `Azure.ServiceBus`, `SqlServer`), `Keycloak` 13.4.6-preview, `NodeJs` 9.5.2 + `CommunityToolkit.Aspire.Hosting.NodeJS.Extensions` 9.9.0.

Note: `README.md` claims "Wolverine 6, TypeScript 6, Vite 8" while root `CLAUDE.md` says "Wolverine 5.9, TypeScript 5.8, Vite 7" — the **pinned versions above (Wolverine 6.16.0)** and `package.json` are authoritative; CLAUDE.md is stale on these numbers.

**Frontend** (`src/AppointMe.Frontend/package.json`, Node >=22): React **19.2**, TypeScript **6.0**, Vite **8.0** (`@vitejs/plugin-react`, `vite-plugin-mkcert` auto-HTTPS, `vite-plugin-svgr`), Tailwind CSS **4.3** (`@tailwindcss/vite`), shadcn/Radix UI primitives, **TanStack Query 5** (+ table, pacer, hotkeys), **orval 8.15** (OpenAPI → typed axios client), **axios 1.17**, React Router **7.16**, react-hook-form **7.77** + **zod 4.4**, FullCalendar **6.1**, luxon/temporal-polyfill for dates, sonner, lucide-react, recharts. Scripts: `dev`, `build` (`tsc -b && vite build`), `lint`, `generate:api` (orval).

---

### 3. Cross-Cutting Infrastructure (wired in `src/AppointMe.Api/Program.cs`)

Service registration block (lines 26-59):

- **Error handling** (`ErrorHandling/ErrorHandlingServiceCollectionExtensions.cs`): `AddProblemDetails()` plus an ordered chain of `IExceptionHandler`s — `ValidationExceptionHandler`, `NotFoundExceptionHandler`, `ConflictExceptionHandler`, `AccessDeniedExceptionHandler`, then a catch-all `GlobalExceptionHandler`.
- **API versioning** (`ApiVersioning/ApiVersioningExtensions.cs`): default `v1`, URL-segment reader (`api/v1/...`), report versions, assume default when unspecified.
- **Multi-tenancy** (`MultiTenancy/MultiTenancyExtensions.cs`): `AddAppointMeMultiTenancy(t => t.FromHeader("X-Company-Id"))` registers `ICurrentCompany → CurrentCompany` (singleton, AsyncLocal-style ambient) and a `CompanyDetectionBuilder` pipeline of `ICompanyDetection` strategies. Runtime `CompanyResolutionMiddleware` detects the company (header/claim strategies) and scopes it via `currentCompany.Change(companyId)` for the request. Command path uses EF query filters; Dapper reads carry the tenant predicate by convention.
- **Authentication** (`Authentication/AuthenticationExtensions.cs`): **hybrid scheme**. `DefaultScheme = Hybrid` policy scheme whose `ForwardDefaultSelector` routes to **JWT Bearer** when an `Authorization: Bearer` header is present, otherwise **Cookie** (`appointme.auth`, HttpOnly, SameSite=Lax, Secure=Always). Default challenge = **OpenID Connect** (auth-code flow, `SignInScheme=Cookie`, scopes openid/profile/email, callback `/signin-oidc`, only the `id_token` is persisted to keep cookie under Keycloak's 8KB header limit; bearer requests get 401 instead of a redirect). OIDC/JWT options bound from `IdentityProviderOptions` (authority/client/audience). Provider selectable via `Authentication:Provider` (Keycloak default / EntraExternalId). Registers `ICurrentIdentity`, `HttpIdentityFactory`, `IIdentityResolver`.
- **Authorization** (`Authorization/AuthorizationServiceCollectionExtensions.cs`): `ICurrentPrincipal`/`ICurrentPrincipalResolver`, `PermissionRegistry`, `RegisteredUserAuthorizationHandler`, and a **fallback policy** requiring an authenticated user **and** a `RegisteredUserRequirement` (so every endpoint is protected unless it opts out with `AllowAnonymous`).
- **JSON** (`Json/JsonOptionsExtensions.cs`): strict number handling, `JsonStringEnumConverter`, custom `RoleJsonConverter`.
- **OpenAPI** (`OpenApi/OpenApiExtensions.cs`): `AddOpenApi` with `Permission`/`Role` schema + document transformers.
- **OpenTelemetry** (`OpenTelemetry/OpenTelemetryExtensions.cs`): service `AppointMe.Api` v1.0.0; tracing (AspNetCore + HttpClient + SqlClient with exceptions) and metrics (AspNetCore + HttpClient), both exporting via **OTLP**.
- **Hangfire** (`Hangfire/HangfireExtensions.cs`, skipped during codegen): SQL Server storage in `hangfire` schema, `AddHangfireServer()`, a `SystemContextJobFilter`, plus `IRecurringJobScheduler → HangfireRecurringJobScheduler` and a `RecurringJobsHostedService` that registers discovered recurring jobs.
- **Demo mode** (`Authentication/DemoLogin/DemoModeExtensions.cs`): `DemoOptions` (bound to `Demo`), demo-login endpoint, provider-specific `IDemoUserAuthenticator` HttpClient.
- **Forwarded headers**: `ForwardedHeaders.XForwardedFor | XForwardedProto` (cleared known-proxy lists — for reverse-proxy/Azure ingress).
- **Data protection** (`DataProtection/DataProtectionExtensions.cs`): stable app name `appointme`; persists key ring to **Azure Blob Storage** when a `DataProtectionStorage` connection string exists, else local key ring.
- **Wolverine** (`Wolverine/WolverineHostBuilderExtensions.cs`, `builder.Host.AddWolverine(...)`): SQL Server message persistence (schema `wolverine`), durable local queues, EF Core transactions auto-applied, **domain events auto-published from `AggregateRoot.Events`**, dynamic codegen in Development / static in Production. Registers ambient-context **policies** (`TenantContextPolicy`, `DomainEventContextPolicy`, `IdentityContextPolicy`, `PrincipalContextPolicy`, `CompanyContextPolicy`) that propagate tenant/identity/principal into handlers. Transport switch on `Wolverine:Transport`: `SqlDurable` (default, no external broker) or `AzureServiceBus` (auto-provision). Handler-context behaviors live in `Wolverine/HandlerContext/`.

`isCodegen` (via `Wolverine/CodeGenerationDetection.cs`) disables Hangfire, the Hangfire dashboard, and Wolverine's durability agent when the process is running codegen commands.

---

### 4. Request Pipeline / Middleware Order (`Program.cs` lines 61-85)

`var app = builder.Build();` then:

1. `app.UseForwardedHeaders()` — apply X-Forwarded-For/Proto first.
2. **Development only**: `app.MapOpenApi().AllowAnonymous()` — serves `/openapi/v1.json`.
3. `app.UseHttpsRedirection()`
4. `app.UseExceptionHandler()` — routes to the `IExceptionHandler` chain / ProblemDetails.
5. `app.UseStaticFiles()` — serves the built SPA assets.
6. `app.UseAppointMeMultiTenancy()` — `CompanyResolutionMiddleware` (resolves + scopes `X-Company-Id`).
7. `app.UseAuthentication()`
8. `app.UseAuthorization()`
9. **Non-codegen only**: `app.UseAppointMeHangfireDashboard()` — dashboard at `/admin/jobs` (no auth filter configured).
10. `app.MapEndpoints()` — maps all `IEndpoint`s under the versioned `api/v1` group.
11. `app.MapFallbackToFile("index.html").AllowAnonymous()` — SPA fallback for client-side routing.
12. `return await app.RunJasperFxCommands(args)` — JasperFx/Wolverine entry point (supports codegen and other CLI commands instead of `app.Run()`).

Ordering note: multi-tenancy runs **before** authentication (company can be resolved from the `X-Company-Id` header pre-auth), and the Hangfire dashboard is mapped after auth middleware but exposed without its own authorization filter.



<!-- section: Routing Endpoints Map -->

## Endpoint auto-discovery & global routing conventions

**Discovery (Scrutor):** `EndpointsServiceCollectionExtensions.AddEndpoints(Assembly)` (`/Users/maksak/projects/bravodev/public/appointme/src/AppointMe.Shared/Endpoints/EndpointsServiceCollectionExtensions.cs`) uses `services.Scan(...).AddClasses(c => c.AssignableTo<IEndpoint>()).AsImplementedInterfaces().WithTransientLifetime()` to register every `IEndpoint` (`/Users/maksak/projects/bravodev/public/appointme/src/AppointMe.Shared/Endpoints/IEndpoint.cs`) in a module's assembly. Called once per module: `OrganizationsModule.cs:38`, `IdentityModule.cs:34`, `BookingModule.cs:37`, `CrmModule.cs:33`, and `DemoModeExtensions.cs:16` (API host assembly).

**Mapping (global prefix + versioning):** `EndpointsApplicationBuilderExtensions.MapEndpoints()` (`/Users/maksak/projects/bravodev/public/appointme/src/AppointMe.Shared/Endpoints/EndpointsApplicationBuilderExtensions.cs`) builds an API version set (v1, `ReportApiVersions`), creates one group `app.MapGroup("api/v1").WithApiVersionSet(...)`, resolves `IEnumerable<IEndpoint>` from DI, and calls `endpoint.MapEndpoint(versionedGroup)` on each. So **every route below is served under the prefix `api/v1/`** (e.g. `/customers` -> `api/v1/customers`). There are no other per-module `MapGroup` sub-prefixes; each endpoint maps its own relative template on the single `api/v1` group.

**Global authorization (fallback policy):** `AuthorizationServiceCollectionExtensions.AddAppointMeAuthorization()` (`/Users/maksak/projects/bravodev/public/appointme/src/AppointMe.Api/Authorization/AuthorizationServiceCollectionExtensions.cs`) sets a **fallback policy** = `RequireAuthenticatedUser()` + `RegisteredUserRequirement`. The requirement is satisfied only when `IIdentityResolver` resolves a `UserIdentity` (`RegisteredUserAuthorizationHandler.cs`). Because it is a *fallback* policy, any endpoint that does **not** declare its own auth metadata is implicitly protected (authenticated + registered user). `AllowAnonymous` opts out; `RequireAuthorization()` explicitly re-asserts the default.

**Permission enforcement is NOT at the endpoint level.** Endpoints only dispatch a command/query via Wolverine `IMessageBus.InvokeAsync`. Fine-grained permissions are enforced inside the handlers with `principal.Require(<Permission>)` (verified via grep across all `*Handler.cs`). Permissions are auto-discovered `public static readonly Permission` fields on `*Permissions` classes. The "Permission required" column reflects the handler-level check, not endpoint metadata.

**Program.cs pipeline** (`/Users/maksak/projects/bravodev/public/appointme/src/AppointMe.Api/Program.cs`): `UseAuthentication` -> `UseAuthorization` -> `app.MapEndpoints()` -> `app.MapFallbackToFile("index.html").AllowAnonymous()`. In Development only, `app.MapOpenApi().AllowAnonymous()`. Hangfire dashboard mapped via `app.UseAppointMeHangfireDashboard()` (non-`IEndpoint`). Multi-tenancy resolves company from `X-Company-Id` header.

## Complete endpoint table

All routes are under `api/v1/`. "Auth" column: `AllowAnonymous` = explicitly anonymous; `RequireAuthorization` = explicit; `Fallback` = no endpoint-level metadata, so the global fallback (authenticated + registered user) applies. "Permission" = `principal.Require(...)` inside the dispatched handler (`—` = none).

| Verb | Route (after `api/v1`) | Handler / Endpoint file | Auth (endpoint) | Permission (in handler) | Dispatches |
|------|------------------------|-------------------------|-----------------|-------------------------|------------|
| GET | `/login` | Identity/.../Login/LoginEndpoint.cs | AllowAnonymous | — | none — `TypedResults.Challenge` to OpenIdConnect |
| GET | `/logout` | Identity/.../Logout/LogoutEndpoint.cs | AllowAnonymous | — | none — `SignOutAsync` (Cookie + OIDC) |
| POST | `/signup` | Identity/.../Signup/SignupEndpoint.cs | AllowAnonymous | — | `SignupCommand` |
| GET | `/me` | Organizations/.../Me/GetCurrentUserEndpoint.cs | AllowAnonymous | — | `GetCurrentUserQuery` |
| GET | `/me/access` | Organizations/.../UserAccess/GetCurrentUserAccessEndpoint.cs | Fallback | — | `GetCurrentUserAccessQuery` |
| POST | `/onboarding` | Organizations/.../Companies/Onboarding/OnboardingEndpoint.cs | Fallback | — | `OnboardingCommand` -> `Created<OnboardingResponse>` |
| GET | `/team` | Organizations/.../Employees/GetTeam/GetTeamEndpoint.cs | Fallback | `employees:view` | `GetTeamQuery` |
| PUT | `/employees/{id:guid}/roles` | Organizations/.../Employees/UpdateEmployeeRoles/UpdateEmployeeRolesEndpoint.cs | Fallback | `employees:update_roles` | `UpdateEmployeeRolesCommand` |
| DELETE | `/employees/{id:guid}` | Organizations/.../Employees/DeleteEmployee/DeleteEmployeeEndpoint.cs | Fallback | `employees:remove` | `DeleteEmployeeCommand` |
| POST | `/invitations` | Organizations/.../Invitations/InviteEmployee/InviteEmployeeEndpoint.cs | Fallback | `employees:invite` | `InviteEmployeeCommand` -> `Created<InviteEmployeeResponse>` |
| GET | `/invitations/pending` | Organizations/.../Invitations/GetPendingInvitations/GetPendingInvitationsEndpoint.cs | **RequireAuthorization** (explicit) | — | `GetPendingInvitationsQuery` |
| POST | `/invitations/{id:guid}/accept` | Organizations/.../Invitations/AcceptInvitation/AcceptInvitationEndpoint.cs | **RequireAuthorization** (explicit) | — | `AcceptInvitationCommand` |
| DELETE | `/invitations/{id:guid}` | Organizations/.../Invitations/CancelInvitation/CancelInvitationEndpoint.cs | Fallback | `invitations:cancel` | `CancelInvitationCommand` -> `204 NoContent` |
| POST | `/invitations/{id:guid}/resend` | Organizations/.../Invitations/ResendInvitation/ResendInvitationEndpoint.cs | Fallback | `invitations:resend` | `ResendInvitationCommand` |
| GET | `/settings/permissions` | Organizations/.../Settings/Permissions/GetPermissions/GetPermissionsEndpoint.cs | Fallback | `permissions:view` | `GetPermissionsQuery` |
| PATCH | `/settings/permissions` | Organizations/.../Settings/Permissions/UpdatePermissions/UpdatePermissionsEndpoint.cs | Fallback | `permissions:manage` (SystemPermission) | `UpdatePermissionsCommand` |
| DELETE | `/settings/permissions/overrides` | Organizations/.../Settings/Permissions/ResetPermissions/ResetPermissionsEndpoint.cs | Fallback | `permissions:manage` (SystemPermission) | `ResetPermissionsCommand` |
| POST | `/customers` | CRM/.../Customers/RegisterCustomer/RegisterCustomerEndpoint.cs | Fallback | `customers:create` | `RegisterCustomerCommand` (`request.ToCommand()`) -> `Created<RegisterCustomerResponse>` |
| GET | `/customers` | CRM/.../Customers/GetCustomers/GetCustomersEndpoint.cs | Fallback | `customers:view` | `GetCustomerQuery` |
| GET | `/customers/{id:guid}` | CRM/.../Customers/GetCustomerById/GetCustomerByIdEndpoint.cs | Fallback | `customers:view` | `GetCustomerByIdQuery` |
| PUT | `/customers/{id:guid}` | CRM/.../Customers/UpdateCustomer/UpdateCustomerEndpoint.cs | Fallback | `customers:update` | `UpdateCustomerCommand` (`request.ToCommand(id)`) -> `204 NoContent` |
| DELETE | `/customers/{id:guid}` | CRM/.../Customers/DeleteCustomer/DeleteCustomerEndpoint.cs | Fallback | `customers:delete` | `DeleteCustomerCommand` |
| POST | `/appointments` | Booking/.../Appointments/ScheduleAppointment/ScheduleAppointmentEndpoint.cs | Fallback | `appointments:schedule` | `ScheduleAppointmentCommand` (`request.ToCommand()`) -> `Created<ScheduleAppointmentResponse>` |
| GET | `/appointments` | Booking/.../Appointments/GetAppointments/GetAppointmentsEndpoint.cs | Fallback | `appointments:view` | `GetAppointmentsQuery` (`request.ToQuery()`) |
| GET | `/appointments/{id:guid}` | Booking/.../Appointments/GetAppointmentById/GetAppointmentByIdEndpoint.cs | Fallback | `appointments:view` | `GetAppointmentByIdQuery` |
| PUT | `/appointments/{id:guid}/reschedule` | Booking/.../Appointments/RescheduleAppointment/RescheduleAppointmentEndpoint.cs | Fallback | `appointments:reschedule` | `RescheduleAppointmentCommand` (`request.ToCommand(id)`) -> `204 NoContent` |
| POST | `/appointments/{id:guid}/cancel` | Booking/.../Appointments/CancelAppointment/CancelAppointmentEndpoint.cs | Fallback | `appointments:cancel` | `CancelAppointmentCommand` -> `204 NoContent` |
| GET | `/booking/service-providers` | Booking/.../ServiceProviders/GetServiceProviders/GetServiceProvidersEndpoint.cs | Fallback | `appointments:view` | `GetServiceProvidersQuery` |
| GET | `/login/demo` | AppointMe.Api/Authentication/DemoLogin/DemoLoginEndpoint.cs | AllowAnonymous + `ExcludeFromDescription` | — | none — demo cookie sign-in via `IDemoUserAuthenticator`, redirects to frontend |

**Total: 29 `IEndpoint` implementations** (3 Identity, 14 Organizations, 5 CRM, 6 Booking, 1 API host).

## Notes / observations

- **Absolute paths** (module roots): Identity `/Users/maksak/projects/bravodev/public/appointme/src/Identity/AppointMe.Identity`, Organizations `/Users/maksak/projects/bravodev/public/appointme/src/Organizations/AppointMe.Organizations`, CRM `/Users/maksak/projects/bravodev/public/appointme/src/CRM/AppointMe.Crm`, Booking `/Users/maksak/projects/bravodev/public/appointme/src/Booking/AppointMe.Booking`, API host `/Users/maksak/projects/bravodev/public/appointme/src/AppointMe.Api`.
- **Every endpoint** sets `.WithName(nameof(HandlerMethod))` for link generation / OpenAPI operationId.
- **Permission definitions** (auto-discovered): `CustomerPermissions` (view/create/update/delete), `AppointmentPermissions` (view/schedule/reschedule/cancel), `EmployeePermissions` (view/invite/remove/update_roles), `InvitationPermissions` (resend/cancel), `PermissionPermissions` (view + `Manage` which is a `SystemPermission`).
- **Auth inconsistency worth flagging:** `GetPendingInvitations` and `AcceptInvitation` add an explicit `.RequireAuthorization()` (redundant given the fallback policy already requires authenticated+registered), while all other protected endpoints rely on the implicit fallback. Their handlers do **no** `principal.Require(...)` permission check, so any registered user can list/accept invitations addressed to them.
- **`GetCurrentUserAccess`, `Onboarding`** are protected by the fallback (authenticated + registered) but perform no permission check — appropriate since onboarding creates the company and access returns the caller's own permissions.
- **`GetServiceProviders`** reuses `AppointmentPermissions.View` (there is no separate service-provider permission).
- **Non-`IEndpoint` routes in the pipeline:** `MapOpenApi()` (Development only, AllowAnonymous), `MapFallbackToFile("index.html")` (AllowAnonymous, serves SPA), and the Hangfire dashboard.
- **Request-to-command mapping helpers:** CRM `RegisterCustomer`/`UpdateCustomer` and all Booking write endpoints use `request.ToCommand(...)` / `request.ToQuery()` extension methods rather than inline command construction.



<!-- section: Data Access Layers -->

## Data Access Layers — AppointMe (modular monolith, .NET 10 / EF Core 10 + Dapper)

Each bounded context owns one `DbContext` + one SQL Server schema, plus a set of Dapper read repositories. Writes go through EF Core; reads go through Dapper. All contexts share a single physical database (connection string `AppointMeSql`) and are separated by schema.

---

### 1) DbContexts and schemas

| DbContext | Schema const | File | DbSets | Notes |
|---|---|---|---|---|
| `IdentityDbContext` | `"identity"` (`DefaultSchema`) | `src/Identity/AppointMe.Identity/Database/IdentityDbContext.cs` | `Users` | `sealed`; no tenant filter injected |
| `OrganizationsDbContext` | `"organizations"` (`DefaultSchema`) | `src/Organizations/AppointMe.Organizations/Database/OrganizationsDbContext.cs` | `Companies`, `Employees`, `Invitations`, `RolePermissionOverrides` | non-sealed; ctor takes `ICurrentCompany`; converts `Role` via `RoleValueConverter` in `ConfigureConventions` |
| `CrmDbContext` | `"crm"` (`DefaultSchema`) | `src/CRM/AppointMe.Crm/Database/CrmDbContext.cs` | `Customers` | `sealed`; ctor takes `ICurrentCompany` |
| `BookingDbContext` | `"booking"` (const named `Schema`, not `DefaultSchema`) | `src/Booking/AppointMe.Booking/Database/BookingDbContext.cs` | `Appointments`, `Attendees`, `ServiceProviders`, `BookingCompanies` | `sealed`; ctor takes `ICurrentCompany` |

All four call `modelBuilder.HasDefaultSchema(...)` and `ApplyConfigurationsFromAssembly(ModuleAssembly.Instance)` (Booking uses `Infrastructure.BookingModuleAssembly.Instance`) to auto-apply `IEntityTypeConfiguration` in that module.

Registration (per module `Configuration/*Module.cs`): each calls `.AddDbContext<T>(...)` with `options.UseSqlServer(ConnectionStrings.AppointMeSql, builder => builder.MigrationsHistoryTable("__EFMigrationsHistory", <schema>))` — i.e. a **per-schema migrations history table**. Confirmed in `CrmModule.cs`, `BookingModule.cs`, `OrganizationsModule.cs`, `IdentityModule.cs`.

---

### 2) CQRS split — EF for writes, Dapper for reads

**Writes = EF Core.** Command handlers inject the `DbContext` directly, mutate aggregates, and call `SaveChangesAsync`. Example `src/CRM/AppointMe.Crm/Customers/RegisterCustomer/RegisterCustomerCommandHandler.cs`:
```csharp
public sealed class RegisterCustomerCommandHandler(CrmDbContext dbContext, TimeProvider timeProvider)
...
await dbContext.Customers.AddAsync(customer, cancellationToken);
await dbContext.SaveChangesAsync(cancellationToken);
```
Same pattern in `UpdateCustomer`, `DeleteCustomer`, `SeedDemoCustomers` handlers (all call `dbContext.SaveChangesAsync`).

The `*Queries.cs` files named in the task brief are **NOT Dapper** — they are EF-Core `IQueryable<T>` extension helpers used on the write/aggregate-load path (single-aggregate loads that throw `NotFoundException`):
- `CustomerQueries.LoadAsync` (`src/CRM/.../Customers/Database/CustomerQueries.cs`)
- `AppointmentQueries.LoadAsync` (`src/Booking/.../Appointments/Database/AppointmentQueries.cs`)
- `EmployeeQueries.LoadAsync` (`src/Organizations/.../Employees/Database/EmployeeQueries.cs`) — filters by `Id` + `CompanyId`
- `RolePermissionOverrideQueries.LoadAsync` (`src/Organizations/.../Settings/Permissions/Database/RolePermissionOverrideQueries.cs`) — `ToDictionaryAsync` keyed by `(PermissionCode, Role)`
- also `CompanyQueries`, `EmployeeInvitationQueries` (the latter uses `.IgnoreQueryFilters([EmployeeInvitationFilters.CompanyId])` in `LoadForRecipientAsync`), `ServiceProviderQueries`, `AttendeeQueries`.
All use `SingleOrDefaultAsync` with strongly-typed-ID predicates — no raw SQL, fully parameterized by EF.

**Reads = Dapper.** The list/projection queries live in `*Repository.cs` classes (not `*Queries.cs`), each injecting `IDbConnectionFactory`:
- `CustomersRepository` — `src/CRM/AppointMe.Crm/Customers/Database/CustomersRepository.cs` (`GetAll`, `LoadById`)
- `AppointmentsRepository` — `src/Booking/AppointMe.Booking/Appointments/Database/AppointmentsRepository.cs` (`GetByDateRange`, `LoadById`; 2× INNER JOIN to ServiceProviders/Attendees)
- `TeamRepository` — `src/Organizations/AppointMe.Organizations/Employees/Database/TeamRepository.cs` (`GetTeam`; `UNION ALL` of Employees + EmployeeInvitations)
- `ServiceProvidersRepository` — `src/Booking/AppointMe.Booking/ServiceProviders/Database/ServiceProvidersRepository.cs` (`GetAll`, static SQL, no builder)

Read handlers inject the repository, not the DbContext. Example `src/CRM/AppointMe.Crm/Customers/GetCustomers/GetCustomerQueryHandler.cs` injects `CustomersRepository` and calls `repository.GetAll(query.Search.Tokenize(), ...)`.

**Connection factory.** Interface `IDbConnectionFactory.OpenConnectionAsync` (`src/AppointMe.Shared/Database/IDbConnectionFactory.cs`); impl `SqlConnectionFactory` (`src/AppointMe.Shared/Database/SqlConnectionFactory.cs`) opens a new `Microsoft.Data.SqlClient.SqlConnection(connectionString)` per call. It is registered `AddSingleton<IDbConnectionFactory, SqlConnectionFactory>(...)` **once per module** (CRM, Booking, Organizations, Identity modules all register it, each pointing at the same `ConnectionStrings.AppointMeSql`) — so the last-registered wins as the single resolved singleton; harmless because all use the identical connection string. Dapper type handlers (`DateOnlyTypeHandler`, `UtcDateTimeOffsetTypeHandler`) are registered globally in `SharedModule.AddSharedModule` via `DapperTypeHandlerRegistration.Register()`.

---

### 3) Raw-SQL building — parameter binding vs concatenation

`ExtSqlBuilder` (`src/AppointMe.Shared/Database/Dapper/ExtSqlBuilder.cs`) subclasses Dapper's `SqlBuilder` and drives the `/**where**/`, `/**orderby**/`, `/**pagination**/`, `/**totalcount**/` template markers in the repo SQL constants.

- **Pagination — bound.** `AddPagination` binds `@Offset`/`@Limit` as parameters:
```csharp
AddClause("pagination", "OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY",
    new { pagination.Offset, pagination.Limit }, " ");
```
The `totalcount` clause injects a literal `COUNT(*) OVER () AS [TotalCount]` (no user input).

- **Search — value bound, column-name concatenated.** `SqlBuilderExtensions.WhereSearch`:
```csharp
public SqlBuilder WhereSearch(string column, string[] searchTokens)
{
    for (var i = 0; i < searchTokens.Length; i++)
    {
        var parameters = new DynamicParameters();
        parameters.Add($"SearchToken{i}", searchTokens[i]);
        sqlBuilder.Where($"{column} LIKE '%' + @SearchToken{i} + '%'", parameters);
    }
    return sqlBuilder;
}
```
The user-supplied search **value** is passed as a bound Dapper parameter (`@SearchToken{i}`) — safe. The `column` argument **is string-concatenated** into the SQL fragment, but at every call site it is a hard-coded string literal, never user input:
  - `CustomersRepository.GetAll`: `.WhereSearch("[SearchKey]", searchTokens)`
  - `TeamRepository.GetTeam`: `.WhereSearch("TeamMembers.[SearchKey]", filter.SearchTokens)`
  So there is **no SQL-injection exposure from user input** in the current code, though the API contract of `WhereSearch` (concatenating the column identifier) is injection-prone if ever called with untrusted input.

- **Where clauses in repos — bound.** e.g. `CustomersRepository`: `.Where("[CompanyId] = @CompanyId", new { CompanyId = companyId.Value })`, `.Where("[Id] = @Id", new { Id = customerId.Value })`, literal `.Where("[IsDeleted] = 0")`. `AppointmentsRepository.GetByDateRange` binds `@CompanyId`, `@To` (`range.End`), `@From` (`range.Start`) and uses a literal `OrderBy("Appointments.[Start]")`. `TeamRepository` seeds `@CompanyId` via `DynamicParameters` and passes them through `AddTemplate(TeamSqlTemplate, parameters)`.

- **Search tokenization.** `SearchExtensions.Tokenize` (`src/AppointMe.Shared/Utilities/SearchExtensions.cs`) splits on whitespace, lowercases, de-dupes, and caps at 30 tokens — each becomes an AND'd `LIKE` (bounded token count limits parameter explosion). Values still flow only through bound parameters.

- **Pagination read.** `DbConnectionPaginationExtensions.QueryWithPaginationAsync<T>` (`src/AppointMe.Shared/Database/Dapper/DbConnectionPaginationExtensions.cs`) uses Dapper multi-mapping with `splitOn: "TotalCount"` to pull the window-function count out of each row. `PaginationFilter` (`src/AppointMe.Shared/Pagination/PaginationFilter.cs`) clamps `Limit` to 0–1000 and `Page` ≥ 1, so `@Offset`/`@Limit` are always sane integers.

**Net: all user-controlled values are parameter-bound; the only concatenation is of compile-time-constant column identifiers.**

---

### 4) Global query filters / soft-delete / tenant filters

EF global filters are applied in each context's `OnModelCreating` using named filters (EF Core 10 multiple-filter API). The named-filter constants live in `*Filters.cs` (each just declares `const string SoftDelete`/`CompanyId = nameof(...)`):

- **Identity** (`IdentityDbContext`): `User` → `UserFilters.SoftDelete` (`user => !user.IsDeleted`). No tenant filter. (`UserFilters.cs`)
- **Organizations** (`OrganizationsDbContext`):
  - `Employee` → `EmployeeFilters.SoftDelete` (`!IsDeleted`) **and** `EmployeeFilters.CompanyId` (`CompanyId == currentCompany.CompanyId`)
  - `EmployeeInvitation` → `EmployeeInvitationFilters.CompanyId` (tenant only; no soft-delete)
  - `RolePermissionOverride` → `RolePermissionOverrideFilters.CompanyId` (tenant only)
- **CRM** (`CrmDbContext`): `Customer` → `CustomerFilters.SoftDelete` (`!IsDeleted`) + `CustomerFilters.CompanyId`
- **Booking** (`BookingDbContext`) — all keyed off `BookingFilters.SoftDelete`/`CompanyId`:
  - `Appointment` → CompanyId only (**no soft-delete filter**)
  - `Attendee` → SoftDelete + CompanyId
  - `ServiceProvider` → SoftDelete + CompanyId
  - `BookingCompany` → no filters

Tenant (`CompanyId`) comes from `ICurrentCompany.CompanyId` injected into the context ctor, so EF reads are tenant-scoped automatically. Named filters allow selective bypass, e.g. `EmployeeInvitationQueries.LoadForRecipientAsync` calls `.IgnoreQueryFilters([EmployeeInvitationFilters.CompanyId])` to look up an invitation across tenants by id+email.

**Important:** These filters apply **only to the EF path**. The Dapper read repositories bypass EF entirely and must re-implement tenant/soft-delete predicates by hand — e.g. `CustomersRepository` re-adds `[CompanyId] = @CompanyId` + `[IsDeleted] = 0`; `ServiceProvidersRepository` re-adds both; `AppointmentsRepository` filters `Appointments.[CompanyId]` but (consistent with the entity's EF config) has no `IsDeleted` predicate on Appointments and does **not** re-apply `IsDeleted = 0` on the joined `ServiceProviders`/`Attendees`. This manual duplication is a drift risk: any future change to an EF global filter must be mirrored in the corresponding hand-written SQL or the two paths will diverge.

---

### 5) Migrations mechanism

`src/AppointMe.Shared/Database/Migrations/DatabaseMigrations.cs`:
- `DatabaseMigrationsExtensions.AddDatabaseMigration<TContext>()` registers (a) a single `IHostedService` (`DatabaseMigrationService`, via `TryAddEnumerable` so it is added once) and (b) configures `DatabaseMigrationOptions` to `Register<TContext>()`. Each module calls `.AddDatabaseMigration<TContext>()` (seen in all four `*Module.cs`), so every context is registered into one ordered, de-duplicated list (`DatabaseMigrationOptions` uses a `HashSet` guard + ordered `List`).
- `DatabaseMigrationService.StartAsync` opens a DI scope and iterates the registered context types in registration order, calling `context.Database.MigrateAsync(...)` on each (logging before/after). `StopAsync` is a no-op. So migrations run automatically at host startup, sequentially, once per context, each into its own `__EFMigrationsHistory` table in its own schema.

---

### Key file paths
- Contexts: `src/{Identity,Organizations,CRM,Booking}/.../Database/*DbContext.cs`
- Connection abstraction: `src/AppointMe.Shared/Database/IDbConnectionFactory.cs`, `SqlConnectionFactory.cs`
- SQL building: `src/AppointMe.Shared/Database/Dapper/ExtSqlBuilder.cs`, `DbConnectionPaginationExtensions.cs`, `src/AppointMe.Shared/Utilities/SearchExtensions.cs`
- Dapper read repos: `.../Customers/Database/CustomersRepository.cs`, `.../Appointments/Database/AppointmentsRepository.cs`, `.../Employees/Database/TeamRepository.cs`, `.../ServiceProviders/Database/ServiceProvidersRepository.cs`
- EF query helpers: `.../*/Database/*Queries.cs`
- Filters: `.../*/Database/*Filters.cs`
- Migrations: `src/AppointMe.Shared/Database/Migrations/DatabaseMigrations.cs`
- Registration/wiring: `src/{...}/Configuration/*Module.cs`, `src/AppointMe.Shared/Configuration/{SharedModule,ConnectionStrings}.cs`, `DapperTypeHandlerRegistration.cs`



<!-- section: External Integrations & File/Resource Management -->

## Overview

The app is a modular .NET 10 monolith. Its external touchpoints are: two pluggable identity providers (Keycloak / Entra External ID), SQL Server (data + Wolverine message store + Hangfire store), Azure Service Bus (optional Wolverine transport), Hangfire (recurring reconciliation jobs), and optional Azure Blob Storage (DataProtection key ring). **The application never sends email itself** — invitation/verification email is delegated to the identity provider. There is essentially **no application-level file I/O**; the only file handling is static frontend assets, a dev-cert export script, and cert-based storage config.

A single config switch `Authentication:Provider` (default `"Keycloak"`) selects both the *authentication* scheme (API side) and the *provisioning* identity provider (Identity module side). It is read in three independent places:
- `src/AppointMe.Api/Authentication/AuthenticationExtensions.cs:18` (which auth options to bind)
- `src/Identity/AppointMe.Identity/Configuration/IdentityModule.cs:40` (which `IIdentityProvider` to register)
- `src/AppointMe.Api/Authentication/DemoLogin/DemoModeExtensions.cs:18` (which demo authenticator's `HttpClient` to register)

---

## 1. Identity Providers

### `IIdentityProvider` abstraction
`src/Identity/AppointMe.Identity/IIdentityProvider.cs` — two methods only: `CreateUserWithPasswordSetup(email, name, redirectUri, ct)` and `ResendInvitationEmail(email, redirectUri, ct)`. Registered `AddScoped` by whichever provider extension the module selects.

### Keycloak (default)
- **Provisioning**: `src/Identity/AppointMe.Identity/Keycloak/KeycloakIdentityProvider.cs`. Uses the `Schick.Keycloak.RestApiClient` (FS.Keycloak) package (`AppointMe.Identity.csproj:19`). It builds an `AuthenticationHttpClient` via **client-credentials flow** (`CreateHttpClient`, lines 81-90) using `KeycloakAdminOptions` (BaseUrl, Realm, ClientId, ClientSecret). `CreateUserWithPasswordSetup` POSTs a `UserRepresentation` with `RequiredActions = [VERIFY_EMAIL, UPDATE_PASSWORD]`, extracts the new user id from the `Location` header (`ExtractUserIdFromLocation`, lines 93-104), then calls `PutUsersSendVerifyEmailByUserIdAsync` which makes **Keycloak** send the verification email. On HTTP 409 it treats the user as already-existing and looks them up by email. `ResendInvitationEmail` re-triggers `PutUsersSendVerifyEmailByUserIdAsync`.
- **Admin credentials source**: `KeycloakAdminOptions` (`src/Identity/AppointMe.Identity/Keycloak/KeycloakAdminOptions.cs`) — all 4 fields `[Required]`. Bound from config section `"KeycloakAdmin"` in `KeycloakServiceCollectionExtensions.cs:11-14` with `ValidateDataAnnotations().ValidateOnStart()`. Concrete dev values live **in git-tracked plaintext** at `src/AppointMe.Api/appsettings.Development.json:19-24` (ClientId `appointme-api`, ClientSecret `X3Rc...awii`, BaseUrl `https://localhost:8082`, Realm `appointme`).
- **Authentication side (token validation / browser OIDC)**: `src/AppointMe.Api/Authentication/Keycloak/KeycloakAuthenticationExtensions.cs`. Binds `KeycloakOptions` (`Authority`, `FrontendClientId`, `FrontendClientSecret`, `ApiAudience` — `KeycloakOptions.cs`) from `"Authentication:Keycloak"` and projects them into the shared `IdentityProviderOptions`. Adds a `kc_idp_hint` query param passthrough via `CustomizeOidc` (lines 25-37) and registers `KeycloakClaimsTransformer`. Dev secrets again plaintext in `appsettings.Development.json:11-17` (FrontendClientSecret `d7BP...JhVB`).

### Entra External ID (Microsoft Graph)
- **Provisioning**: `src/Identity/AppointMe.Identity/Entra/EntraIdentityProvider.cs`. Uses `Microsoft.Graph` + `Azure.Identity` (`AppointMe.Identity.csproj:10,18`). Authenticates with a **`ClientSecretCredential(TenantId, ClientId, ClientSecret)`** requesting scope `https://graph.microsoft.com/.default` (`CreateGraphClient`, lines 104-108). Both methods funnel to `SendInvitation` which POSTs a Graph `Invitation` with `SendInvitationMessage = true` (Graph sends the email). Has notable error handling: an "ambiguous BadRequest" (existing member vs `+`-alias/invalid SMTP) is disambiguated by looking the user up via `_graph.Users.GetAsync` filtered on `mail eq '{email}'` (`FindUserIdByEmail`, lines 67-76) — note this interpolates the email directly into the OData filter.
- **Admin credentials source**: `EntraIdentityOptions` (`src/Identity/AppointMe.Identity/Entra/EntraIdentityOptions.cs`) — `TenantId`, `ClientId`, `ClientSecret`, all `[Required]`. Bound from config section `"EntraIdentity"` in `EntraServiceCollectionExtensions.cs:11-14`. Empty placeholders in `appsettings.json:29-33`; devtest config (`appsettings.Devtest.json:21-24`) supplies TenantId + ClientId but **not** ClientSecret — the secret must come from an out-of-band source (user-secrets/env var), not the tracked file.
- **Authentication side**: `src/AppointMe.Api/Authentication/EntraExternalId/EntraExternalIdAuthenticationExtensions.cs` binds `EntraExternalIdOptions` (`Authority`, `ClientId`, `ClientSecret`, `ApiAudience`) from `"Authentication:EntraExternalId"` and registers `EntraExternalIdClaimsTransformer`.

### Shared auth wiring
`src/AppointMe.Api/Authentication/AuthenticationExtensions.cs` builds a **hybrid scheme** (`HybridAuthenticationDefaults`): a policy scheme forwards to JWT Bearer when an `Authorization: Bearer` header is present, else to Cookie. OIDC (`AddOpenIdConnect`) drives browser login; `OnTokenValidated` (lines 87-105) deliberately stores **only the id_token** in the auth cookie (comment: avoids blowing past Keycloak's 8KB header limit). Cookie is `appointme.auth`, HttpOnly, SameSite=Lax, `SecurePolicy=Always`. `RequireHttpsMetadata` is config-driven (`Authentication:RequireHttpsMetadata`, default false in dev, true in devtest). Both OIDC and JWT options pull `Authority`/`ClientId`/`Audience` from the provider-agnostic `IdentityProviderOptions` (`src/AppointMe.Api/Authentication/IdentityProviderOptions.cs`).

### Demo login (bypasses browser redirect)
`src/AppointMe.Api/Authentication/DemoLogin/` — gated by `Demo:Enabled`. `IDemoUserAuthenticator` has two impls registered via typed `HttpClient`:
- `KeycloakDemoUserAuthenticator.cs` — **ROPC** (`grant_type=password`) against `{Authority}/protocol/openid-connect/token`, reusing the OIDC client id/secret; requires realm client "Direct Access Grants". Returns the `id_token`.
- `EntraExternalIdDemoUserAuthenticator.cs` — Entra **native-auth** 3-legged flow (`initiate` → `challenge` → `token`) since Entra doesn't support ROPC; derives the native-auth base URL from the authority. Returns `id_token`.
Demo user email/password/name come from `Demo:User` config (plaintext `demo@appointme.dev` / `AppointMe1` in `appsettings.Development.json` and `appsettings.Devtest.json`).

---

## 2. Email (Mailpit / SMTP)

There is **no SMTP client, MailKit, or `IEmailSender` in application code** — email is entirely the identity provider's responsibility (Keycloak `send-verify-email`, Entra invitation `SendInvitationMessage=true`).

- The invite chain: `InviteEmployeeCommandHandler.cs` (Organizations) persists an `EmployeeInvitation` and raises a domain event; the Identity module's `EmployeeInvitedHandler.cs` / `EmployeeInvitationResentHandler.cs` (`src/Identity/AppointMe.Identity/Users/InviteUser/`) react and call `IIdentityProvider.CreateUserWithPasswordSetup` / `ResendInvitationEmail`, passing `FrontendOptions.InvitationUrl` as the redirect. So email dispatch is triggered by domain events flowing through Wolverine.
- `FrontendOptions` (`src/AppointMe.Shared/Configuration/FrontendOptions.cs`) derives `InvitationUrl` = `BaseUrl` + `/auth/login`; `BaseUrl` bound from `"Frontend"` config (`https://localhost:5173` dev).
- **Keycloak's** SMTP is configured in the realm import, not the app: `src/AppointMe.Aspire/appointme-realm.json:1733-1743` sets `smtpServer` host `Mailpit`, port `1025`, from `noreply@appointme.local`. Mailpit runs as an Aspire container (see below).

---

## 3. Azure Service Bus / Wolverine transport

`src/AppointMe.Api/Wolverine/WolverineHostBuilderExtensions.cs` (`AddWolverine`). Packages: `WolverineFx`, `WolverineFx.AzureServiceBus`, `WolverineFx.SqlServer`, `WolverineFx.EntityFrameworkCore`, `WolverineFx.RuntimeCompilation` (`AppointMe.Api.csproj:27-31`).
- **Message persistence is always SQL Server**: `PersistMessagesWithSqlServer(GetConnectionString("AppointMeSql"))`, schema `wolverine` (lines 25-27, 37). EF-core transactions + `PublishDomainEventsFromEntityFrameworkCore<AggregateRoot>`; durable local queues.
- **Transport is switched by `Wolverine:Transport`** (default `"SqlDurable"`, lines 21, 52-66):
  - `SqlDurable` — SQL durable local queues only, **no external broker**.
  - `AzureServiceBus` — `options.UseAzureServiceBus(connectionString).AutoProvision()` using connection string **`AppointMeMessaging`** (thrown if missing). Auth is entirely via that connection string (`appsettings.json:13` has an empty `AppointMeMessaging` placeholder; devtest sets `Wolverine:Transport=AzureServiceBus` but the connection string is supplied out-of-band).
- Several context policies are attached (`TenantContextPolicy`, `DomainEventContextPolicy`, `IdentityContextPolicy`, `PrincipalContextPolicy`, `CompanyContextPolicy`) — impls in `src/AppointMe.Api/Wolverine/HandlerContext/`. Durability agent disabled during codegen.

---

## 4. SQL Server connection

- Single logical DB named **`AppointMeSql`** shared by all module DbContexts (each with its own schema), Wolverine, and Hangfire.
- Connection string resolution: `ConnectionStrings` (`src/AppointMe.Shared/Configuration/ConnectionStrings.cs`) reads `GetConnectionString("AppointMeSql")` and throws if absent; registered as singleton in `SharedModule.cs:24`. Dapper reads go through `SqlConnectionFactory` (`src/AppointMe.Shared/Database/SqlConnectionFactory.cs`) wrapping `Microsoft.Data.SqlClient.SqlConnection`; registered per-module (e.g. `IdentityModule.cs:35-36`). EF writes use `UseSqlServer(...)` per DbContext (e.g. `IdentityModule.cs:26`).
- **Credentials**: plaintext connection string in git-tracked `appsettings.Development.json:9` (`Server=localhost,60740;...User ID=sa;Password=Password1;TrustServerCertificate=True`). In Aspire, the reference is injected (see below).

---

## 5. Hangfire jobs & recurring scheduling

`src/AppointMe.Api/Hangfire/`. Packages `Hangfire.AspNetCore`, `Hangfire.Core`, `Hangfire.SqlServer` (`AppointMe.Api.csproj:10-12`). Skipped entirely during codegen (`Program.cs:36-39,77-80`).
- **Setup** — `HangfireExtensions.cs`: `UseSqlServerStorage(AppointMeSql, schema "hangfire", PrepareSchemaIfNecessary=true)`, `AddHangfireServer()`, and a `SystemContextJobFilter`. Storage auth is the same `AppointMeSql` connection string.
- **Dashboard** — `UseAppointMeHangfireDashboard()` mounts `/admin/jobs` with **`Authorization = []` (no auth filter — open dashboard)**, per the explicit comment (`HangfireExtensions.cs:38-46`). Worth flagging.
- **System identity for jobs** — `SystemContextJobFilter.cs` (`IServerFilter`) swaps in a `SystemIdentity` / `SystemPrincipal` for the duration of each job via `ICurrentIdentity.Change` / `ICurrentPrincipal.Change`, disposing the scopes in `OnPerformed`.
- **Recurring registration** — `RecurringJobsHostedService.cs` (an `IHostedService`) iterates all DI-registered `IRecurringJobRegistrar` on startup and calls `Register(scheduler)`. `HangfireRecurringJobScheduler.cs` adapts to Hangfire's `IRecurringJobManager.AddOrUpdate`. Abstractions live in `src/AppointMe.Shared/Jobs/` (`IRecurringJobRegistrar.cs`, `IRecurringJobScheduler.cs`).
- **Reconciliation jobs** (all in `src/Booking/AppointMe.Booking/`, each a `Job` + a `Registrar`):
  - `Attendees/ReconcileAttendees/AttendeeReconciliationJob.cs` — loads all `BookingCompanies`, calls `bus.InvokeForTenantAsync(companyId, ReconcileAttendeesCommand)` per company. Cron `0 3 * * *` (daily 03:00), JobId `booking:attendee-reconciliation`.
  - `BookingCompanies/ReconcileBookingCompanies/BookingCompanyReconciliationJob.cs` — `bus.InvokeAsync(ReconcileBookingCompaniesCommand)`. Cron `0 * * * *` (hourly), JobId `booking:company-reconciliation`.
  - `ServiceProviders/ReconcileServiceProviders/ServiceProviderReconciliationJob*.cs` — hourly `0 * * * *`, JobId `booking:service-provider-reconciliation`.
  - Pattern: Hangfire jobs are thin triggers that push commands onto Wolverine; the actual reconciliation runs as Wolverine handlers (cross-module projection sync).

---

## 6. File I/O, certs, DataProtection

- **Application file I/O**: none of note. `Program.cs:72` `UseStaticFiles()` + `MapFallbackToFile("index.html")` serve the built SPA from `wwwroot` (`AppointMe.Api.csproj:35` copies `wwwroot\**`). No upload/download endpoints, no `IFormFile`, no `Path.Combine`/`Directory`/`FileStream` usage in module code (grep only hit csproj, DataProtection, and the realm JSON).
- **DataProtection key storage** — `src/AppointMe.Api/DataProtection/DataProtectionExtensions.cs`: `SetApplicationName("appointme")`. If connection string **`DataProtectionStorage`** is configured, keys persist to **Azure Blob Storage** (`PersistKeysToAzureBlobStorage`, container `data-protection-keys`, blob `keys.xml`) via package `Azure.Extensions.AspNetCore.DataProtection.Blobs` (`AppointMe.Api.csproj:9`). Auth is entirely via that blob connection string. If empty (default, `appsettings.json:14`), falls back to the local key ring — meaning keys are ephemeral/per-instance unless Blob is configured.
- **Dev HTTPS cert** — `docker/keycloak/export-dev-cert.sh`: runs `dotnet dev-certs https --trust` then exports PEM (`keycloak.crt` + unencrypted `keycloak.key`, `--no-password`) into `docker/keycloak/certs/` so a docker-compose Keycloak can serve HTTPS on `localhost:8082`. The generated `*.crt`/`*.key` are **git-ignored** (`.gitignore:380-382`); only `.gitkeep` and the script are tracked. (Note: there is no docker-compose file in the repo currently; the Aspire path uses `AddKeycloak` instead — the script/cert dir appear to be for a separate compose workflow.)
- **Forwarded headers** — `Program.cs:50-55,63` enables `XForwardedFor|XForwardedProto` with `KnownProxies`/`KnownIPNetworks` cleared (trusts all proxies) — relevant when behind a reverse proxy/TLS terminator.

---

## 7. Local dev orchestration (Aspire)

`src/AppointMe.Aspire/Program.cs` wires the backing services (used by `dotnet run` in AppointMe.Aspire):
- **SQL Server** container `mssql/server:2025-CU1-ubuntu-24.04`, host port 60740, password from parameter `sqlPassword` (default `Password1`, inline in `Program.cs:5`), persistent data volume; database `AppointMeSql`/`AppointMe`. Injected into the API via `.WithReference(database)`.
- **Keycloak** via `AddKeycloak` (package `Aspire.Hosting.Keycloak`) on port 8082, admin creds from parameters `username`/`password` (default `admin`/`admin`), `WithRealmImport("appointme-realm.json")`, persistent volume. Referenced by the API.
- **Mailpit** container `axllent/mailpit` — SMTP `1026→1025`, web UI `8026→8025`. The API `WaitFor(mailpit)` but references it indirectly (Keycloak's realm SMTP points at it).
- **Frontend** Vite app on HTTPS 5173, referencing the API.
- Note: the API's Keycloak *admin/OIDC secrets* still come from its own `appsettings.Development.json` (the realm import in `appointme-realm.json` defines matching client secrets), not from Aspire injection.

---

## Secret-handling summary (for follow-up review)

- **Plaintext secrets committed to git**: `appsettings.Development.json` (Keycloak FrontendClientSecret, KeycloakAdmin ClientSecret, SQL sa password, demo password). Acceptable-ish for local-only dev creds tied to the imported realm, but they are real secrets in source control.
- **Devtest** (`appsettings.Devtest.json`, tracked) exposes Entra tenant/client IDs but correctly omits the Entra `ClientSecret` and the Azure Service Bus / DataProtection connection strings — those are expected from user-secrets/env.
- All external auth reduces to either a **client secret** (Keycloak client-credentials, Entra `ClientSecretCredential`) or a **connection string** (SQL, Service Bus, Blob). No managed-identity/tokenless auth is used anywhere.




---

# Appendix B — All Verified Findings (raw, un-deduplicated: 31 originally; 27 listed)


Each entry is a finding that survived adversarial verification. These are the raw per-dimension outputs; the register in the main body de-duplicates and re-prioritizes them. Entries for fully fixed findings are removed (B3, Hangfire dashboard; B11, optimistic concurrency; B14/B19, logout CSRF; B16/B30, timezone rehydration; B17, pagination ceiling; B21, HSTS; B22, security response headers; B23, RequireHttpsMetadata default — see the remediation-status note in the register); the original numbering is preserved.


## B1. [High] UpdateEmployeeRoles lets a non-owner assign the protected Owner SystemRole, enabling vertical privilege escalation and self-promotion

- **Location:** `src/Organizations/AppointMe.Organizations/Employees/UpdateEmployeeRoles/UpdateEmployeeRoles.cs:23`
- **Dimension / category:** authz-idor / privilege-escalation
- **Verdict:** CONFIRMED
- **Needs architectural review:** yes

**Explanation.** The Roles array is bound directly from the request body (UpdateEmployeeRolesRequest.Roles -> UpdateEmployeeRolesCommand.Roles) and passed to Employee.UpdateRoles. The only guard is that entries in `lockedRoles` are not removed; there is NO check that the roles being ADDED are permitted. `Role.Owner` is deliberately a `SystemRole` and is excluded from `Role.Configurable`, and the permission-override subsystem explicitly forbids touching it (UpdatePermissions.ValidateGrants throws `role_permissions_immutable` for any SystemRole and `system_managed_permission` for the SystemPermission `permissions:manage`). This slice bypasses that entire protection: EmployeePermissions.UpdateRoles is granted by default to the Manager role (EmployeeRolesGrants.DefaultGrants), and the handler applies no self-target guard (unlike DeleteEmployee). So a Manager can PUT /employees/{ownEmployeeId}/roles with body {"roles":["Owner"]} and on the next request be resolved as an Owner, which grants PermissionPermissions.Manage (rewrite all company permission overrides) and CustomerPermissions.Delete — effectively full control of the tenant. `LockedRolesFor` only protects the single primary owner from having Owner removed; it does nothing to stop Owner being granted to others.

**Evidence.**
```
var removed = lockedRoles.Where(role => !roles.Contains(role)).ToArray();
if (removed.Length > 0)
{
    throw new ValidationException(...);
}
employee.Roles = roles.ToList();  // no check that `roles` excludes SystemRole/Owner
```

**Proposed remediation.**
```
Constrain assignable roles to a whitelist and reject SystemRoles that the caller is not already entitled to grant. e.g. before assigning:

var addedRoles = roles.Except(employee.Roles);
if (addedRoles.Any(role => role is SystemRole))
    throw new AccessDeniedException("The Owner role cannot be assigned.");
if (roles.Any(role => !Role.Configurable.Contains(role) && !lockedRoles.Contains(role)))
    throw new ValidationException("Only configurable roles may be assigned.");

and add a self-target guard in UpdateEmployeeRolesCommandHandler (mirroring DeleteEmployeeCommandHandler) so a member cannot rewrite their own roles.
```

**Verification.** Traced the full exploit chain in code and found no mitigation. UpdateEmployeeRoles.UpdateRoles (UpdateEmployeeRoles.cs:9-23) only guards against REMOVING lockedRoles (lines 16-21) then unconditionally assigns employee.Roles = roles.ToList() (line 23); there is no check that added roles exclude SystemRole/Owner. The endpoint (UpdateEmployeeRolesEndpoint.cs:16) binds the request body's Roles[] straight into the command, and RoleJsonConverter is globally registered (src/AppointMe.Api/Json/JsonOptionsExtensions.cs:17) calling Role.Create, so {"roles":["Owner"]} deserializes to the canonical Role.Owner SystemRole (Role.cs:33; confirmed by RoleTests). The handler (UpdateEmployeeRolesCommandHandler.cs:13-18) requires only EmployeePermissions.UpdateRoles — which EmployeeRolesGrants.cs:15-20 grants to Role.Manager by default — and applies no self-target guard, unlike DeleteEmployeeCommandHandler.cs:14-17. LockedRolesFor (CompanyOwnership.cs:12-13) returns [Owner] only for the primary owner and [] otherwise, so for a Manager targeting their own (or any) employee, removed is empty and Owner is assigned and persisted. On the next request UserPrincipalFactory.cs:17-33 reloads the stored roles (rehydrated to the Owner SystemRole via RoleValueConverter/Role.Create) and PermissionResolver.cs:14-17 unions Owner's default grants — including PermissionPermissions.Manage (PermissionRolesGrants.cs:9-12) and CustomerPermissions.Delete (CrmDefaultGrantPolicy.cs:11-16), neither of which Manager holds. This bypasses the SystemRole immutability boundary the codebase explicitly enforces elsewhere (UpdatePermissions.ValidateGrants throws role_permissions_immutable for any SystemRole, UpdatePermissions.cs:95-99). No middleware, FluentValidation validator, or global filter neutralizes it; the only scoping present is companyId on the employee load, so the escalation is within-tenant, not cross-tenant.

**Verified remediation.**
```
Add a SystemRole guard inside UpdateRoles (it already receives both roles and lockedRoles), placed after the removed-roles check and before the assignment on line 23. Rejecting only non-locked SystemRoles closes the Owner-escalation hole without breaking the primary owner (whose Owner role is in lockedRoles) or any custom non-BuiltIn roles:

    var addedSystemRoles = roles
        .Where(role => role is SystemRole && !lockedRoles.Contains(role))
        .ToArray();
    if (addedSystemRoles.Length > 0)
    {
        throw new ValidationException(
            $"The {string.Join(", ", addedSystemRoles.Select(role => role.Name))} role cannot be assigned.",
            code: "system_role_not_assignable");
    }

    employee.Roles = roles.ToList();

As defense-in-depth, also add a self-target guard in UpdateEmployeeRolesCommandHandler (mirroring DeleteEmployeeCommandHandler.cs:14-17) so a member cannot rewrite their own roles. Note the self-target guard alone is insufficient — without the SystemRole check a Manager could still promote a colleague to Owner — so the SystemRole guard is the load-bearing fix.
```

**Notes.** Precondition: attacker must already hold a role carrying EmployeePermissions.UpdateRoles (Manager or Owner by default), so this is not anonymous-exploitable — hence High rather than Immediate. It is nonetheless a full within-tenant vertical privilege escalation (any Manager becomes Owner, gaining permissions:manage over all company overrides and customers:delete) that defeats the SystemRole immutability invariant the code enforces in UpdatePermissions.ValidateGrants. Employee.LoadAsync is scoped to companyId, so there is no cross-tenant reach. The reviewer's cited line 16 points at the insufficient removal guard; the actual unvalidated mutation is line 23, where the fix belongs.


## B2. [High] InviteEmployee allows creating an invitation with the Owner SystemRole, a second privilege-escalation path

- **Location:** `src/Organizations/AppointMe.Organizations/Invitations/InviteEmployee/InviteEmployee.cs:13`
- **Dimension / category:** authz-idor / privilege-escalation
- **Verdict:** CONFIRMED
- **Needs architectural review:** yes

**Explanation.** EmployeeInvitation.Create accepts arbitrary roles (bound from InviteEmployeeRequest.Roles -> InviteEmployeeCommand.Roles) with no SystemRole/whitelist validation — it only rejects an empty list. EmployeePermissions.Invite is granted by default to Manager. A Manager can therefore POST /invitations with {"roles":["Owner"], ...}; when the recipient accepts (AcceptInvitationCommandHandler calls Employee.Register with invitation.Roles verbatim), a full Owner employee is created. This is the same trust gap as the UpdateEmployeeRoles finding and equally bypasses the SystemRole protection enforced in UpdatePermissions.ValidateGrants.

**Evidence.**
```
var distinctRoles = roles.Distinct().ToList();
if (distinctRoles.Count == 0)
{
    throw new ValidationException("At least one role is required.");
}
// roles used as-is; no rejection of SystemRole/Owner
```

**Proposed remediation.**
```
Validate that every invited role is in Role.Configurable (reject any SystemRole such as Owner) inside EmployeeInvitation.Create, and/or enforce it in InviteEmployeeCommandHandler after principal.Require(EmployeePermissions.Invite). Apply the same whitelist used to fix UpdateEmployeeRoles.
```

**Verification.** The end-to-end chain is real. EmployeeRolesGrants.cs grants EmployeePermissions.Invite to Manager, and InviteEmployeeEndpoint's POST /invitations is gated solely by principal.Require(EmployeePermissions.Invite) in InviteEmployeeCommandHandler:11 — so a Manager is authorized to invite. The request body Roles (Role[]) deserializes via RoleJsonConverter.Read -> Role.Create, and RoleFactory.Create (Role.cs:33) returns Role.BuiltIn.FirstOrDefault(name=="Owner"), i.e. the actual SystemRole Owner singleton. InviteEmployee.Create (InviteEmployee.cs:13) only rejects an empty list — no SystemRole/whitelist filter — and stores Roles verbatim. On accept, AcceptInvitationCommandHandler:25-32 passes invitation.Roles straight into Employee.Register, and RegisterEmployee.cs:13 likewise only rejects empty. RoleValueConverter re-materializes the persisted "Owner" back into the real SystemRole Owner via Role.Create, so permission resolution against DefaultGrants (keyed by Role.Owner) matches at runtime. Owner confers strictly more than Manager: PermissionPermissions.Manage (full permission-override control; Manager only has View) and CustomerPermissions.Delete, and these Owner grants are immutable (UpdatePermissions.ValidateGrants:95 throws for any SystemRole, so they can never be revoked via config). No mitigating control exists: the SystemRole rejection present in UpdatePermissions.ValidateGrants is entirely absent from both the invite and register paths, and CompanyOwnership.LockedRolesFor only prevents removing Owner from the primary owner, never adding Owner to a new member. This is the same trust gap as the referenced UpdateEmployeeRoles finding (UpdateEmployeeRoles.cs:9 also lacks a whitelist). The claim that Employee.Register is used verbatim, that Invite is a Manager-default permission, and that the empty-list-only check is the sole validation are all accurate.

**Verified remediation.**
```
Reject non-configurable/system roles at the aggregate factory, mirroring UpdatePermissions.ValidateGrants, and add the same guard to Employee.Register for defense-in-depth. In InviteEmployee.Create (src/Organizations/AppointMe.Organizations/Invitations/InviteEmployee/InviteEmployee.cs), after computing distinctRoles:

var distinctRoles = roles.Distinct().ToList();
if (distinctRoles.Count == 0)
{
    throw new ValidationException("At least one role is required.");
}

var invalidRoles = distinctRoles.Where(role => role is SystemRole || !Role.Configurable.Contains(role)).ToArray();
if (invalidRoles.Length > 0)
{
    throw new ValidationException(
        $"The following roles cannot be assigned via invitation: {string.Join(", ", invalidRoles.Select(role => role.Name))}",
        code: "role_not_assignable");
}

Add the identical Role.Configurable/SystemRole guard inside RegisterEmployee.Register (RegisterEmployee.cs:13) so the invariant holds even if a future caller bypasses the invitation path. Optionally also validate in InviteEmployeeCommandHandler after principal.Require(EmployeePermissions.Invite). Using Role.Configurable (Manager/Staff/Receptionist) rejects both the Owner SystemRole and any arbitrary custom role name that Role.Create would otherwise accept.
```

**Notes.** Exploitation is conditional, which caps severity at High rather than Immediate: the Manager cannot invite their own current company email (blocked by the isExistingEmployee check in InviteEmployeeCommandHandler:20-27), so the attack needs an account whose email matches the invited address to call accept — a second attacker-controlled email/account, or elevating a colleague beyond the Manager's authority. The AcceptInvitation endpoint only RequireAuthorization() and matches currentUser.Email to the invitation email, so any authenticated user with the invited email can accept. The most damaging consequence is that the newly minted Owner holds PermissionPermissions.Manage and other SystemRole grants that are immutable via the permission-config path (UpdatePermissions.ValidateGrants throws for SystemRole), so the escalation cannot be undone through normal admin config once created. Cited file/line (InviteEmployee.cs:13) is the correct anchor; no correction needed. This should be fixed together with the UpdateEmployeeRoles finding using a shared whitelist to avoid divergence.


## B4. [High] Reconcile service providers silently drops all updates/deletes after the first failed record

- **Location:** `src/Booking/AppointMe.Booking/ServiceProviders/ReconcileServiceProviders/ReconcileServiceProvidersCommandHandler.cs:41`
- **Dimension / category:** robustness / correctness
- **Verdict:** CONFIRMED
- **Needs architectural review:** no

**Explanation.** Unlike the Attendee and BookingCompany reconcilers (which re-query each entity inside UpsertAsync on every iteration), this handler pre-loads the tracked `locals` list ONCE at lines 20-23 and reuses those tracked ServiceProvider instances across all loop iterations. When any single record throws (e.g. PersonName.Create raising ValidationException for an employee with an empty/invalid name), the catch block calls dbContext.ChangeTracker.Clear(), which DETACHES every entity in `locals`. On all subsequent iterations, ServiceProviderSynchronizer.UpdateServiceProvider/DeleteServiceProvider/RestoreServiceProvider mutate these now-detached POCOs, but because EF Core is no longer tracking them, the following SaveChangesAsync generates no SQL and the mutations are silently lost. The loop appears to succeed (no exception, no error log), so a single bad record silently poisons the projection state of every remaining existing provider in the batch. Only the create path (AddAsync) survives a prior Clear().

**Evidence.**
```
catch (Exception exception) when (exception is not OperationCanceledException)
{
    logger.LogError(exception, "Failed to reconcile service provider {ProviderId} in company {CompanyId}.",
        existing?.Id.Value ?? snapshot?.EmployeeId, companyId);
    dbContext.ChangeTracker.Clear();
}
```

**Proposed remediation.**
```
Do not reuse pre-loaded tracked entities across iterations that can Clear() the tracker. Either re-query each provider inside the loop iteration (as UpsertAttendee/UpsertBookingCompany do), or after ChangeTracker.Clear() re-attach/re-load the entity before mutating it. Simplest fix: move the per-provider load into the try block so each iteration works with a freshly-tracked entity, e.g. `var tracked = await dbContext.ServiceProviders.IgnoreQueryFilters().SingleOrDefaultAsync(p => p.Id == existing.Id, ct);` and mutate `tracked`.
```

**Verification.** The defect is real end-to-end. ReconcileServiceProvidersCommandHandler.cs:20-23 loads `locals` once as tracked entities. FullOuterJoin (AppointMe.Shared/Utilities/EnumerableExtensions.cs:15) builds its left dictionary directly from those `locals`, so every `existing` yielded into the loop is a reference to an originally-tracked ServiceProvider instance. ServiceProviderSynchronizer.Apply routes to UpdateServiceProvider/DeleteServiceProvider, which mutate `existing` in place (existing.Update(...), existing.Delete(), existing.Restore()); ServiceProvider.Update (ServiceProviders/UpdateServiceProvider/UpdateServiceProvider.cs) merely assigns Name on the POCO with no re-attach. A realistic trigger exists: PersonName.Create (AppointMe.Shared/Domain/Common/PersonName.cs:39-59) throws ValidationException for empty/oversized names, evaluated inside UpdateServiceProvider (line 54) and CreateServiceProvider (line 63); any per-record exception (including a SaveChangesAsync DB error) also lands in the catch. The catch at line 41 calls dbContext.ChangeTracker.Clear(), which detaches ALL tracked entities, including the ones destined for later iterations. On every subsequent iteration, existing.Update/Delete/Restore mutate a now-detached POCO, and SaveChangesAsync produces no SQL and throws nothing — the mutation is silently lost with no error log. Only the create path (AddAsync of a brand-new instance) survives a prior Clear(). This is unique to this reconciler: the Attendee reconciler (Attendees/ReconcileAttendees/UpsertAttendee.cs:18-20) and BookingCompany reconciler (BookingCompanies/ReconcileBookingCompanies/UpsertBookingCompany.cs:16-17) both re-query the local entity inside UpsertAsync on every iteration, so a prior ChangeTracker.Clear() is harmless for them. No middleware, base class, global filter, or interceptor mitigates this — nothing re-loads or re-attaches locals after Clear().

**Verified remediation.**
```
Do not reuse pre-loaded tracked entities across iterations that can Clear() the tracker. Re-query a freshly-tracked entity inside each iteration's try block so a prior Clear() does not leave it detached. In ReconcileServiceProvidersCommandHandler.HandleAsync, replace the loop body:

foreach (var (existing, snapshot) in pairs)
{
    try
    {
        var tracked = existing is null
            ? null
            : await dbContext.ServiceProviders
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(provider => provider.Id == existing.Id, cancellationToken);

        await synchronizer.Apply(tracked, snapshot, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        logger.LogError(exception, "Failed to reconcile service provider {ProviderId} in company {CompanyId}.",
            existing?.Id.Value ?? snapshot?.EmployeeId, companyId);
        dbContext.ChangeTracker.Clear();
    }
}

(Use `locals`/`pairs` only for pairing keys; fetch the tracked instance per iteration. Note: use cancellationToken, not `ct`.) Alternatively, refactor Apply to accept the snapshot and re-query the local internally, mirroring UpsertAttendee/UpsertBookingCompany.
```

**Notes.** Line 41 (ChangeTracker.Clear()) is the correct anchor for where detachment happens, but the root cause spans lines 20-34 (loading locals once and reusing those tracked instances across iterations). Severity High rather than Immediate: this is a background projection-reconciliation job, so the impact is silent staleness/corruption of the ServiceProviders read model, not a security/auth/data-breach issue. It requires a triggering condition (at least one record in the batch that throws — e.g. an employee with an empty/invalid name, or any transient SaveChanges failure), and it is persistent: because the failing record recurs on each run, records ordered after it are silently dropped on every reconciliation. The create path is unaffected. Concrete failure scenario: a company batch where employee A (existing provider) has a valid updated name and employee B (existing provider, processed earlier) has an empty first name — B throws ValidationException, Clear() detaches A, A's name update (and any later deletes/restores) is silently lost with no exception and no error log.


## B5. [Medium] Demo login mints a full authenticated session with no credentials via an anonymous GET, and is enabled in a deployed config with a committed password

- **Location:** `src/AppointMe.Api/Authentication/DemoLogin/DemoLoginEndpoint.cs:23`
- **Dimension / category:** auth-session / auth-session
- **Verdict:** CONFIRMED
- **Needs architectural review:** yes

**Explanation.** To answer the specific question: the demo path CANNOT be used to authenticate as arbitrary users — it only ever signs in the single pre-provisioned demo user whose email/password come from server config (DemoUserOptions), and the endpoint returns NotFound when `Demo:Enabled` is false, so it is gated to demo mode. However, when demo mode is on, `/login/demo` is an AllowAnonymous GET that performs `context.SignInAsync(...)` and establishes a full cookie session for the demo account without the caller presenting any credential (the server holds the password). Because it is a state-changing GET, any cross-site link or `<img>`/navigation silently establishes that session (login CSRF). More importantly, `Demo:Enabled` is true not only in appsettings.Development.json but in the tracked appsettings.Devtest.json — a deployed environment (real Entra tenant, frontend base https://app.appointme.dev) — with the demo password `AppointMe1` committed to source. Anyone reaching that host's /login/demo obtains a working session for the demo user with no authentication.

**Evidence.**
```
builder.MapGet("/login/demo", DemoLogin).AllowAnonymous()...
...
var principal = BuildPrincipal(idToken, demoUser);
await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
```

**Proposed remediation.**
```
Ensure Demo:Enabled is false in every non-local configuration (never in Devtest/Production); do not commit demo passwords; and if the demo path must exist in a shared environment, require it to be a POST protected against CSRF and further gated (e.g. an explicit demo host allowlist).
```

**Verification.** Read DemoLoginEndpoint.cs: line 23 maps GET /login/demo with .AllowAnonymous(), and line 51 calls context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal) — a state-changing GET that establishes a full cookie session with no caller-supplied credential (the demo email/password come from server config DemoOptions, lines 37-42). The endpoint correctly returns NotFound() when Demo:Enabled is false (lines 37-40) and only ever signs in the single configured demo user, so the reviewer's nuance is accurate: it is NOT an arbitrary-user auth bypass. However, appsettings.Devtest.json (lines 28-35) sets Demo:Enabled=true with the committed password "AppointMe1", and that file is a genuine deployed-environment config (real Entra tenant appointmedevtest.ciamlogin.com, Frontend:BaseUrl https://app.appointme.dev, RequireHttpsMetadata:true, Azure Service Bus transport, ForwardedHeaders/reverse-proxy setup in Program.cs). git check-ignore confirms appsettings.Devtest.json is tracked in source control, not gitignored. I searched for mitigations and found none: Program.cs maps endpoints via app.MapEndpoints() with no IsDevelopment/environment gate on the demo endpoint (the only gate is the runtime Demo:Enabled config, which is true in the committed Devtest config); there is no host allowlist and no CSRF/antiforgery on the GET (antiforgery does not apply to GET regardless). Base appsettings.json defaults Demo:Enabled=false, but Devtest overrides it to true. The defect is therefore real end-to-end: on a reachable Devtest host anyone hitting /login/demo obtains a working cookie session for the demo account with no authentication, and a live IdP credential is committed to the repo.

**Verified remediation.**
```
// 1) Set Demo:Enabled=false in every non-local config. appsettings.Devtest.json currently has:
//    "Demo": { "Enabled": true, "User": { "Email": "demo@appointme.dev", "Password": "AppointMe1", ... } }
//    -> set "Demo": { "Enabled": false } and remove the committed password (rotate it in the IdP).
// 2) If a demo path must exist in a shared environment, make it a POST and gate it further, e.g.:
builder.MapPost("/login/demo", DemoLogin)
    .AllowAnonymous()
    .WithName(nameof(DemoLogin))
    .ExcludeFromDescription();
// and in the handler, in addition to demoOptions.Value.Enabled, restrict to an explicit demo-host
// allowlist (e.g. check HttpContext.Request.Host against a configured DemoOptions.AllowedHosts).
// Keep the demo user's password out of source control (user-secrets / env var / key vault).
```

**Notes.** The reviewer's file/line anchor (DemoLoginEndpoint.cs:23) is accurate for the anonymous session-minting code; the "deployed config with committed password" half of the finding is anchored in src/AppointMe.Api/appsettings.Devtest.json lines 28-35 (git-tracked, verified via git check-ignore). Severity is at the lower-Medium end and could be argued Low because: (a) it grants only the single, pre-provisioned demo account, not arbitrary users, which is the likely intended behavior of a "try the demo" flow; (b) Devtest is a test/demo environment, not production; and (c) exploitation requires the host to be internet-reachable AND the demo account to actually exist in the Entra tenant with that exact password and native-auth (public client) enabled — otherwise EntraExternalIdDemoUserAuthenticator returns null and the endpoint yields 502, so it is not unconditionally exploitable. It stays at Medium (not Low) because a live IdP credential is committed to a public repo and anonymous cookie-session minting is enabled in a deployed-intent configuration with no host/CSRF gating. Confirm whether app.appointme.dev is actually internet-reachable and whether the demo account is provisioned there to finalize real-world exploitability.


## B6. [Medium] Auth cookie has no ExpireTimeSpan / absolute lifetime and no re-validation against the IdP token or session

- **Location:** `src/AppointMe.Api/Authentication/AuthenticationExtensions.cs:47`
- **Dimension / category:** auth-session / auth-session
- **Verdict:** CONFIRMED
- **Needs architectural review:** yes

**Explanation.** The cookie handler sets Name/HttpOnly/SameSite/SecurePolicy but no `ExpireTimeSpan`, no `SlidingExpiration`, and no `OnValidatePrincipal` event. It therefore falls back to the framework defaults (14-day ExpireTimeSpan with sliding expiration on). Since only the id_token is stored and the handler never re-checks the id_token `exp` or the IdP session, the browser session is fully decoupled from the identity provider: a user disabled or logged out at Keycloak/Entra remains authenticated in the app until the cookie lapses, a stolen cookie stays valid for up to 14 days and self-renews on each use, and there is no absolute session cap. There is no server-side revocation mechanism.

**Evidence.**
```
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.Name = "appointme.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
})
```

**Proposed remediation.**
```
Set an explicit `options.ExpireTimeSpan` (e.g. hours, not the 14-day default), decide `SlidingExpiration` deliberately, and add a `CookieAuthenticationEvents.OnValidatePrincipal` that revalidates the stored id_token expiry (and ideally refreshes/rejects), so app sessions honor IdP token lifetime and revocation.
```

**Verification.** Read src/AppointMe.Api/Authentication/AuthenticationExtensions.cs (lines 47-53): the AddCookie configuration sets Cookie.Name/HttpOnly/SameSite/SecurePolicy only. Grep across src/AppointMe.Api for ExpireTimeSpan, SlidingExpiration, OnValidatePrincipal, CookieAuthenticationEvents, PostConfigure/Configure<CookieAuthenticationOptions>, and UseTokenLifetime returned no matches beyond this block, so none are configured. Consequences confirmed: (1) ExpireTimeSpan falls back to the ASP.NET Core default of 14 days; (2) SlidingExpiration falls back to the default true (self-renewing); (3) OnValidatePrincipal is absent, so the stored token is never re-checked. OnTokenValidated (lines 87-106) stores ONLY the id_token via StoreTokens (no refresh token), and OpenIdConnectOptions.UseTokenLifetime is not set (default false in ASP.NET Core), so the cookie ticket lifetime is decoupled from the id_token exp. CustomizeOidc for Keycloak only appends kc_idp_hint on redirect (KeycloakAuthenticationExtensions.cs:25-38) and Entra sets no CustomizeOidc, so neither affects cookie lifetime. The DemoLogin path (DemoLoginEndpoint.cs:51) signs in with the same default cookie options. Net effect: a user disabled/logged-out at the IdP keeps a valid app session, there is no absolute session cap, and a captured cookie stays valid up to ~14 days and self-renews. No framework/config mitigation exists elsewhere. Mitigating factors that bound severity: HttpOnly blocks XSS-based cookie theft, SecurePolicy.Always requires TLS, and SameSite=Lax limits cross-site leakage — so exploitation requires conditions (a deprovisioned user, or cookie exfiltration via a non-XSS vector), not an active bypass.

**Verified remediation.**
```
Anchor: src/AppointMe.Api/Authentication/AuthenticationExtensions.cs:47 (the .AddCookie block). Bound the session and revalidate the stored id_token:

.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.Name = "appointme.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

    // Replace the 14-day sliding default with an explicit, bounded lifetime.
    options.ExpireTimeSpan = TimeSpan.FromHours(1);
    options.SlidingExpiration = false; // decide deliberately; false gives an absolute cap

    options.Events.OnValidatePrincipal = async context =>
    {
        var idToken = context.Properties.GetTokenValue(OpenIdConnectParameterNames.IdToken);
        if (idToken is null)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(idToken);
        if (jwt.ValidTo < DateTime.UtcNow)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    };
})

Note: because only the id_token is persisted (no refresh token), OnValidatePrincipal can only reject-on-expiry, tying the app session to the id_token lifetime. To also support silent refresh and true IdP-side revocation, persist the refresh token (or introduce back-channel/remote sign-out) and refresh against the IdP inside this event.
```

**Notes.** Framework defaults verified: CookieAuthenticationOptions.ExpireTimeSpan defaults to 14 days and SlidingExpiration defaults to true; OpenIdConnectOptions.UseTokenLifetime defaults to false in ASP.NET Core, so the ticket is not tied to the token. IsPersistent is not set (default false), so in the browser the cookie is a session cookie — but the encrypted ticket still carries the ~14-day server-side expiry, so a replayed cookie value is accepted server-side up to that window regardless of browser-close behavior. Severity held at Medium (not Immediate/High) because HttpOnly + Secure + SameSite=Lax reduce the theft surface; the concrete residual risks are session-lifetime/revocation gaps requiring specific conditions. The reviewer's file/line (line 47) is precise; no correction needed.


## B7. [Medium] UseForwardedHeaders trusts X-Forwarded-* from any client (KnownProxies/KnownIPNetworks cleared), enabling scheme and client-IP spoofing

- **Location:** `src/AppointMe.Api/Program.cs:50`
- **Dimension / category:** transport-headers / transport
- **Verdict:** CONFIRMED (claimed High → final Medium)
- **Needs architectural review:** yes

**Explanation.** ForwardedHeaders is configured to process X-Forwarded-For and X-Forwarded-Proto while explicitly emptying the trusted-proxy allow-lists with options.KnownIPNetworks.Clear() and options.KnownProxies.Clear(). By default ASP.NET Core only honours forwarded headers from loopback; clearing both lists removes that safety check so the middleware accepts these headers from ANY remote source. app.UseForwardedHeaders() runs unconditionally in every environment (line 63). Concrete impact: an attacker hitting the origin/proxy over plain HTTP can send 'X-Forwarded-Proto: https' — the app then treats Request.IsHttps as true, so UseHttpsRedirection (line 70) skips the redirect AND the auth cookie (CookieSecurePolicy.Always) is emitted over cleartext HTTP, defeating the Secure flag. Spoofed X-Forwarded-For also poisons request logging and any IP-based decisions. This is the canonical reverse-proxy misconfiguration for host/scheme spoofing.

**Evidence.**
```
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
```

**Proposed remediation.**
```
Constrain forwarded headers to the actual ingress. Set options.KnownProxies / options.KnownIPNetworks to the ingress controller / load-balancer IP range (e.g. options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("10.0.0.0"), 8)) for the internal cluster CIDR), keep the default ForwardLimit, and enable UseForwardedHeaders only when actually deployed behind a trusted proxy. If the proxy IP is genuinely unknown in a container platform, rely on the platform's already-authenticated forwarded-header integration (e.g. app.UseForwardedHeaders() gated behind ASPNETCORE_FORWARDEDHEADERS_ENABLED and a network that is not directly reachable), never a cleared allow-list on a publicly reachable listener.
```

**Verification.** The configuration exists exactly as cited. src/AppointMe.Api/Program.cs:50-55 enables ForwardedHeaders.XForwardedFor | XForwardedProto and calls both options.KnownIPNetworks.Clear() and options.KnownProxies.Clear(); app.UseForwardedHeaders() at line 63 runs unconditionally in every environment. In ASP.NET Core's ForwardedHeadersMiddleware the remote-IP check is guarded by checkKnownIps = (KnownNetworks.Count > 0 || KnownProxies.Count > 0); clearing both lists makes this false, so the middleware applies X-Forwarded-For/Proto WITHOUT validating the connecting client — i.e. it trusts these headers from any source. I searched the whole solution for a mitigating KnownProxies/KnownNetworks assignment and found none, so nothing elsewhere neutralizes it. The misconfiguration is real. HOWEVER, the finding's concrete high-impact claim is inaccurate: AuthenticationExtensions.cs:52 sets Cookie.SecurePolicy = CookieSecurePolicy.Always, which emits the Secure attribute unconditionally regardless of Request.IsHttps/X-Forwarded-Proto — spoofing the proto header does NOT strip the Secure flag and does not cause the auth cookie to be sent over cleartext. Additionally, a grep across application code (excluding bin/obj) for RemoteIpAddress, RateLimiter/AddRateLimiter, IsHttps, Request.Scheme, IPAddress, and allowlist/whitelist returned no hits, so no authorization, rate-limiting, or IP-allowlist decision depends on the spoofable client IP or scheme; and Program.cs has no UseHsts(). Therefore the real, surviving impact is limited to (a) attacker-controlled X-Forwarded-For poisoning request logs/OpenTelemetry client-IP, and (b) unreliable scheme detection that can suppress UseHttpsRedirection for the attacker's own request. Both require the origin to be directly reachable by the attacker (bypassing the intended proxy) and neither yields an auth bypass or victim cookie exposure. The defect is genuine but the claimed severity/mechanism is overstated.

**Verified remediation.**
```
// Constrain forwarded headers to the known ingress instead of trusting all clients.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Do NOT clear both allow-lists on a publicly reachable listener. Pin the proxy/ingress:
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    foreach (var cidr in builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
    {
        var parts = cidr.Split('/');
        options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse(parts[0]), int.Parse(parts[1])));
    }
    // options.ForwardLimit stays at its default of 1.
});
// If the platform (Azure Container Apps / App Service / k8s ingress) already provides an
// authenticated forwarded-header integration and the app port is NOT directly reachable,
// leaving the lists cleared is acceptable — but that must be a deliberate, documented decision,
// and the app port must be verified unreachable from untrusted networks.
// Also add app.UseHsts() in non-Development environments to harden against downgrade.
```

**Notes.** Severity downgraded from the claimed High to Medium. The finding's central impact statement — that spoofing X-Forwarded-Proto: https causes the auth cookie to be emitted over cleartext and "defeats the Secure flag" — is incorrect: CookieSecurePolicy.Always (AuthenticationExtensions.cs:52) sets the Secure attribute unconditionally, so it is not affected by the perceived scheme. No IP- or scheme-based security control exists in the codebase for spoofing to bypass, so the practical impact is log/telemetry client-IP poisoning plus self-affecting HTTPS-redirect suppression, both conditional on the origin being directly reachable by the attacker. If the production origin is only reachable through a trusted proxy/ingress (typical for the Aspire/container topology hinted at by the Devtest settings), clearing both lists is the Microsoft-recommended pattern and this drops to Low/informational. The reviewer's file/line (Program.cs:50) is correct; the actual Clear() calls are lines 53-54.


## B8. [Medium] Live Entra External ID demo-account password committed to source for the deployed Devtest environment

- **Location:** `src/AppointMe.Api/appsettings.Devtest.json:33`
- **Dimension / category:** config-secrets / secrets
- **Verdict:** CONFIRMED
- **Needs architectural review:** no

**Explanation.** appsettings.Devtest.json ships to the deployed Devtest tenant and contains a real account credential (demo@appointme.dev / AppointMe1). EntraExternalIdDemoUserAuthenticator (src/AppointMe.Api/Authentication/DemoLogin/EntraExternalIdDemoUserAuthenticator.cs:70-77) submits this exact password to the live Entra native-auth token endpoint, so it is not a placeholder — it is the working password of a real user object in the deployed CIAM tenant. It is committed in cleartext (also duplicated in the local Keycloak realm at src/AppointMe.Aspire/appointme-realm.json:497). Unlike the Entra ClientSecret and connection strings, which this project correctly sources from Key Vault (infra/modules/app-service.bicep:84-101), this credential sits directly in a committed file. It is not covered by the gitleaks allowlist (.gitleaks.toml only allowlists Password1 and the two Keycloak client secrets), but that only matters if the scanner flags it.

**Evidence.**
```
"Email": "demo@appointme.dev",
      "Password": "AppointMe1",
```

**Proposed remediation.**
```
Remove the demo user password from the committed file; if demo mode must exist in a hosted environment, source Demo:User:Password from Key Vault (same pattern as Authentication__EntraExternalId__ClientSecret) and rotate the AppointMe1 credential in the Entra tenant now that it is in git history.
```

**Verification.** The defect is real end-to-end. (1) appsettings.Devtest.json is git-tracked and ships to the deployed Devtest App Service: infra/modules/app-service.bicep sets ASPNETCORE_ENVIRONMENT=Devtest (l.76-77) and its comment (l.59-61) explicitly states non-secret config lives in the committed appsettings.<Env>.json. (2) The value is real, not a placeholder — the committed appsettings.Devtest.example.json uses "<demo-user-password>" while appsettings.Devtest.json substitutes "AppointMe1"; the Authority (appointmedevtest.ciamlogin.com/8e4cba39-.../v2.0) is a live CIAM tenant. (3) EntraExternalIdDemoUserAuthenticator.AuthenticateAsync (l.70-77) POSTs this exact password to the live Entra native-auth token endpoint with grant_type=password, so it is the working password of a real user object. (4) Demo.Enabled=true in Devtest and DemoLoginEndpoint maps /login/demo with .AllowAnonymous(), so it is actively used in the deployed env. No mitigation neutralizes it: the bicep sources ClientSecret/SQL/messaging/DataProtection from Key Vault (l.84-101) but deliberately does NOT source Demo__User__Password, which comes only from the committed file; the .gitleaks.toml allowlist does not include AppointMe1, and an allowlist would not un-leak a live credential anyway. Reviewer's line was slightly off: the password is at appsettings.Devtest.json:32 (line 33 is "Name": "John Doe").

**Verified remediation.**
```
Remove the demo password from the committed file (delete the "Password" line from appsettings.Devtest.json; keep it as a placeholder in the .example file). Source it from Key Vault exactly like the other secrets — add a KV secret (e.g. DemoUserPassword) and a bicep app setting in infra/modules/app-service.bicep:

{
  name: 'Demo__User__Password'
  value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/${demoUserPasswordSecretName}/)'
}

Then rotate the AppointMe1 credential on the demo@appointme.dev user in the Entra External ID (appointmedevtest) tenant, since the old value is now in git history. (The appsettings.Development.json / appointme-realm.json copies are local-Keycloak-only and non-blocking.)
```

**Notes.** Scope caps severity below Immediate/High: the credential authenticates only a single, presumably low-privilege demo user in a dedicated devtest CIAM tenant seeded with demo data — not a production data store or admin/service credential. It is nonetheless a currently-valid, cleartext-committed secret that is live in git history and therefore requires rotation regardless of remediation approach. Line reference corrected from 33 to 32. The Development.json (l.29) and appointme-realm.json (l.497) occurrences are intentional local-only Keycloak dev credentials and are not part of this deployed-environment finding.


## B9. [Medium] SQL server exposed to all Azure traffic via 0.0.0.0 firewall rule plus public network access

- **Location:** `infra/modules/sql.bicep:53`
- **Dimension / category:** config-secrets / network-exposure
- **Verdict:** CONFIRMED
- **Needs architectural review:** yes

**Explanation.** The SQL server sets publicNetworkAccess: 'Enabled' (sql.bicep:34) and adds a firewall rule with startIpAddress/endIpAddress 0.0.0.0-0.0.0.0. The 0.0.0.0 rule is the special 'Allow Azure services and resources to access this server' entry, which permits inbound connections from any resource hosted anywhere in Azure — including other tenants' subscriptions — not just this app. Combined with SQL admin login/password authentication, the server's attack surface is the entire Azure fabric plus any leaked/brute-forced admin credential. The code comment acknowledges this is a devtest convenience, but it is committed infrastructure that provisions the deployed environment.

**Evidence.**
```
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}
```

**Proposed remediation.**
```
For any non-throwaway environment, drop the 0.0.0.0 rule and set publicNetworkAccess: 'Disabled', reaching SQL through a private endpoint + VNet integration (as the README notes for prod), or at minimum scope firewall rules to the App Service outbound IPs and prefer Entra managed-identity auth over a SQL admin password.
```

**Verification.** The code exhibits exactly what the finding describes. infra/modules/sql.bicep line 34 sets `publicNetworkAccess: 'Enabled'` on the `Microsoft.Sql/servers` resource, and lines 53-60 define firewall rule `AllowAllAzureServices` with `startIpAddress: '0.0.0.0'` / `endIpAddress: '0.0.0.0'`. The `0.0.0.0`-`0.0.0.0` entry is the well-documented Azure SQL special rule "Allow Azure services and resources to access this server," which grants network reachability from any Azure-hosted resource, including resources in other subscriptions/tenants — not just this app's App Service. The server uses SQL admin login/password auth (administratorLogin/administratorLoginPassword, lines 31-32), so the network reachability plus a leaked/brute-forced credential is the attack path.\n\nI checked for mitigations that would neutralize this and found none at the code level: in infra/main.bicep the `sql` module (lines 76-86) is deployed UNCONDITIONALLY — there is no `if (environmentName == 'devtest')` guard (contrast the `customDomain` module at line 148 which is gated with `if (!empty(customHostname))`). The `environmentName` param is explicitly documented to accept `devtest, staging, prod` (main.bicep line 3-6), so the same 0.0.0.0 rule and public access ship to whatever environment is deployed. The only mitigations are documentation, not code: README.md line 3/title frame this as "devtest infrastructure" and lines 186-193 list a prod-hardening checklist that explicitly says to "remove Allow Azure services," add private endpoints/VNet integration, and move to Entra-only auth. That checklist is manual and aspirational — nothing enforces it — so it does not neutralize the committed defect. The finding is real end-to-end. The cited anchor (sql.bicep:53, the firewall rule) is accurate; the companion `publicNetworkAccess: 'Enabled'` is at line 34.

**Verified remediation.**
```
// Gate hardening on environment (thread an isProduction/allowPublicNetwork param from main.bicep).\n// 1) Disable public network access for real environments and reach SQL via private endpoint + VNet integration:\nresource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {\n  name: serverName\n  location: location\n  tags: tags\n  properties: {\n    administratorLogin: administratorLogin\n    administratorLoginPassword: administratorLoginPassword\n    version: '12.0'\n    publicNetworkAccess: allowPublicNetwork ? 'Enabled' : 'Disabled'\n    minimalTlsVersion: '1.2'\n  }\n}\n\n// 2) Drop the blanket 0.0.0.0 'Allow Azure services' rule. If public access must stay on for devtest,\n//    at minimum scope firewall rules to the App Service outbound IPs instead of all of Azure:\nresource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = if (allowPublicNetwork) {\n  parent: sqlServer\n  name: 'AllowAppServiceOutbound'\n  properties: {\n    startIpAddress: appServiceOutboundIp\n    endIpAddress: appServiceOutboundIp\n  }\n}\n\n// 3) Prefer Entra managed-identity auth over the SQL admin password (see README prod-hardening checklist):\n//    set an Azure AD admin (Microsoft.Sql/servers/administrators) and enable azureADOnlyAuthentication.
```

**Notes.** Mitigating factors that keep this at Medium rather than High/Immediate: (1) The 0.0.0.0 rule grants network *reachability*, not data access — actually reading data still requires the SQL admin credentials, which are supplied via a @secure() param and stored/read from Key Vault, not committed. So this is a defense-in-depth / attack-surface finding, not a direct auth bypass or data breach. (2) The infra is primarily framed and defaulted as `devtest` (main.bicepparam sets environmentName='devtest'), and a documented prod-hardening checklist exists. Aggravating factor keeping it above Low: the SQL module is deployed with no environment guard, and minimalTlsVersion:'1.2' is the only server-side control, so if this template is used for staging/prod the exposure ships as-is and lets any Azure tenant attempt credential brute-force/spray against the server. Azure SQL has no default login lockout, which makes broad network reachability a meaningful amplifier.


## B10. [Medium] Permission-override cache invalidation does not propagate across application instances

- **Location:** `src/Organizations/AppointMe.Organizations/Settings/Permissions/Infrastructure/RolePermissionOverridesCache.cs:30`
- **Dimension / category:** robustness / robustness
- **Verdict:** CONFIRMED
- **Needs architectural review:** yes

**Explanation.** HybridCache is registered with a bare services.AddHybridCache() (SharedModule.cs:20) — no distributed L2 store and no backplane. The cache entry uses LocalCacheExpiration = 1 hour and is authoritative for authorization: UserPrincipalFactory.Create builds every request's effective permission set from GetAsync. When permissions change, CompanyPermissionsChangedEventHandler calls InvalidateAsync -> cache.RemoveAsync, which only evicts the L1 entry on the node that processed the event. HybridCache has no cross-node L1 invalidation without a backplane, so every OTHER instance keeps serving the stale permission set for up to an hour. In a scaled-out deployment this means revoked permissions remain effective (and newly granted ones remain unavailable) on other nodes for up to 60 minutes — a security-relevant staleness/consistency defect. Even single-instance today, the modular-monolith is designed to scale out, so this is latent.

**Evidence.**
```
public ValueTask InvalidateAsync(CompanyId companyId, CancellationToken cancellationToken = default)
{
    return cache.RemoveAsync(KeyFor(companyId), cancellationToken);
}
```

**Proposed remediation.**
```
Configure a distributed L2 backing store and a backplane for HybridCache (e.g. AddStackExchangeRedisCache + the Redis backplane) so RemoveAsync propagates invalidation to all nodes, or drastically shorten LocalCacheExpiration and rely on a shared L2, or emit the CompanyPermissionsChanged invalidation to all instances via the existing message bus.
```

**Verification.** The defect is real and there is no mitigation elsewhere. (1) HybridCache is registered bare at SharedModule.cs:20 with `services.AddHybridCache()`. A grep across the entire src tree shows this is the only HybridCache registration and there is NO Redis/IDistributedCache/L2/backplane registration anywhere — so HybridCache is L1-only (in-process). (2) RolePermissionOverridesCache uses LocalCacheExpiration = 1h and Expiration = 1h; InvalidateAsync (line 32) calls cache.RemoveAsync, which on an L1-only cache evicts only the processing node's entry. (3) This cache is authoritative for authZ: UserPrincipalFactory.Create (lines 27/33) builds every request's effective permission set from GetAsync and feeds it to PermissionResolver.Resolve. (4) I specifically checked for a messaging-based backplane mitigation and it does not exist: the CompanyPermissionsChanged event is a cascaded Wolverine message handled by CompanyPermissionsChangedEventHandler, and Wolverine is configured with UseDurableLocalQueues() + default transport SqlDurable (node-local durable queue, no external broker). Even the alternate AzureServiceBus path is a competing-consumer queue, not a broadcast — so exactly ONE node processes the invalidation and only that node's L1 is cleared. Therefore, in a multi-instance deployment, all other nodes keep the stale permission set until their local 1h expiry, meaning revoked permissions stay effective (and new grants unavailable) for up to 60 minutes. The one nuance the reviewer already acknowledged: the current Aspire AppHost (Program.cs) registers appointme-api with no .WithReplicas(...), so the deployment is single-instance today and the defect does not manifest in production right now — it is latent, surfacing only on the scale-out the modular monolith is explicitly designed for. Line anchor is slightly imprecise: the RemoveAsync call is line 32 (line 30 is the method signature), and the root-cause fix belongs at SharedModule.cs:20.

**Notes.** Practical severity today is Low because the app runs single-instance (no .WithReplicas in AppHost/Program.cs) — with one node, RemoveAsync clears the only L1 and there is no staleness. It escalates to Medium/High the moment the API is scaled to more than one instance, which the codebase is explicitly designed for. The staleness is bounded and self-healing (max 1h, per LocalCacheExpiration). This is a consistency/robustness issue, not an immediately exploitable auth bypass, so Immediate/High do not apply to the current state. Recommend fixing before any horizontal scale-out. Also note the reviewer's line should be 32 (RemoveAsync) rather than 30, and the actual configuration fix lives in SharedModule.cs:20.


## B12. [Low] RequireHttpsMetadata defaults to false for both OIDC and JWT Bearer, allowing token-signing metadata over HTTP

- **Location:** `src/AppointMe.Api/Authentication/AuthenticationExtensions.cs:32`
- **Dimension / category:** auth-session / transport
- **Verdict:** PLAUSIBLE (claimed Medium → final Low)
- **Needs architectural review:** no

**Explanation.** The single flag `requireHttpsMetadata` is read with a fallback of `false` (line 32) and applied to both the OpenIdConnect handler (`oidc.RequireHttpsMetadata`, line 108) and the JwtBearer handler (`jwt.RequireHttpsMetadata`, line 120). The base appsettings.json ships this as false (line 18), and Development inherits that base. With RequireHttpsMetadata=false the OIDC discovery document and JWKS (the public keys used to verify every id_token / access token signature) may be fetched over plaintext HTTP. An attacker able to MITM that metadata channel can serve a forged JWKS and thereby mint tokens that pass signature validation, defeating the entire token-verification chain. The secure default should be true, relaxed only in Development.

**Evidence.**
```
var requireHttpsMetadata = configuration.GetValue("Authentication:RequireHttpsMetadata", false);
...
oidc.RequireHttpsMetadata = requireHttpsMetadata;
...
jwt.RequireHttpsMetadata = requireHttpsMetadata;
```

**Proposed remediation.**
```
Default to true: `configuration.GetValue("Authentication:RequireHttpsMetadata", true)` and set `"RequireHttpsMetadata": true` in appsettings.json; override to false only in appsettings.Development.json where the local Keycloak may be reached over http.
```

**Verification.** The code facts are all accurate. AuthenticationExtensions.cs:32 reads `Authentication:RequireHttpsMetadata` with an insecure fallback of `false`, and that single value is applied to both the OpenIdConnect handler (line 108) and the JwtBearer handler (line 120). appsettings.json:18 ships `false`, and appsettings.Development.json contains no override, so Development runs with `false`. So the insecure-default code pattern genuinely exists — this is not refuted.

However, the claimed exploitable impact ("token-signing metadata over HTTP" → forged JWKS → auth bypass) does not materialize in any shipping configuration, because it is neutralized by config elsewhere:
1. Deployed environment: infra/main.bicepparam sets environmentName='devtest'; main.bicep:134 maps that to ASPNETCORE_ENVIRONMENT=Devtest; app-service.bicep:76-77 pushes that env var; and appsettings.Devtest.json:14 sets RequireHttpsMetadata=true. (The older compiled artifact infra/main.json:1137-1150 likewise hardcodes ASPNETCORE_ENVIRONMENT=Production plus an explicit Authentication__RequireHttpsMetadata=true env var, which — being an environment variable — would override the base appsettings false regardless.) So the actually-deployed app requires HTTPS metadata.
2. Development: although the flag is false, the Keycloak authority is https://localhost:8082 (appsettings.Development.json:13). RequireHttpsMetadata=false only relaxes the requirement; it does not force an HTTP fetch. Since the authority is HTTPS, discovery and JWKS are still retrieved over HTTPS, so no id_token/access-token signing material ever traverses plaintext HTTP.
3. There is no appsettings.Production.json. The only residual risk is a hypothetical future Production deployment that sets ASPNETCORE_ENVIRONMENT=Production without also supplying the RequireHttpsMetadata=true env var — then the base false would apply. That is a latent secure-default gap, not a live vulnerability in the current codebase/pipeline.

Net: the finding correctly identifies a real insecure-default anti-pattern worth fixing for defense-in-depth, but its Medium "exploitable MITM/forged-JWKS" framing overstates current risk because every actual runtime path is mitigated (deployed=true; dev uses an HTTPS authority). Downgrading to Low. File/line citations (AuthenticationExtensions.cs:32/108/120 and appsettings.json:18) are all precise.

**Verified remediation.**
```
Make the flag secure-by-default so a future Production deployment can't silently inherit an insecure value:

// AuthenticationExtensions.cs:32 — secure default
var requireHttpsMetadata = configuration.GetValue("Authentication:RequireHttpsMetadata", true);

// appsettings.json — flip the shipped base value
"Authentication": { "RequireHttpsMetadata": true, ... }

// appsettings.Development.json — only relax locally if you ever run Keycloak over plain HTTP
"Authentication": { "RequireHttpsMetadata": false }

Devtest/Production already set true (appsettings.Devtest.json:14 and the infra env var), so this change is transparent for them and simply removes the latent risk that a Production run without an explicit override defaults to false.
```

**Notes.** Mitigations that reduce severity: (1) The deployed environment is Devtest (main.bicepparam environmentName='devtest' → ASPNETCORE_ENVIRONMENT=Devtest → appsettings.Devtest.json:14 RequireHttpsMetadata=true); the stale infra/main.json also hardcodes an explicit true env var which would override appsettings anyway. (2) In Development the Keycloak authority is HTTPS (appsettings.Development.json:13), so no token-signing metadata is fetched over plaintext HTTP even with the flag false. The only residual concern is a future Production deployment (no appsettings.Production.json exists) that forgets to set the env var — which is exactly what the secure-default remediation guards against. Treat as a defense-in-depth hardening item, not a live auth-bypass.


## B13. [Low] Identity-provider client secrets and demo password committed to source control

- **Location:** `src/AppointMe.Api/appsettings.Development.json:15`
- **Dimension / category:** auth-session / secrets
- **Verdict:** CONFIRMED (claimed Medium → final Low)
- **Needs architectural review:** no

**Explanation.** appsettings.Development.json (tracked) contains the Keycloak FrontendClientSecret and KeycloakAdmin ClientSecret in cleartext, and both appsettings.Development.json and appsettings.Devtest.json (also tracked) contain the demo user password `AppointMe1`. Devtest additionally references a real Entra tenant and app.appointme.dev. Committed OIDC client secrets and account passwords are a standing credential-leak: the frontend client secret participates in the OIDC code exchange and the ROPC demo grant, so exposure enables token acquisition against those realms/clients.

**Evidence.**
```
"FrontendClientSecret": "d7BP1AYRxH9Ku2yGQ99AzBApfbxyJhVB",
"ApiAudience": "appointme-api"
...
"KeycloakAdmin": { ... "ClientSecret": "X3RcYV9FL3FSDvHh5dBYNURvxdZzawii" }
...
"Demo": { "Enabled": true, "User": { "Email": "demo@appointme.dev", "Password": "AppointMe1", ... } }
```

**Proposed remediation.**
```
Move all secrets to user-secrets / environment variables / a secret store, rotate the exposed Keycloak secrets and demo password, and keep only placeholders (as in the .example file) in tracked configs.
```

**Verification.** The cited files are genuinely tracked (git ls-files confirms both appsettings.Development.json and appsettings.Devtest.json; git check-ignore shows neither is gitignored) and contain cleartext credentials: appsettings.Development.json:15 FrontendClientSecret="d7BP1AYRxH9Ku2yGQ99AzBApfbxyJhVB", :23 KeycloakAdmin.ClientSecret="X3RcYV9FL3FSDvHh5dBYNURvxdZzawii", :29 demo Password="AppointMe1"; appsettings.Devtest.json repeats the demo password. The FrontendClientSecret is really used — KeycloakAuthenticationExtensions.cs:23 maps it to OidcClientSecret, consumed by KeycloakDemoUserAuthenticator.cs in a ROPC grant_type=password token exchange. No framework/config mitigation removes a committed secret from history, so the defect exists. HOWEVER, exploitability is low: the Keycloak Authority is https://localhost:8082 (local dev only), and the identical secret and demo password are already baked into the committed realm import src/AppointMe.Aspire/appointme-realm.json (line 689 secret, line 497 password), i.e. throwaway local-provisioning values with no production reach. The appointme-frontend client is "publicClient": true in the realm, so its "secret" carries no real confidentiality. Devtest.json leaks NO Entra client secret — only ClientId c5892593-... and TenantId 8e4cba39-..., which are public OIDC identifiers by design, not secrets. The reviewer's characterization that Devtest "references a real Entra tenant" is accurate but those are public IDs. The only credential with any external reach is the intentional shared demo account password. Finding is real as a secrets-in-VCS hygiene issue but not remotely exploitable, so severity refined Medium -> Low.

**Verified remediation.**
```
Keep tracked configs pointing at placeholders (as appsettings.Devtest.example.json already does) and load real values from an untracked source. In appsettings.Development.json remove the literal secrets and demo password, then supply them via dotnet user-secrets for local dev (e.g. `dotnet user-secrets set "Authentication:Keycloak:FrontendClientSecret" "<value>"`, `... "KeycloakAdmin:ClientSecret" "<value>"`, `... "Demo:User:Password" "<value>"`) and via environment variables / a secret store in real environments. Note the same values are also committed in src/AppointMe.Aspire/appointme-realm.json (lines 497, 689) — a full fix requires parameterising the realm import too (Aspire config/env substitution) rather than shipping fixed secrets, otherwise removing them from appsettings alone is cosmetic. Because these are localhost dev-only values, rotation is optional; do rotate the demo account password if that account has any privileged/live-demo access.
```

**Notes.** File/line citation is accurate (appsettings.Development.json:15). Additional secret locations for completeness: appsettings.Development.json:23 (KeycloakAdmin.ClientSecret) and :29 (demo password); appsettings.Devtest.json:32 (demo password); and the same values are committed in src/AppointMe.Aspire/appointme-realm.json:497 and :689. Key corrections to the finding: (1) Devtest.json does NOT contain a real Entra client secret — only public ClientId/TenantId/ApiAudience, which are not confidential in OIDC; (2) the Keycloak secrets are localhost-only dev secrets duplicated in the tracked realm import, so 'rotation' of them has little value and they grant no access to any remote system; (3) the frontend client is a public client. The one item worth attention is the demo account password if the live demo at app.appointme.dev grants that user any meaningful write access — but it appears to be a deliberately shared one-click demo login.


## B15. [Low] Cross-tenant isolation relies on the client-supplied X-Company-Id header and is only enforced when a handler happens to inject IPrincipal

- **Location:** `src/AppointMe.Api/Wolverine/HandlerContext/CompanyContextBehavior.cs:8`
- **Dimension / category:** authz-idor / multi-tenancy
- **Verdict:** CONFIRMED (claimed Medium → final Low)
- **Needs architectural review:** yes

**Explanation.** The active tenant is taken verbatim from the client-controlled `X-Company-Id` header (HeaderCompanyDetection) and installed as the ambient CurrentCompany with no membership check at the boundary. All EF global query filters and Dapper queries then scope to that header value. The ONLY place a user is verified to belong to the requested company is UserPrincipalFactory.Create (it looks up the caller's Employee row filtered by the current company), and that runs solely as a side effect of a Wolverine handler injecting IPrincipal (PrincipalContextPolicy). Today every HTTP-reachable handler that touches tenant-scoped data does inject IPrincipal, so this is not currently exploitable — but the control is implicit and unenforced: a future or refactored handler that reads/writes tenant data via a `CompanyId` parameter (CompanyContextBehavior) without also taking `IPrincipal`, or any raw Dapper query, would silently operate on an arbitrary company's data for any authenticated registered user, with no compile-time or test safeguard. The fallback authorization policy (RegisteredUserRequirement) only checks the identity is a UserIdentity, not company membership.

**Evidence.**
```
using (currentCompany.Change(companyId.Value))  // header value trusted; membership never verified here
{
    await next(context);
}
// verification happens only conditionally in UserPrincipalFactory.Create when IPrincipal is resolved
```

**Proposed remediation.**
```
Enforce tenant membership at the boundary rather than as a per-handler side effect: e.g. resolve/verify the principal for the requested company in an authorization requirement or a Wolverine policy applied to every command/query chain (not just those that reference IPrincipal), so that CompanyContextBehavior.Load fails closed unless the caller is a member of the header-specified company. Alternatively make CompanyContextBehavior itself perform the membership check.
```

**Verification.** The finding's technical description is accurate on every point I could verify against the code. (1) Program.cs:29 configures tenant detection as FromHeader("X-Company-Id"); CompanyResolutionMiddleware.InvokeAsync (line 17) takes that header value verbatim via HeaderCompanyDetection.Detect (which just Guid.TryParses the header) and calls currentCompany.Change(companyId.Value) with no membership check. (2) All tenant-scoped reads/writes then key off currentCompany.CompanyId: EF global query filters in OrganizationsDbContext.cs:32-40 (Employee/Invitation/RolePermissionOverride) and Dapper queries in CustomersRepository.cs (WHERE [CompanyId] = @CompanyId). (3) The ONLY membership verification is UserPrincipalFactory.Create (UserPrincipalFactory.cs:14-25): it reads currentCompany.CompanyId, queries Employees for the caller's UserId (scoped to the current company via the global filter), and throws AccessDeniedException if no roles are found. (4) That factory runs only through CurrentPrincipalResolver.Resolve -> PrincipalContextBehavior.LoadAsync, which PrincipalContextPolicy (PrincipalContextPolicy.cs:13) attaches ONLY to chains where chain.Uses<IPrincipal>(). (5) CompanyContextBehavior.Load (CompanyContextBehavior.cs:8-16), attached by CompanyContextPolicy to chains that Uses<CompanyId>(), performs NO membership check — it only reads currentCompany and stamps messageContext.TenantId. (6) The fallback authorization policy (AuthorizationServiceCollectionExtensions.cs:18-21) plus RegisteredUserAuthorizationHandler.cs:14 only assert the identity is a UserIdentity, never company membership. So tenant isolation is genuinely enforced as an implicit side effect of IPrincipal injection, exactly as claimed, and there is no boundary-level or framework mitigation that neutralizes it (the global query filter IS the implicit control being critiqued, not an independent safeguard). No architecture/convention test enforces the IPrincipal convention. I could not refute it. However, I also confirmed the reviewer's own caveat that it is NOT currently exploitable: I enumerated every handler taking CompanyId; every HTTP-reachable command/query handler also injects IPrincipal and calls principal.Require(...). The only two handlers taking CompanyId without IPrincipal (SeedDemoCustomersCommandHandler, SeedDemoAppointmentsCommandHandler) are not HTTP-reachable — they are dispatched solely by DemoSeedingSaga with a trusted TenantId taken from domain-event payloads (DemoSeedingSaga.cs:46,58), and TenantContextBehavior sets the company from that trusted envelope, not the header. No endpoint bypasses the bus to hit tenant data (the only non-bus endpoints are AllowAnonymous login/logout/demo-login). So the defect is a real latent/defense-in-depth weakness, not an active cross-tenant breach.

**Verified remediation.**
```
Fail closed for every tenant-scoped handler regardless of whether it injects IPrincipal, by resolving/verifying the principal inside CompanyContextBehavior.Load (which is attached to all chains that use CompanyId). Resolution runs UserPrincipalFactory.Create only for UserIdentity, so HTTP callers are verified while system/saga flows (SystemIdentity) are unaffected:

public static async Task<CompanyId> Load(
    ICurrentCompany currentCompany,
    ICurrentPrincipalResolver principalResolver,
    IMessageContext messageContext,
    CancellationToken cancellationToken)
{
    var companyId = currentCompany.CompanyId
        ?? throw new AccessDeniedException("Active company was not specified.");

    // Fail closed: verify the caller belongs to the (header-supplied) company,
    // independent of whether the handler happens to inject IPrincipal.
    // For UserIdentity this runs UserPrincipalFactory.Create and throws
    // AccessDeniedException when there is no Employee row in this company.
    await principalResolver.Resolve(cancellationToken);

    messageContext.TenantId = companyId.Value.ToString();
    return companyId;
}

(Update CompanyContextPolicy to add the method as async middleware.) This makes membership an invariant of tenant access rather than a per-handler convention. Cited-line anchor CompanyResolutionMiddleware.cs:17 is where the untrusted header is trusted, but the correct place to enforce the fix is CompanyContextBehavior.Load.
```

**Notes.** Severity lowered from the claimed Medium to Low: verification shows no current exploit path. Every HTTP-reachable tenant-data handler injects IPrincipal (so UserPrincipalFactory.Create runs and rejects non-members of the header company), and no endpoint bypasses the Wolverine bus to touch tenant data. The two handlers that take CompanyId without IPrincipal (SeedDemo*) are saga/event-driven with a trusted TenantId, not header-driven. This is therefore a defense-in-depth / future-proofing gap: it would escalate to High only if a new or refactored HTTP-reachable handler read/wrote tenant data via a CompanyId parameter without also injecting IPrincipal. The risky pattern already exists in-repo (SeedDemo handlers), so a copy-paste into an HTTP slice would silently open cross-tenant access with no compile-time or test guardrail — which is the legitimate basis for the finding. The reviewer's file/line (CompanyResolutionMiddleware.cs:17) correctly identifies where the header is trusted; the enforcement fix belongs in CompanyContextBehavior.Load.


## B18. [Low] No antiforgery/CSRF protection for cookie-authenticated state-changing endpoints; sole defense is SameSite=Lax

- **Location:** `src/AppointMe.Api/Authentication/AuthenticationExtensions.cs:51`
- **Dimension / category:** csrf-verbs / csrf
- **Verdict:** CONFIRMED (claimed Medium → final Low)
- **Needs architectural review:** yes

**Explanation.** The app authenticates browser flows with a cookie ('appointme.auth') via the Hybrid scheme: any request without a Bearer header is authenticated by the cookie (AuthenticationExtensions.cs:43-45). The frontend sends this cookie on every request (src/AppointMe.Frontend/src/lib/axios.ts: withCredentials:true) and attaches no CSRF token — the only custom header is X-Company-Id, which the server never requires. There is NO antiforgery anywhere: Program.cs has no AddAntiforgery/UseAntiforgery and no endpoint calls RequireAntiforgery/ValidateAntiForgeryToken (grep across the solution returns nothing). Every state-changing endpoint is therefore protected against CSRF only by the cookie's SameSite=Lax attribute (line 51). SameSite=Lax blocks classic cross-site form/fetch POST/PUT/DELETE with cookies in modern browsers, but it is a single, browser-dependent control rather than defense-in-depth: it does not cover GET-triggered state changes (see the /logout and /login/demo findings), it historically had the Lax+POST 2-minute exemption, and it fails open on any endpoint later exposed over a safe verb. Affected cookie-auth mutation endpoints include POST/PUT/DELETE /customers, POST /appointments, PUT /appointments/{id}/reschedule, POST /appointments/{id}/cancel, POST /invitations, POST /invitations/{id}/accept|resend, DELETE /invitations/{id}, PUT /employees/{id}/roles, DELETE /employees/{id}, POST /onboarding, PATCH /settings/permissions, and DELETE /settings/permissions/overrides.

**Evidence.**
```
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.Name = "appointme.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
})
```

**Proposed remediation.**
```
Add explicit antiforgery for the cookie-authenticated surface rather than relying solely on SameSite. Register builder.Services.AddAntiforgery(...) and app.UseAntiforgery() in Program.cs, and enforce token validation on cookie-auth state-changing endpoints (e.g. group.AddEndpointFilter for antiforgery / .RequireAntiforgery, issuing the token to the SPA). Optionally tighten the auth cookie to SameSite=Strict where UX allows, and keep SameSite as a secondary layer, not the primary control.
```

**Verification.** The code genuinely exhibits the described gap. AuthenticationExtensions.cs:47-53 registers the browser cookie scheme (`appointme.auth`) with `SameSite=Lax` (line 51), and the Hybrid policy scheme (lines 41-46) authenticates any non-Bearer request via that cookie, so all browser-driven mutations are cookie-authenticated. A solution-wide grep for `Antiforgery|antiforgery|ValidateAntiForgery|RequireAntiforgery|CSRF|Xsrf` returns zero hits, and Program.cs registers neither `AddAntiforgery` nor `UseAntiforgery`. The SPA (`src/AppointMe.Frontend/src/lib/axios.ts`) sends the cookie via `withCredentials: true` and attaches no CSRF token — only `X-Company-Id`, which `CompanyResolutionMiddleware` treats as optional (null → proceeds), so it is not an enforced custom-header defense. Therefore `SameSite=Lax` is genuinely the sole CSRF control. I checked the common false-positive mitigations and none apply: there is no global antiforgery filter, no base-class/endpoint-filter enforcement, and no CORS is configured at all (grep for AddCors/UseCors/AllowCredentials/WithOrigins is empty; the app is a same-origin SPA served via MapFallbackToFile), so there is no permissive credentialed-CORS hole either. The finding is factually accurate. Severity is refined down: SameSite=Lax does block the classic cross-site POST/PUT/DELETE vectors in modern browsers, so the enumerated mutation endpoints are not actively exploitable via ordinary CSRF; the real residual risk is limited to GET-triggered state changes (e.g. the confirmed `GET /login/demo`, which is demo-mode-gated and tracked as a separate finding), the narrow historical Lax+POST 2-minute window, and fail-open on any future safe-verb endpoint. This is a legitimate defense-in-depth/hardening gap rather than an active auth bypass or data-breach vector.

**Verified remediation.**
```
Keep SameSite as a secondary layer and add explicit antiforgery for the cookie-authenticated surface. In Program.cs:

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = "appointme.xsrf";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
...
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery(); // after auth, before endpoints

Enforce validation only on cookie-authenticated state-changing endpoints. Since the Hybrid scheme means Bearer (API/mobile) callers should be exempt, gate an endpoint filter on the absence of a Bearer header, e.g. a shared filter applied to mutation endpoint groups:

group.AddEndpointFilter(async (ctx, next) =>
{
    var http = ctx.HttpContext;
    if (!http.Request.HasBearerTokenHeader())
        await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http);
    return await next(ctx);
});

Issue the token to the SPA (e.g. a `GET /antiforgery/token` that calls `IAntiforgery.GetAndStoreTokens` and returns the request token) and have axios send it back in `X-XSRF-TOKEN`. Optionally tighten the auth cookie to SameSite=Strict where UX allows. Note: because there is no CORS policy and SameSite=Lax already blocks classic cross-site POST/PUT/DELETE, this is hardening/defense-in-depth; prioritize it alongside converting the GET /login/demo and logout state-changing endpoints to POST.
```

**Notes.** Severity downgraded from the claimed Medium to Low: SameSite=Lax + no CORS is an accepted (if not best-practice) CSRF posture for a same-origin SPA, so the enumerated POST/PUT/DELETE endpoints are not actively exploitable. The finding's own framing (defense-in-depth, SameSite as the mitigating control) is correct. The concrete residual exploit path (GET /login/demo performing sign-in) is demo-mode-gated via AddDemoMode(configuration) and is covered by a separate finding, so it should not be double-counted here. Line 51 is the exact SameSite=Lax line; the cookie block spans lines 47-53.


## B20. [Low] Demo login establishes an authenticated session over MapGet with no CSRF protection (login CSRF)

- **Location:** `src/AppointMe.Api/Authentication/DemoLogin/DemoLoginEndpoint.cs:23`
- **Dimension / category:** csrf-verbs / csrf
- **Verdict:** CONFIRMED
- **Needs architectural review:** no

**Explanation.** GET /api/v1/login/demo is AllowAnonymous and performs a state-changing SignInAsync that mints an authenticated cookie principal for the shared demo user, then redirects to the app. Being a GET with no antiforgery, an attacker page can silently force a victim's browser to this URL and log the victim into the attacker-controlled/shared demo account (login CSRF / session fixation), so the victim's subsequent actions and any data they enter are attributed to the demo identity. Severity is limited because the endpoint is gated behind DemoOptions.Enabled, which is false in appsettings.json but true in Development/Devtest profiles — so the exposure is real in any environment where demo mode is on.

**Evidence.**
```
builder.MapGet("/login/demo", DemoLogin).AllowAnonymous()...
...
var principal = BuildPrincipal(idToken, demoUser);
await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
return TypedResults.Redirect(frontendOptions.Value.BaseUrl.ToString());
```

**Proposed remediation.**
```
Perform the demo sign-in over MapPost with antiforgery validation instead of a bare GET, and ensure demo mode stays disabled in any internet-reachable environment. A safe-verb, tokenless sign-in should not create a session.
```

**Verification.** The code exhibits the defect exactly as described. DemoLoginEndpoint.cs line 23 registers `builder.MapGet("/login/demo", DemoLogin).AllowAnonymous()`, and the handler at line 51 calls `context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal)` — a state-changing operation that mints an authenticated cookie session — over a safe GET verb with no antiforgery. I searched the entire src tree: `Antiforgery` appears only in obj/ build artifacts (transitive package), never in source. Program.cs's middleware pipeline (lines 63-83) has no `app.UseAntiforgery()`, and the endpoint has no token/POST requirement. The handler reads demo credentials from server config and takes no request input, so a bare cross-site GET fully triggers the sign-in.\n\nI specifically examined the SameSite=Lax cookie setting (AuthenticationExtensions.cs line 51) as a possible framework mitigation and rejected it: SameSite governs whether cookies are attached to *outgoing* cross-site requests (protecting against classic CSRF on an existing session), not whether a `Set-Cookie` response is honored. Login CSRF establishes a *new* session from the server's Set-Cookie response and requires no pre-existing credential, so SameSite=Lax provides no protection against this specific attack. HttpOnly/Secure are likewise irrelevant to login CSRF.\n\nGating: `Demo.Enabled` is false in base appsettings.json (production is safe), but true in appsettings.Development.json and appsettings.Devtest.json; Devtest's Frontend.BaseUrl is https://app.appointme.dev, an internet-reachable environment, so the exposure is real where demo mode is on. No mitigation neutralizes the finding.

**Verified remediation.**
```
Do not establish a session over a safe verb. Convert the demo sign-in to POST with antiforgery validation and register the middleware (currently absent):\n\n// DemoLoginEndpoint.cs\nbuilder.MapPost("/login/demo", DemoLogin)\n    .AllowAnonymous()\n    .WithName(nameof(DemoLogin))\n    .ExcludeFromDescription();\n\n// Program.cs — add to the service registration and the pipeline\nbuilder.Services.AddAntiforgery();\n...\napp.UseAuthentication();\napp.UseAuthorization();\napp.UseAntiforgery();   // must run before MapEndpoints\n\n// Validate the token inside the handler (frontend must send it, e.g. header X-CSRF-TOKEN)\nawait antiforgery.ValidateRequestAsync(context);   // IAntiforgery injected\n\nSeparately, keep demo mode disabled in every internet-reachable environment: appsettings.Devtest.json currently sets Demo.Enabled=true while Frontend.BaseUrl points at https://app.appointme.dev — set it to false there (or gate behind an additional trusted-network/auth control) so the shared demo identity cannot be force-established remotely.
```

**Notes.** Severity remains Low, matching the reviewer. Production is not exposed (Demo.Enabled=false in base appsettings.json). The impacted account is a shared public demo sandbox rather than a real per-user account, which limits confidentiality/integrity impact; the primary concern is that a victim can be silently signed into the shared demo identity in demo-enabled internet-reachable environments (Devtest → app.appointme.dev), so any data they enter is attributed to / readable within the shared demo account. The line-23 anchor (MapGet + AllowAnonymous) is the correct root-cause location; the actual state change is line 51 (SignInAsync).


## B24. [Low] AllowedHosts wildcard disables host-header filtering

- **Location:** `src/AppointMe.Api/appsettings.json:8`
- **Dimension / category:** transport-headers / transport
- **Verdict:** PLAUSIBLE
- **Needs architectural review:** no

**Explanation.** AllowedHosts is "*", so Host header filtering is disabled for all environments (no environment file overrides it). Combined with the unrestricted forwarded-headers processing above, the app will accept and act on arbitrary Host/X-Forwarded-Host values, which facilitates host-header injection into any absolute URLs the app generates (password/redirect links, cache keys) and web-cache poisoning. Lower severity because current redirects use the configured Frontend:BaseUrl rather than the request host, but the missing control is worth tightening.

**Evidence.**
```
"AllowedHosts": "*",
```

**Proposed remediation.**
```
Set AllowedHosts to the concrete production hostname(s) (e.g. "app.appointme.dev") in the production/Devtest appsettings, keeping "*" only in Development.
```

**Verification.** The literal config claim is accurate: appsettings.json:8 sets "AllowedHosts": "*", and none of appsettings.Development/Devtest/Devtest.example/Codegen override it, so host-header filtering is effectively disabled in every environment. However, the finding's stated impact mechanism is not reachable in this codebase. (1) Program.cs:52 enables only ForwardedHeaders.XForwardedFor | XForwardedProto — XForwardedHost is NOT processed, so the request Host cannot be spoofed via X-Forwarded-Host as the explanation claims. (2) No absolute URL is built from the request Host: LoginEndpoint.cs:20, LogoutEndpoint.cs:21, DemoLoginEndpoint.cs:53, and all invitation emails (FrontendOptions.InvitationUrl, used by SignupCommandHandler/EmployeeInvitedHandler/EmployeeInvitationResentHandler) all derive from the configured Frontend:BaseUrl. LoginEndpoint additionally restricts returnUrl to relative URIs. Grep found no Request.Host, GetDisplayUrl, LinkGenerator, or host-based cache keys anywhere. Password-reset/verification links are generated by Keycloak/Entra, not this pipeline. So the permissive config is real (a defense-in-depth gap worth tightening) but there is no demonstrable host-header-injection exploit, and the finding's forwarded-host rationale is factually wrong. This is a Low hardening nit, not a confirmed vulnerability.

**Verified remediation.**
```
Pin AllowedHosts to concrete hostnames in the deployed environments and keep the wildcard only in local dev. In appsettings.Devtest.json (and any future production config) add:

  "AllowedHosts": "app.appointme.dev"

(use a semicolon-separated list for multiple hosts, e.g. "app.appointme.dev;www.appointme.dev"). Leaving "*" in appsettings.json is acceptable as the dev default since appsettings.Development.json does not override it. Note: this is pure hardening — no request-host-derived URL or cache key exists today, and X-Forwarded-Host is not enabled in ForwardedHeadersOptions (Program.cs:52), so there is no active injection path to close.
```

**Notes.** The finding's severity (Low) and its own caveat that redirects use Frontend:BaseUrl are correct, but the explanation's core mechanism is inaccurate: it asserts the forwarded-headers block accepts arbitrary X-Forwarded-Host values, whereas Program.cs enables only XForwardedFor and XForwardedProto. The separate, more material transport issue nearby is KnownProxies.Clear()/KnownIPNetworks.Clear() (Program.cs:53-54), which trusts X-Forwarded-For/Proto from any caller — that belongs to a different finding, not this one. If the deployment sits behind a reverse proxy that already enforces Host, the practical value of tightening AllowedHosts is defense-in-depth only.


## B25. [Low] RequireHttpsMetadata defaults to false in the base appsettings, so a hosted environment that omits the override silently disables OIDC metadata TLS enforcement

- **Location:** `src/AppointMe.Api/appsettings.json:18`
- **Dimension / category:** config-secrets / transport
- **Verdict:** CONFIRMED (claimed Medium → final Low)
- **Needs architectural review:** no

**Explanation.** The base appsettings.json — which applies to every environment unless overridden — sets Authentication.RequireHttpsMetadata=false. This is an insecure-by-default posture: RequireHttpsMetadata=false lets the OIDC/JWT middleware fetch the authority's discovery document and JWKS signing keys over plain HTTP, opening a MITM path to substitute token-signing keys and forge access tokens. It works today only because appsettings.Devtest.json:14 explicitly overrides it to true, but a new hosted environment (e.g. a future appsettings.Production.json) that forgets this single line inherits the insecure default with no error. Safe defaults should fail closed.

**Evidence.**
```
"Authentication": {
    "Provider": "Keycloak",
    "RequireHttpsMetadata": false,
```

**Proposed remediation.**
```
Flip the base default to true in appsettings.json and override to false only in appsettings.Development.json (where localhost Keycloak on http is expected). That way any hosted environment is secure unless it deliberately opts out.
```

**Verification.** The defect exists as described. appsettings.json:18 sets "RequireHttpsMetadata": false in the base config that every environment inherits unless overridden, and AuthenticationExtensions.cs:32 reads it with configuration.GetValue("Authentication:RequireHttpsMetadata", false) — so the insecure value is the default at BOTH the config and the code layer. That flag is then applied to oidc.RequireHttpsMetadata (line 108) and jwt.RequireHttpsMetadata (line 120), meaning the OIDC discovery document and JWKS signing keys can be fetched over plain HTTP. ASP.NET Core's own framework default for RequireHttpsMetadata is true, so this is a deliberate fail-open weakening. Only appsettings.Devtest.json:14 (and .example) restore true; appsettings.Development.json does not override it and inherits false; no appsettings.Production.json exists. I checked for mitigations and found none that neutralize it: Program.cs has no environment-based fail-closed logic, and app.UseHttpsRedirection()/UseForwardedHeaders() only affect inbound request transport, not the middleware's outbound metadata fetch. HOWEVER, there is no currently exploitable path — all configured environments use https:// authorities and the only hosted-style profile (Devtest) sets true — so the risk is latent (a future hosted environment omitting the override plus an http authority plus a MITM position). This warrants Low, not Medium. I also note the proposed remediation is incomplete: flipping only the base config leaves the false fallback on line 32, so deleting the key still fails open; the code default must be flipped too.

**Verified remediation.**
```
Fail closed at both layers. In appsettings.json set the base default to true (or remove the key and rely on a secure code default), and move the false only into appsettings.Development.json where localhost is expected:

// appsettings.json
"Authentication": {
  "Provider": "Keycloak",
  "RequireHttpsMetadata": true,
  ...
}

// appsettings.Development.json
"Authentication": {
  "RequireHttpsMetadata": false,
  ...
}

Critically, also flip the code fallback in AuthenticationExtensions.cs:32 so a missing key does not silently fail open:
var requireHttpsMetadata = configuration.GetValue("Authentication:RequireHttpsMetadata", true);
```

**Notes.** The reviewer cited only appsettings.json:18, but the insecure default is also present in code at src/AppointMe.Api/Authentication/AuthenticationExtensions.cs:32 (default value false in the GetValue call). Any remediation that fixes only the config file is incomplete because removing the config key still yields false. No current exploitation: Development, Devtest, and even the Keycloak dev authority use https:// (https://localhost:8082), and Devtest explicitly sets true — the risk is purely a future hosted environment being created without the override. That latent, multi-condition nature is why I downgraded from the claimed Medium to Low. The flag governs outbound OIDC metadata/JWKS TLS enforcement; app-level UseHttpsRedirection does not mitigate it.


## B26. [Low] Key Vault has purge protection disabled and public network access enabled

- **Location:** `infra/modules/key-vault.bicep:27`
- **Dimension / category:** config-secrets / infra-hardening
- **Verdict:** CONFIRMED
- **Needs architectural review:** no

**Explanation.** The vault that holds all production-like secrets (SQL/messaging/data-protection connection strings and the Entra client secret) is provisioned with enablePurgeProtection: null (i.e. off) and publicNetworkAccess: 'Enabled'. Without purge protection a soft-deleted vault or secret can be permanently purged within the retention window, which removes the tamper/ransom safety net; public network access leaves the vault reachable from the internet (still gated by AAD RBAC, so exposure is limited, but it is not network-isolated).

**Evidence.**
```
enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enablePurgeProtection: null
    publicNetworkAccess: 'Enabled'
```

**Proposed remediation.**
```
Set enablePurgeProtection: true for any environment that stores real secrets, and consider publicNetworkAccess: 'Disabled' with a private endpoint / trusted-services bypass for production.
```

**Verification.** Read infra/modules/key-vault.bicep directly: line 27 is `enablePurgeProtection: null` and line 28 is `publicNetworkAccess: 'Enabled'`, exactly as reported. In Bicep, `null` leaves purge protection unset, and Key Vault defaults purge protection to OFF — so soft-deleted vaults/secrets can be permanently purged within the 7-day soft-delete window (softDeleteRetentionInDays: 7). Traced infra/main.bicep:67-74: the module is instantiated unconditionally with no per-environment override for these two properties, so the weak posture applies to prod as well as devtest (environmentName is a free @maxLength(10) param, and main.bicepparam currently sets 'devtest'). Confirmed the vault holds real secrets via infra/modules/app-service.bicep:84-101, which builds @Microsoft.KeyVault SecretUri references for EntraExternalIdClientSecret, the SQL connection string, the messaging connection string, and the DataProtectionStorage connection string. Checked for mitigations: enableRbacAuthorization: true gates network access via AAD RBAC (partial mitigation for the public-access concern, acknowledged in the finding) and enableSoftDelete: true allows recovery — but neither restores purge protection nor network isolation. No other module, param, or config file overrides these vault properties. The defect is real end-to-end.

**Verified remediation.**
```
Enable purge protection for any environment that stores real secrets and tighten network exposure. Minimal change:

  enableRbacAuthorization: true
  enableSoftDelete: true
  softDeleteRetentionInDays: 7
  enablePurgeProtection: true

Note: purge protection is irreversible once set, so if dev/throwaway vaults must stay purgeable, gate it on environment instead of enabling globally, e.g. add a param and:

  enablePurgeProtection: environmentName == 'devtest' ? null : true

For network isolation (defense-in-depth, optional for prod), set publicNetworkAccess: 'Disabled' with a private endpoint plus a trusted-Azure-services bypass:

  publicNetworkAccess: 'Disabled'
  networkAcls: {
    bypass: 'AzureServices'
    defaultAction: 'Deny'
  }

This requires the App Service to reach the vault over a private endpoint / VNet integration (not currently provisioned), so it is a larger change than the purge-protection flag and can be deferred.
```

**Notes.** Severity Low is correct: exposure is bounded because enableRbacAuthorization: true means the public endpoint still requires AAD credentials, and soft delete allows recovery unless actively purged. This is an infra-hardening / defense-in-depth gap, not an exploitable vulnerability or data-exposure path. The purge-protection fix is a one-line, low-risk change and is the higher-value part; the network-isolation change is heavier (needs private endpoint / VNet integration that the current infra lacks) and reasonably deferred. Current deployed param set targets 'devtest' only, but because the module hard-codes these values with no environment gating, a future prod deployment would silently inherit the weak posture — which is the core of the finding.


## B27. [Low] Hardcoded local-development credentials committed (SQL sa password, Keycloak admin, Keycloak client secrets)

- **Location:** `src/AppointMe.Api/appsettings.Development.json:9`
- **Dimension / category:** config-secrets / secrets
- **Verdict:** CONFIRMED
- **Needs architectural review:** no

**Explanation.** appsettings.Development.json commits the SQL sa password (Password1, line 9), the Keycloak frontend client secret (d7BP1AYRxH9Ku2yGQ99AzBApfbxyJhVB, line 15) and the Keycloak admin API client secret (X3RcYV9FL3FSDvHh5dBYNURvxdZzawii, line 23); the same SQL password appears in compose.yaml:30 and src/AppointMe.Aspire/Program.cs:5, the admin/admin Keycloak bootstrap creds in Program.cs:18-19 and compose.yaml:47-48, and the API client secret in appointme-realm.json:689. These are genuinely local-development-only: the Keycloak realm they authenticate against is spun up locally from the committed realm file, hosted environments use Entra External ID instead, and .gitleaks.toml explicitly allowlists these values by content (not by file, so a new real secret in these files is still caught). The Keycloak dev TLS private key is correctly git-ignored (docker/keycloak/certs/*.key) and not committed. Risk is therefore low, but the credentials are weak and permanent, so they must never be promoted to or reused by any hosted environment.

**Evidence.**
```
"FrontendClientSecret": "d7BP1AYRxH9Ku2yGQ99AzBApfbxyJhVB",
      ...
  "KeycloakAdmin": {
    "ClientSecret": "X3RcYV9FL3FSDvHh5dBYNURvxdZzawii"
  }
```

**Proposed remediation.**
```
Acceptable to keep for local dev given the gitleaks allowlist and Entra-only hosted auth, but document clearly that these secrets are local-only and ensure no hosted Keycloak/SQL ever imports this realm file or reuses Password1; prefer .NET user-secrets for anything a developer might otherwise be tempted to point at a shared instance.
```

**Verification.** All claims verified against the code. src/AppointMe.Api/appsettings.Development.json is git-tracked and commits three credentials: SQL sa password "Password1" (line 9), Keycloak FrontendClientSecret "d7BP1AYRxH9Ku2yGQ99AzBApfbxyJhVB" (line 15), and KeycloakAdmin ClientSecret "X3RcYV9FL3FSDvHh5dBYNURvxdZzawii" (line 23). The SQL password is echoed in compose.yaml:30 and src/AppointMe.Aspire/Program.cs:5; admin/admin Keycloak bootstrap creds in Program.cs:18-19 and compose.yaml:47-48; the API client secret is at src/AppointMe.Aspire/appointme-realm.json:689 ("secret": "X3RcYV9FL3FSDvHh5dBYNURvxdZzawii"). The defect is real: secrets are committed to the repo. However, every mitigation the finding cites is genuine and neutralizes real-world risk: (1) .gitleaks.toml allowlists these by value (regexTarget = "match" with literal-value regexes), not by file, so a new real secret added to these files is still caught; (2) hosted config (appsettings.Devtest.json) uses Provider=EntraExternalId with RequireHttpsMetadata=true and contains no Keycloak client secret or SQL password, so the committed Keycloak realm/secrets are never used in a hosted environment; (3) the Keycloak dev TLS private key is git-ignored (.gitignore:382, docker/keycloak/certs/*.key) with only .gitkeep tracked. These credentials authenticate only against containers on a developer's local machine (localhost SQL, localhost Keycloak realm imported from the committed file). Not exploitable against any hosted or network-reachable system — no auth bypass, no data breach. This is an accurate, self-aware, correctly-rated Low-severity secrets/hygiene finding, not a false positive.

**Verified remediation.**
```
Acceptable to keep for local dev: the values only authenticate against local containers, the hosted environment uses Entra External ID (appsettings.Devtest.json), and .gitleaks.toml allowlists them by value so new real secrets are still flagged. Hardening/hygiene actions: (1) Add a one-line comment at the top of appsettings.Development.json stating these are local-only throwaway credentials that must never be reused by or promoted to any hosted environment (mirroring the existing .gitleaks.toml header). (2) Guarantee no hosted Keycloak/SQL ever imports appointme-realm.json or reuses "Password1" — keep hosted secrets in Azure Key Vault only. (3) For anything a developer might otherwise point at a shared instance, prefer `dotnet user-secrets` (secrets.json outside the repo) over committing to appsettings.Development.json. Note: the finding anchored line 15 (FrontendClientSecret); the same file also commits the SQL sa password at line 9 and the Keycloak admin ClientSecret at line 23 — treat all three together.
```

**Notes.** Repo path is public/appointme, so this is a public-facing repository, which is why committing even local-only creds warrants a finding rather than being ignored. Severity stays Low because: the secrets are not valid against any hosted/network-reachable system (Keycloak realm is imported locally; SQL is localhost-only); hosted auth is Entra External ID; the dev TLS private key is correctly git-ignored; and gitleaks value-based allowlisting means the controls degrade safely (a genuinely new secret is still caught). No framework mitigation makes this a false positive — the credentials are genuinely committed — but the surrounding controls cap real-world impact at Low. Reviewer's severity, category, and remediation stance are all correct; the only imprecision is the anchor line (multiple secrets across lines 9/15/23; strongest single anchor is the SQL password at line 9).


## B28. [Low] No Content-Security-Policy defined; app contains a raw HTML injection sink (dangerouslySetInnerHTML)

- **Location:** `src/AppointMe.Frontend/index.html:3`
- **Dimension / category:** frontend-security / transport
- **Verdict:** CONFIRMED
- **Needs architectural review:** yes

**Explanation.** index.html declares no Content-Security-Policy (no meta CSP, and none is set here for the response header). The bundle also ships a raw-HTML sink in the vendored shadcn chart component (components/ui/chart.tsx:73 uses dangerouslySetInnerHTML to build a <style> block). Today that sink is fed only developer-authored ChartConfig color/theme values, so it is not currently exploitable, but with no CSP there is no defense-in-depth backstop if any injection sink (this one, or a future one) is ever fed untrusted data. A CSP restricting script-src/style-src/connect-src would contain the blast radius.

**Evidence.**
```
<head>
    <meta charset="UTF-8"/>
    <meta content="width=device-width, initial-scale=1.0" name="viewport"/>
    <link rel="icon" type="image/svg+xml" href="/src/assets/logo.svg"/>
    <title>AppointMe</title>
</head>
```

**Proposed remediation.**
```
Serve a Content-Security-Policy via response headers on the SPA host (preferred over a meta tag) restricting default-src to 'self', constraining connect-src to the API origin, and avoiding 'unsafe-inline' for scripts. Because chart.tsx injects a <style> via dangerouslySetInnerHTML, either allow style-src 'unsafe-inline' narrowly or refactor that CSS to nonce-based/CSS-variables so a strict style-src can be used.
```

**Verification.** All factual claims verified against the code. index.html (lines 3-8) declares no CSP meta tag. The real SPA host is the ASP.NET Core API — Program.cs:72 `app.UseStaticFiles()` and Program.cs:83 `app.MapFallbackToFile("index.html")` serve the built SPA — and Program.cs contains NO response-header middleware at all (no `app.Use(...)` emitting Content-Security-Policy, X-Frame-Options, or X-Content-Type-Options). No nginx/Docker/reverse-proxy config exists in the repo to inject a CSP either. The only `browserSecurityHeaders` hit is in src/AppointMe.Aspire/appointme-realm.json:1724, which is Keycloak's realm config for its own login pages, not the SPA host. So the absence of CSP is genuine and unmitigated. The raw-HTML sink is real: chart.tsx:73 uses `dangerouslySetInnerHTML` to build a `<style>` block, and it is the only such sink in the frontend. The finding is accurate; it is honestly scoped as a Low, defense-in-depth item that is not exploitable today. Two facts make it even less exploitable than the reviewer states: (1) the sink is fed only developer-authored ChartConfig color/theme strings, and (2) the chart component is dead code — `ChartContainer`/`ChartConfig` are not imported anywhere under src/app, so the sink is not reachable in any rendered path. HTTPS is already enforced via UseHttpsRedirection (Program.cs:70), so the claimed 'transport' category is mislabeled — the true gap is a missing content-injection defense-in-depth header, not a transport weakness.

**Verified remediation.**
```
Add a Content-Security-Policy response header at the true SPA host, the ASP.NET Core pipeline in src/AppointMe.Api/Program.cs (a response header is preferred over a meta tag). Insert header middleware before UseStaticFiles/MapFallbackToFile, e.g.:

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; " +
        "connect-src 'self'; " +           // API is same-origin (served by this host)
        "img-src 'self' data:; " +          // logo.svg + any data: URIs
        "style-src 'self' 'unsafe-inline'; " + // required: chart.tsx injects a <style> block, and Tailwind/Radix use inline styles
        "script-src 'self'; " +
        "object-src 'none'; base-uri 'self'; frame-ancestors 'none'");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    await next();
});
app.UseStaticFiles();

If you prefer to avoid 'unsafe-inline' for style-src, first refactor chart.tsx (ChartStyle) to set the per-chart CSS custom properties via a nonce-tagged <style> or inline `style` custom properties instead of dangerouslySetInnerHTML, then tighten style-src to a nonce. Given the sink is currently unused dead code, deleting components/ui/chart.tsx (or leaving it until a chart feature is actually added) removes the sink entirely and is the lowest-effort option.
```

**Notes.** Corrected anchor/category: the reviewer anchored to index.html:3 and categorized it as 'transport'. The absence is real there, but the actual fix location is src/AppointMe.Api/Program.cs (before line 72 UseStaticFiles / line 83 MapFallbackToFile), since the API — not a separate SPA host — serves index.html in production. Category is better described as content-security / defense-in-depth than transport; HTTPS transport is already enforced by UseHttpsRedirection (Program.cs:70). Severity Low is appropriate and arguably generous: the sink is unreachable dead code (ChartContainer/ChartConfig not imported anywhere under src/app) and is fed only developer-authored constants, so there is no current exploit path — this is purely a hardening recommendation.


## B29. [Low] useCurrentUser context guard is dead code — silently returns anonymous default outside a provider

- **Location:** `src/AppointMe.Frontend/src/components/auth/current-user-context.tsx:5`
- **Dimension / category:** frontend-security / robustness
- **Verdict:** CONFIRMED
- **Needs architectural review:** no

**Explanation.** CurrentUserContext is created with a non-null default value ({ isAuthenticated: false }) at line 5-7, but useCurrentUser guards with `if (context === null) throw ...`. Because the default is never null, that guard can never fire. A component that calls useCurrentUser outside CurrentUserProvider will silently receive the anonymous default ({ isAuthenticated: false }) instead of throwing, which can mask a provider-wiring mistake and cause auth-dependent UI/routing (e.g. useUserState in app-shell.tsx) to render as anonymous rather than surfacing the bug. Unlike this file, the sibling contexts (current-company, user-access) correctly default to null.

**Evidence.**
```
const CurrentUserContext = createContext<GetCurrentUserResponse>({
    isAuthenticated: false,
});
...
export const useCurrentUser = () => {
    const context = use(CurrentUserContext);
    if (context === null) {
        throw new Error('useCurrentUser must be used within a CurrentUserProvider');
    }
    return context;
};
```

**Proposed remediation.**
```
Default the context to null (createContext<GetCurrentUserResponse | null>(null)) like the other auth contexts so the misuse guard in useCurrentUser can actually throw, or drop the dead null-check to make the anonymous-default behavior intentional and explicit.
```

**Verification.** Read src/AppointMe.Frontend/src/components/auth/current-user-context.tsx in full. Line 5 creates the context as createContext<GetCurrentUserResponse>({ isAuthenticated: false }) — the type parameter is the non-nullable GetCurrentUserResponse (verified in appointme.schemas.ts:77-91, isAuthenticated is a required boolean, no null in the interface itself) and the default value is a non-null object. CurrentUserProvider (lines 9-16) returns null when !data, so it never renders CurrentUserContext with a null value; the provided value is always non-null. Therefore the guard `if (context === null)` at line 20 is unreachable dead code — it can never throw. A component calling useCurrentUser outside the provider silently receives { isAuthenticated: false } instead of an error. This is confirmed to differ from the sibling contexts, which are correct: current-company-context.tsx:12 uses createContext<CurrentCompanyContextValue | null>(null) and user-access-context.tsx:5 uses createContext<GetCurrentUserAccessResponse | null>(null), so their identical null-guards actually fire. No mitigation elsewhere neutralizes this — it is a pure client-side value/type defect determined entirely by this file. Severity is Low and this is robustness, not security: the anonymous default is fail-closed. In app-shell.tsx:8-24 useUserState maps !isAuthenticated to 'anonymous', which StateRouter redirects to /auth/login — i.e. denies access rather than granting it, so there is no auth-bypass. The only harm is masking a provider-wiring bug as a spurious login redirect.

**Verified remediation.**
```
Make the context nullable so the guard can fire, matching the sibling contexts:

const CurrentUserContext = createContext<GetCurrentUserResponse | null>(null);

// useCurrentUser's existing `if (context === null) throw ...` (line 20) then
// correctly surfaces misuse outside CurrentUserProvider.

Alternatively, if the anonymous default is intentional, delete the dead null-check (lines 20-22) and return context directly to make that behavior explicit.
```

**Notes.** Root cause is the non-null default + non-nullable type parameter on line 5; the dead branch itself is line 20 (the reviewer's cited line). This is a robustness/code-quality issue, not a security vulnerability: the fail-closed anonymous default means a misuse causes a redirect to /auth/login, never an auth bypass. Provider wiring in app-shell.tsx (AppShell wraps Outlet in CurrentUserProvider) means all current call sites (nav-user.tsx, company-selector.tsx, app-shell.tsx, current-company-context.tsx) are inside the provider today, so the dead guard has no runtime effect in practice — it only fails to protect future misuse.


## B31. [Low] SqlConnection leaked when OpenAsync throws (cancellation or connection failure)

- **Location:** `src/AppointMe.Shared/Database/SqlConnectionFactory.cs:10`
- **Dimension / category:** robustness / robustness
- **Verdict:** CONFIRMED
- **Needs architectural review:** no

**Explanation.** The SqlConnection is constructed and OpenAsync is awaited before the instance is returned to the caller's `using`. If OpenAsync throws — e.g. the passed CancellationToken is cancelled mid-open, or a transient network/login failure occurs after a pooled physical connection has been reserved — the SqlConnection object is never disposed because the caller never receives it to place in a using scope. Under load with cancellations this can leak/pin pooled connections and eventually exhaust the connection pool.

**Evidence.**
```
var sqlConnection = new SqlConnection(connectionString);
await sqlConnection.OpenAsync(cancellationToken);
return sqlConnection;
```

**Proposed remediation.**
```
Wrap the open so the connection is disposed on failure:
var sqlConnection = new SqlConnection(connectionString);
try { await sqlConnection.OpenAsync(cancellationToken); }
catch { await sqlConnection.DisposeAsync(); throw; }
return sqlConnection;
```

**Verification.** The cited code (src/AppointMe.Shared/Database/SqlConnectionFactory.cs:10-12) constructs `new SqlConnection(connectionString)`, then `await sqlConnection.OpenAsync(cancellationToken)`, then returns it — with no try/catch. If OpenAsync throws (token cancelled mid-open, or a transient network/login failure after a physical connection has been reserved), the SqlConnection instance is never returned to the caller. Every caller relies on `using var connection = await connectionFactory.OpenConnectionAsync(...)` (confirmed in CustomersRepository.cs:37/61, AppointmentsRepository.cs:39/73, ServiceProvidersRepository.cs:24, TeamRepository.cs:65), and that `using` only binds after a successful return — so on an exception the object escapes any disposal scope. This is the single IDbConnectionFactory implementation, registered as a singleton in all four modules, so no alternate implementation exists. I looked for mitigations (wrapper types, middleware, base class) and found none; nothing else can dispose an object the caller never receives. This is the classic CA2000 leak-on-throw pattern and is genuinely present. Severity remains Low: the ADO.NET pooler reclaims leaked SqlConnection objects via finalization/pruning, so the practical impact is transient pool pressure under sustained cancellation/failure load, not a permanent leak, data breach, or auth issue.

**Verified remediation.**
```
public async Task<IDbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
{
    var sqlConnection = new SqlConnection(connectionString);
    try
    {
        await sqlConnection.OpenAsync(cancellationToken);
    }
    catch
    {
        await sqlConnection.DisposeAsync();
        throw;
    }
    return sqlConnection;
}
```

**Notes.** Confirmed as the only implementation of IDbConnectionFactory, registered singleton in Identity/Organizations/CRM/Booking modules; all repositories consume it via `using var connection = await ...`. The `using` cannot protect against a throw inside the factory because the caller never receives the instance. Real-world blast radius is bounded by connection-pool finalization/pruning, hence Low. The reviewer's cited line 10 is accurate (the construction site); the fix spans lines 10-12.



---

# Appendix C — Refuted Findings (8, investigated and dismissed)


These were raised by a finder but dropped after the verifier found them incorrect or fully mitigated. Recorded so the negative results are auditable.


## C1. [refuted; claimed Medium] ForwardedHeaders processing trusts X-Forwarded-Proto/For from any peer, undermining Secure-cookie and HTTPS enforcement

- **Location:** `src/AppointMe.Api/Program.cs:50`
- **Dimension:** auth-session

**Why refuted.** The middleware mechanism the reviewer describes is real: at src/AppointMe.Api/Program.cs:52-54 both KnownIPNetworks and KnownProxies are cleared while XForwardedFor|XForwardedProto processing is enabled, and ASP.NET Core's ForwardedHeadersMiddleware sets checkKnownIps=false when both lists are empty, so it applies forwarded headers from any immediate peer without a source-IP check. But the claimed AUTH-SESSION / Secure-cookie impact is factually wrong, and the transport impact has no exploitable consumer in this codebase:

1) Secure-cookie enforcement is NOT undermined. AuthenticationExtensions.cs:52 sets options.Cookie.SecurePolicy = CookieSecurePolicy.Always. `Always` sets the cookie's Secure attribute unconditionally — it does NOT depend on Request.Scheme/Request.IsHttps. The finding conflates `Always` with `SameAsRequest` (only the latter derives Secure from the request scheme). Spoofing `X-Forwarded-Proto: https` (or omitting it) cannot cause the `appointme.auth` cookie to be emitted without the Secure flag. The central claim — 'gates whether the auth cookie is considered secure' — describes behavior that does not exist here.

2) The X-Forwarded-For spoofing concern has no consumer. A full grep found zero uses of RemoteIpAddress / HttpContext.Connection / client-IP anywhere in src (no IP-based rate limiting, authorization, or security logging). There is nothing to spoof against.

3) UseHttpsRedirection being 'defeated' only affects the attacker's own request. HTTPS redirection protects users who arrive over plaintext by mistake; an attacker sending their own plaintext request with a spoofed X-Forwarded-Proto only suppresses their own redirect — no cross-user or session harm. There is also no scheme-derived redirect_uri construction in app code (the two RedirectUri usages in LoginEndpoint.cs and LogoutEndpoint.cs are built from configured BaseUrl/options, not Request.Scheme).

4) The clear-both-lists configuration is the Microsoft-recommended pattern for containerized deployments where the proxy IP is not enumerable/stable — and this project uses .NET Aspire (containerized ingress). Correctly forwarding X-Forwarded-Proto is in fact REQUIRED for the app to behave correctly behind a TLS-terminating proxy (OIDC callback/redirect construction, UseHttpsRedirection). The real-world mitigation for the 'any peer' trust is network isolation (the app container being reachable only via the ingress), which is an infrastructure property, not a code defect.

Net: the mechanism is correctly described, but the claimed defect ('undermines Secure-cookie and HTTPS enforcement') is neutralized by CookieSecurePolicy.Always and by the absence of any client-IP consumer. What remains is a minor defense-in-depth hardening opportunity, not the Medium transport/auth defect claimed.

**Notes.** Precise location: line 50 is the Configure(...) call; the two .Clear() lines the finding hinges on are 53-54. Framework mitigations that neutralize the claimed impact: (a) CookieSecurePolicy.Always is scheme-independent (AuthenticationExtensions.cs:52); (b) no RemoteIpAddress/client-IP consumer exists anywhere in src for X-Forwarded-For to affect. Deployment context: project uses .NET Aspire (containerized), where clearing KnownProxies/KnownNetworks is the standard/necessary pattern and network isolation is the intended mitigation. Separate, out-of-scope observation (not part of this finding): there is no UseHsts() call, so HSTS is not emitted — worth a distinct low-severity note if desired, but it does not make the ForwardedHeaders finding valid.


## C2. [refuted; claimed Low] Data Protection key ring silently falls back to an ephemeral local store when no persistence connection string is configured

- **Location:** `src/AppointMe.Api/DataProtection/DataProtectionExtensions.cs:18`
- **Dimension:** auth-session

**Why refuted.** The code at src/AppointMe.Api/DataProtection/DataProtectionExtensions.cs:18-25 does gate PersistKeysToAzureBlobStorage on a non-empty "DataProtectionStorage" connection string and never calls any ProtectKeysWith* (grep across src confirms no key-encryption-at-rest anywhere), and the base appsettings.json ships DataProtectionStorage: "". The auth cookie ("appointme.auth", HttpOnly/Secure/SameSite=Lax) does carry the id_token via context.Properties.StoreTokens (AuthenticationExtensions.cs:87-102), so the cookie payload contains PII and is protected solely by the DP key ring. So far the code matches the finding.

But the finding's load-bearing claim — "on a production topology WITHOUT the connection string ... the base appsettings.json ships an empty DataProtectionStorage, so this fallback is the default path" — is neutralized by config elsewhere in the repo. The shipped IaC configures durable persistence: infra/modules/storage.bicep provisions a Storage Account + private blob container "data-protection-keys" (allowBlobPublicAccess:false, publicAccess:'None', TLS1_2, httpsOnly), and infra/modules/app-service.bicep:100-102 unconditionally sets the App Service app setting ConnectionStrings__DataProtectionStorage to a @Microsoft.KeyVault(...) reference. That environment variable overrides the empty appsettings.json value at runtime, so in the shipped Devtest/production deployment the PersistKeysToAzureBlobStorage branch executes and keys are durable rather than ephemeral. The App Service is a single Linux container (WEBSITES_ENABLE_APP_SERVICE_STORAGE=false), and Azure Blob storage is SSE-encrypted at rest behind managed-identity/RBAC access, so the described impacts — session-invalidating key rotation on restart/scale-out, and an unencrypted key ring on the Linux filesystem readable by anyone with file access — do not describe the actual shipped topology. The empty base value is a placeholder overridden in deployment, not "the default production path."

What genuinely survives is only a weaker defense-in-depth/hardening observation: there is no application-level ProtectKeysWith* (the blob keys.xml relies on Azure SSE + private container rather than app-managed envelope encryption), and the code has no fail-fast guard, so if an operator skips the manual `az keyvault secret set --name DataProtectionStorage` step documented in infra/README.md the silent local fallback would engage. That is an operational footgun, not the exploitable defect described, and is Low/informational given the private-container + Key Vault + SSE mitigations already in place. Because a config mitigation elsewhere neutralizes the reported scenario, the finding as stated is refuted.

**Notes.** Key mitigations found: (1) infra/modules/storage.bicep — private blob container (allowBlobPublicAccess:false, publicAccess:'None', TLS1_2, httpsOnly), (2) infra/modules/app-service.bicep:100-102 — ConnectionStrings__DataProtectionStorage injected as a Key Vault reference into a single Linux container App Service, overriding the empty appsettings.json placeholder. Azure Blob is SSE-encrypted at rest and access is gated by managed-identity/RBAC, so the "unencrypted key ring on Linux filesystem / anyone with file access decrypts PII" impact does not apply to the shipped path. The cited file/line (DataProtectionExtensions.cs:18) is accurate. Residual, non-reported concerns worth a low-priority backlog note: no app-level ProtectKeysWith* envelope encryption, and no fail-fast if the DataProtectionStorage Key Vault secret (seeded manually per infra/README.md:166) is missing.


## C3. [refuted; claimed High] Demo mode enabled in the deployed Devtest environment turns the anonymous /login/demo endpoint into a full authentication bypass

- **Location:** `src/AppointMe.Api/appsettings.Devtest.json:29`
- **Dimension:** config-secrets

**Why refuted.** The code mechanically does what the finding describes: appsettings.Devtest.json:29 (git-tracked) sets Demo.Enabled=true with Password "AppointMe1"; infra/main.bicep:134 sets ASPNETCORE_ENVIRONMENT=Devtest on a public httpsOnly App Service (infra/modules/app-service.bicep:53); DemoModeExtensions.AddDemoMode registers the endpoint with no IsDevelopment gate; DemoLoginEndpoint.cs:23-24 maps GET /login/demo with .AllowAnonymous() and, when Enabled, calls SignInAsync (line 51); DemoSeedingSaga.cs:23 fires on every CompanyRegistered. No middleware/global filter neutralizes any of this. However, the finding's core characterization — a "full authentication bypass" where anyone gets a session "with no credentials" — is a false positive. Git commit 55fb47f is explicitly titled "add demo login for live demo and local quick start," and appsettings.Devtest.example.json also ships Demo.Enabled=true: demo login is an intentional feature of a purpose-built public live-demo environment, not an accidental misconfiguration. The endpoint does not bypass auth; it performs a real Entra External ID native-auth login (initiate→challenge→token, EntraExternalIdDemoUserAuthenticator.cs) for a single pre-provisioned shared demo account and can only ever mint a session for that one account (it fails with 502 if the demo user isn't provisioned with that password). It yields no access to arbitrary users or real customer data — seeded data is fake. The proposed remediation (gate on IsDevelopment, force Enabled=false in hosted overlays) would break the intended live-demo feature, confirming the reviewer misread design intent. Residual concerns are real but far lower severity: a weak password committed in-repo, and DemoSeedingSaga.Start triggering on ANY CompanyRegistered in the shared demo env (pollutes any real company with 100 fake customers).

**Notes.** File/line citation is accurate (appsettings.Devtest.json:29 = "Enabled": true). Downgraded from claimed High to Low because the flagged behavior is an intentional public live-demo feature (commit 55fb47f "add demo login for live demo"; example template also enables it), scoped to a single pre-provisioned shared demo account and seeded fake data — not a bypass of real user authentication and not exposure of real data. If the parent report wants to keep a hardening note, the defensible items are: (1) do not commit the demo account password (AppointMe1) — source it from Key Vault like the other secrets in app-service.bicep, since this lives under a repo path named "public"; (2) constrain DemoSeedingSaga.Start so it only seeds the designated demo company rather than every CompanyRegistered event, e.g. compare @event.CompanyId / owner against a configured demo company id before returning a saga. Do NOT gate /login/demo on IsDevelopment() as the finding proposes — that would disable the intended hosted live demo.


## C4. [refuted; claimed Low] Raw server error text surfaced to users via toast on 5xx/network errors (potential info disclosure)

- **Location:** `src/AppointMe.Frontend/src/lib/query-client.ts:21`
- **Dimension:** frontend-security

**Why refuted.** The claimed info-disclosure does not occur in the current code. The only 5xx-producing exception handler is GlobalExceptionHandler.cs (registered last; ValidationExceptionHandler/NotFoundExceptionHandler/ConflictExceptionHandler/AccessDeniedExceptionHandler each return false for non-matching types). GlobalExceptionHandler hardcodes ProblemDetails { Title = "Internal Server Error", Detail = "Unexpected error occurred.", Status = 500 } and adds no "error" extension. Although it sets ProblemDetailsContext.Exception = exception, the default IProblemDetailsService registered by AddProblemDetails() does not serialize exception details into the body; grep found no CustomizeProblemDetails, no IncludeExceptionDetails, and no UseDeveloperExceptionPage. app.UseExceptionHandler() is called unconditionally (Program.cs:71), so the sanitized body applies even in Development. Tracing query-client.ts: for a real 500, extractErrorMessage returns data.error (undefined) ?? data.detail ("Unexpected error occurred.") -> the generic message. For network errors (status undefined) error.response?.data is undefined, so it falls to error.message (axios generic). For a non-JSON 5xx body (e.g. proxy HTML string), data?.error/detail/title all resolve to undefined and it again falls to error.message. In no current path is internal exception text (stack/SQL/identifiers) rendered. The finding is explicitly conditioned on a configuration that does not exist ("if the API is ever configured with IncludeExceptionDetails..."), and the reviewer concedes the ASP.NET default does not leak. This is a speculative/hypothetical concern, not a present defect; the codebase already has the exact mitigation the remediation asks for.

**Notes.** Not a live vulnerability. At most a defense-in-depth / hardening note: the frontend trusts server-provided text for the 5xx toast, which would matter only if a future change made a 500 handler place exception detail into ProblemDetails.detail/error. Today GlobalExceptionHandler.cs:23-26 guarantees a fixed generic body with no exception detail, so nothing leaks. If the team wants belt-and-suspenders, the queryCache/mutationCache onError handlers (query-client.ts:34-45) could show a fixed literal like 'Something went wrong on our side. Please try again.' for the isServerError branch instead of extractErrorMessage(error), while keeping extractErrorMessage for intentional 4xx call-site messages. This is optional hardening, not a fix for an existing bug. Cited file/line (query-client.ts:21) is accurate for the frontend code but the alleged leak depends on backend behavior that is currently safe.


## C5. [refuted; claimed Low] Chart component injects a <style> block via dangerouslySetInnerHTML

- **Location:** `src/AppointMe.Frontend/src/components/ui/chart.tsx:73`
- **Dimension:** frontend-security

**Why refuted.** The cited code is accurate: src/AppointMe.Frontend/src/components/ui/chart.tsx:73 does inject a <style> block via dangerouslySetInnerHTML in ChartStyle, interpolating the chart `id` (line 44: `chart-${id || React.useId()...}`) and `itemConfig.color`/`itemConfig.theme` from ChartConfig (lines 80-81). This is the only dangerouslySetInnerHTML in the frontend. However, there is no defect to confirm. (1) The inputs are entirely developer-authored: `id` is generated/developer-passed and `config` is a statically-typed ChartConfig. (2) The component is dead code — grep across all of src for `ChartConfig`, `ChartContainer`, `ui/chart`, `recharts`, `ChartStyle`, and `<Chart` returns zero matches outside chart.tsx itself, so no ChartConfig is ever constructed, let alone from API/user data. No untrusted value can reach the sink. (3) The finding explicitly states "there is no XSS today" — it is a trust-boundary documentation note, not an actual, present, or reachable vulnerability. Per repo conventions, components/ui/ is vendored shadcn/ui code treated as semi-third-party (this ChartStyle pattern ships verbatim in the upstream shadcn charts primitive). There is nothing exploitable to fix; the "defect" is a hypothetical contingent on future code that populates ChartConfig from untrusted input, which does not exist.

**Notes.** Mitigations neutralizing the finding: (1) zero call sites — the component is never imported or rendered, so the code path is unreachable; (2) ChartConfig is a compile-time-typed developer artifact, not derived from any request/response; (3) repo CLAUDE.md classifies components/ui/ as vendored semi-third-party shadcn code, and this is the unmodified upstream ChartStyle implementation. Reviewer's file:line is correct. Category is better characterized as defense-in-depth/documentation than an input-validation defect since no input flows to the sink.


## C6. [refuted; claimed Low] X-Company-Id tenant selector is client-controlled (read from localStorage) — server enforcement confirmed

- **Location:** `src/AppointMe.Frontend/src/lib/axios.ts:25`
- **Dimension:** frontend-security

**Why refuted.** The reported "defect" is not a defect — it is a correctly-enforced trust boundary, and the finding's own explanation admits "No vulnerability found." Each link verified:

1. Frontend header injection is real but benign by design. src/AppointMe.Frontend/src/lib/axios.ts:22-26 reads `currentCompanyStore.get()` and, when present, sets `X-Company-Id`. current-company-store.ts:7,20 shows the value is backed by localStorage. A client controlling a tenant-selector header is the standard SPA pattern; it is not a frontend security defect in itself.

2. Server resolves the company from that header. Program.cs:29 wires `tenancy.FromHeader("X-Company-Id")`; HeaderCompanyDetection.cs:9-15 parses the header GUID; CompanyResolutionMiddleware.cs:17 pushes it into the ambient `ICurrentCompany` scope.

3. Membership is enforced server-side. UserPrincipalFactory.Create (UserPrincipalFactory.cs:14-25) takes `currentCompany.CompanyId` (the header value) and queries `dbContext.Employees.Where(employee => employee.UserId == user.Id)`. That DbSet carries a global query filter `employee.CompanyId == currentCompany.CompanyId` (OrganizationsDbContext.cs:34), so the effective query is UserId==caller AND CompanyId==header AND !IsDeleted. A non-member of the header company gets `roles == null` and the factory throws `AccessDeniedException("User is not authorized to access requested company.")`. Principal resolution runs via CurrentPrincipalResolver.cs:22 (per-request) and the Wolverine PrincipalContextBehavior, and any permission check forces this path.

4. EF global query filters (OrganizationsDbContext.cs:34,37,40) additionally scope Employees/Invitations/RolePermissionOverrides reads and writes to the header company.

So the specific claim ("tenant isolation cannot rely on the frontend") is a truism that the server already satisfies; there is nothing exploitable and no frontend change is warranted. Refuted as a security finding.

**Notes.** This should not have been filed as a finding; it is a documentation note about an intended design, and the reviewer explicitly wrote "No vulnerability found." One residual item worth tracking separately (not this finding): the ASP.NET fallback policy (AuthorizationServiceCollectionExtensions.cs:18-21) only requires an authenticated/registered user via RegisteredUserRequirement, which does NOT invoke UserPrincipalFactory — the membership check only fires when the principal is resolved (permission checks / Wolverine PrincipalContextBehavior). Any future authenticated endpoint that reads Organizations-module data purely through the EF company filter without triggering principal resolution would still be filter-scoped to the header company but would skip the explicit membership throw. That is speculative here (no such vulnerable endpoint traced; CQRS Dapper reads use a separate path) and is exactly what the reviewer's own remediation flags, so it does not change the REFUTED verdict for the reported frontend finding.


## C7. [refuted; claimed Low] Permission gating via <Can>/usePermission is UI-only — server enforcement confirmed

- **Location:** `src/AppointMe.Frontend/src/components/auth/use-permission.ts:6`
- **Dimension:** frontend-security

**Why refuted.** The finding is self-refuting (title says "server enforcement confirmed"; the explanation concedes the server enforces independently) and describes correct architecture rather than a defect. The cited frontend code is accurate: use-permission.ts:4-7 reads from the client-held `permissions` list (useUserAccess) and can.tsx:11-13 gates only UI visibility. That is exactly how client-side UX gating is supposed to work, and it is fully backed server-side. UserPrincipalFactory.Create (src/Organizations/AppointMe.Organizations/Infrastructure/UserPrincipalFactory.cs:12-34) resolves roles from the Employees table by UserId and computes permissions via PermissionResolver from the DB — never from client input — throwing AccessDeniedException when the user has no roles for the active company. PrincipalAuthorizationExtensions.Require (src/AppointMe.Shared/Authorization/Principals/PrincipalAuthorizationExtensions.cs:7-19) throws AccessDeniedException when a required permission is missing. Every mutating/reading handler calls principal.Require(...) with the matching permission (ScheduleAppointment→Schedule, GetAppointments→View, DeleteEmployee→EmployeePermissions.Remove, UpdatePermissions→PermissionPermissions.Manage, RegisterCustomer→CustomerPermissions.Create, etc. across Booking/CRM/Organizations). There is no exploitable weakness: hiding a button is never the sole control because the server independently authorizes each operation. Nothing actionable; no code change warranted. The reviewer's own remediation ("no frontend change required") confirms there is nothing to fix.

**Notes.** This is an informational/non-issue rather than a security finding. The UI-only nature of <Can>/usePermission is intentional and properly backed by comprehensive server-side enforcement (principal.Require in every command/query handler; principal resolved from DB via UserPrincipalFactory + PermissionResolver, not from client input). The one optional value-add is the reviewer's suggested negative test asserting a permission-gated endpoint returns 403 without the permission — a test-coverage nicety, not a vulnerability. Recommend dropping this from the security report or reclassifying as informational. Cited file/line (use-permission.ts:6) is accurate.


## C8. [refuted; claimed Low] Login forwards attacker-controllable returnUrl to /api/v1/login — server restricts to relative paths

- **Location:** `src/AppointMe.Frontend/src/app/auth/login/login.tsx:18`
- **Dimension:** frontend-security

**Why refuted.** Traced the flow end to end and there is a genuine mitigation that neutralizes any open redirect.

Frontend (src/AppointMe.Frontend/src/app/auth/login/login.tsx:18): the page never redirects the browser to returnUrl. It sets window.location.href to the hardcoded same-origin path `/api/v1/login`, carrying returnUrl only as a query parameter. So the frontend itself cannot be turned into an open redirect regardless of returnUrl content.

Backend (src/Identity/AppointMe.Identity/Login/LoginEndpoint.cs:20-24): the post-login redirect target is built as `new UriBuilder(frontendOptions.Value.BaseUrl)`. The host/scheme/port therefore always come from server-side config (FrontendOptions.BaseUrl, a `[Required, Url] Uri` bound from appsettings, e.g. "https://localhost:5173" — not attacker-controllable). The attacker-influenced returnUrl is only ever assigned to `redirectUri.Path`, and only when `Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)` is true.

Two independent guards make an external redirect impossible:
1. Absolute URLs like `https://evil.com` fail the UriKind.Relative check and are ignored entirely.
2. Even if a scheme-relative value like `//evil.com` passed the relative check, it is written to UriBuilder.Path, not the host. `UriBuilder.ToString()` keeps the host pinned to BaseUrl, yielding `https://localhost:5173//evil.com` — host stays localhost. Backslash variants (`/\evil.com`) contain invalid URI characters and fail IsWellFormedUriString.

So the redirect host is always BaseUrl; returnUrl can only influence the path on the trusted origin. The finding itself states "an external open redirect is not achievable" and rates it Low purely as documentation. This is a defense-in-depth note, not an exhibited defect — the claimed open-redirect vulnerability does not exist in the code.

**Notes.** FrontendOptions.BaseUrl is server-controlled config (src/AppointMe.Shared/Configuration/FrontendOptions.cs:9-10; value in src/AppointMe.Api/appsettings.json:9-11), confirmed not attacker-controllable. The reviewer already acknowledged non-exploitability and filed at Low as documentation only; there is no user-facing security impact and no code change is warranted on correctness grounds.
