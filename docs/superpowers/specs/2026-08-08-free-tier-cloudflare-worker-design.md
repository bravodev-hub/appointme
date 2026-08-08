# Free-tier devtest via Cloudflare Worker — design

**Date:** 2026-08-08
**Status:** approved

## Goal

Downgrade the devtest App Service Plan from B1 (~£10/mo) to F1 (free) while
keeping the branded demo URL `https://app.appointme.dev`. F1 forbids custom
hostname bindings (the attempted downgrade failed with "SSL configuration
'SNI enabled' is not allowed"), so the custom domain moves entirely to the
Cloudflare edge.

## Context

- `appointme.dev` is already a Cloudflare zone (proxied DNS); browser TLS
  already terminates at CF. The origin connection currently presents
  `Host: app.appointme.dev`, which is why the App Service needs a hostname
  binding + managed-cert SNI today (provisioned by
  `infra/modules/custom-domain*.bicep` when `CUSTOM_HOSTNAME` is set).
- CF free plan cannot rewrite the Host header to an out-of-zone value via
  Origin Rules — a Worker is the free mechanism.
- `Program.cs` processes only `XForwardedFor | XForwardedProto`; the public
  host currently comes from the Host header itself, which stops working once
  the binding is gone.
- User accepted F1 runtime limits: no Always On (cold start with EF
  migrations after idle, 30–90s first request), 60 CPU-min/day quota (403
  when exhausted), 1 GB shared memory.
- CF access: wrangler CLI, user authenticates once via `npx wrangler login`.

## Deliverables

### 1. Cloudflare Worker (edge host-rewrite proxy)

- New `infra/cloudflare-worker/` directory: `wrangler.toml` + `src/index.js`
  (committed — it is part of the template's devtest story).
- Worker forwards every request to
  `https://app-appointme-devtest-ze5tkm.azurewebsites.net` preserving path,
  query, method, headers, and body, and sets
  `X-Original-Host: app.appointme.dev`.
- Route: `app.appointme.dev/*` on zone `appointme.dev`. DNS record unchanged
  (stays proxied).
- Deployed with wrangler (`npx wrangler deploy`).

### 2. App change — forwarded host

In `Program.cs`, extend the existing `ForwardedHeadersOptions`:

- add `ForwardedHeaders.XForwardedHost` to `options.ForwardedHeaders`;
- set `options.ForwardedHostHeaderName = "X-Original-Host"`.

Effect: OIDC `redirect_uri`, login/logout redirects, and generated absolute
URLs use `app.appointme.dev` even though origin requests arrive under the
azurewebsites hostname. Entra redirect URI registrations are unchanged.

### 3. Azure teardown + downgrade (az CLI)

- Delete the SNI/hostname binding for `app.appointme.dev` and the App
  Service Managed Certificate (plan-scoped).
- `az appservice plan update --sku F1` (with `--number-of-workers 1` left as
  is). Verify `alwaysOn` is not enabled (F1 disallows it).

### 4. Bicep + docs

- `infra/main.bicepparam` / `modules/app-service-plan.bicep`: devtest SKU
  becomes `F1`. Check `modules/app-service.bicep` for `alwaysOn` and force it
  compatible with F1.
- `custom-domain*.bicep` modules remain in the repo as the paid-tier
  template option; README notes devtest instead uses the CF Worker
  (new subsection) and documents the Worker deploy command.
- Regenerate `infra/main.json`.

## Ordering (verify between every step)

1. App change → CI deploy (harmless while the binding still exists).
2. Worker + route live → full login round-trip via `app.appointme.dev`
   (traffic now reaches origin under the azurewebsites hostname).
3. Remove binding + certificate → verify again.
4. Downgrade plan to F1 → verify (first request is a cold start).
5. Commit Bicep/docs.

## Error handling / rollback

- Every step is reversible: scale back to B1 and re-apply the
  `custom-domain` module; delete the Worker route to restore direct
  proxying.
- If the Worker misroutes, the azurewebsites.net hostname remains reachable
  directly throughout — a second, independent path for verification.

## Verification

- After step 2 and step 4: demo login (`/login/demo`), register + delete a
  customer via `app.appointme.dev`, confirm redirects stay on the custom
  domain (no azurewebsites.net leaking into the address bar), site 200 on
  both hostnames.
- `az appservice plan show --query sku.name` returns `F1`.

## Out of scope

- Apex domain, other subdomains, or CF settings beyond the one Worker+route.
- Changing SQL tier or any other Azure resource.
- Prod guidance (the README continues to describe the managed-cert path for
  paid tiers).
