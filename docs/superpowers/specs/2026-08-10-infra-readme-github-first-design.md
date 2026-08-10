# infra/README.md rework: GitHub-first — design

**Date:** 2026-08-10
**Status:** approved

## Goal

`infra/README.md` leads with the GitHub Actions setup (the active deployment
path); GitLab remains documented as an alternative. All stale claims fixed.

## Structure

1. Intro + module table — add `custom-domain*.bicep` rows (optional, B1+) and
   the Log Analytics 0.5 GB/day cap.
2. "What this does NOT provision" — unchanged (already current).
3. One-time setup: (1) Entra External ID + custom-domain redirect-URI note;
   (2) CI managed identity + roles (provider-neutral, extracted from the old
   GitLab section); (3) GitHub — two federated credentials, secrets table,
   `APP_PUBLIC_URL` variable, `devtest` environment; (4) GitLab (alternative)
   — federated credential + variables, condensed.
4. First deploy — remove dead exports (`ENTRA_*`, `HANGFIRE_ADMINS` are not
   read by `main.bicepparam`), use `westeurope`, note `CUSTOM_HOSTNAME` must
   stay unset on F1.
5. New section: custom domain on the free tier (Cloudflare Worker,
   `X-Original-Host`, `APP_PUBLIC_URL`; `custom-domain.bicep` for B1+).
6. Seed Key Vault / Wolverine / prod-hardening — unchanged.

Docs-only; no code or pipeline changes. Verified-accurate facts and the full
staleness audit are recorded in the conversation of 2026-08-10.
