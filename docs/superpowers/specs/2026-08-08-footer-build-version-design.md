# Footer build version — design

**Date:** 2026-08-08
**Status:** approved

## Goal

Show which build is deployed, in the frontend footer, with zero release
ceremony: `© 2026 AppointMe · <sha8>`. The version must always identify the
deployed build exactly.

## Strategy

The 8-char git commit SHA is already the deployment identity — both CI
pipelines tag the container image with it. The footer displays the same
value, so footer text == image tag == commit. No semver, no manual bumps,
nothing to forget. Rejected: package.json semver (stale when bumps are
forgotten), build timestamps (don't map back to a commit).

## Deliverables

1. **Dockerfile** (`src/AppointMe.Api/Dockerfile`, frontend stage): add
   `ARG APP_VERSION=local` and `ENV VITE_APP_VERSION=$APP_VERSION` before
   `npm run build`. Vite statically inlines the value.
2. **GitHub workflow** (`.github/workflows/devtest.yml`, build-image job):
   add `--build-arg APP_VERSION=${GITHUB_SHA::8}` to `az acr build`.
3. **GitLab pipeline** (`.gitlab-ci.yml`, build-image job): add
   `--build-arg APP_VERSION=$CI_COMMIT_SHORT_SHA` to `az acr build` — the
   ONLY GitLab change; everything else stays untouched.
4. **Frontend**:
   - `src/app/layouts/footer/version.ts` — exports
     `APP_VERSION: string` = `import.meta.env.VITE_APP_VERSION ?? 'dev'`
     (separate `.ts` file per the react-refresh lint rule: no non-component
     exports in `.tsx`).
   - `footer.tsx` — append ` · <APP_VERSION>` after the brand in an
     extra-muted span (`text-gray-400`, one shade lighter than the
     copyright's `text-gray-500`).

## Behavior

- Deployed (either CI): `© 2026 AppointMe · 339c990c`.
- Local `npm run dev` / local docker build without the arg:
  `© 2026 AppointMe · dev` / `· local`.

## Verification

- Local: `VITE_APP_VERSION=test123 npm run build` then grep `test123` in
  `dist/`; `npm run lint` clean.
- Deployed: after CI, the live footer at app.appointme.dev shows the pushed
  commit's sha8 (browser check).

## Out of scope

- Backend/API version exposure, health endpoints, semver of any kind.
