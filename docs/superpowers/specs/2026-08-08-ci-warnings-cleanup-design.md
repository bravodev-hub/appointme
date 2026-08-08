# CI warnings cleanup — design

**Date:** 2026-08-08
**Status:** approved

## Goal

A green Actions run with **zero annotations**: no Node 20 deprecation
warning, no `react-refresh/only-export-components` eslint warnings.
`npm run lint` locally reports 0 errors / 0 warnings.

## Context

- The Node 20 warning comes from the runner: `actions/checkout@v4`,
  `setup-node@v4`, `setup-dotnet@v4`, `cache@v4` declare Node 20. Latest
  majors (verified via GitHub API): `checkout@v7`, `setup-node@v7`,
  `setup-dotnet@v6`, `cache@v6`.
- 14 react-refresh warnings: 7 in vendored shadcn `src/components/ui/*`
  (`badge`, `button`, `button-group`, `toggle`, `navigation-menu` export cva
  variants; `form`, `sidebar` export hooks — upstream shadcn shape), 7 in app
  code (5 context/provider files + 2 `columns.tsx`).
- eslint annotations appear on CI because `actions/setup-node` registers a
  problem matcher; locally the same warnings print but exit 0.
- House convention (frontend CLAUDE.md): components in `.tsx`, hooks/
  constants in sibling `.ts`; `components/ui/` is semi-third-party.

## Deliverables

### 1. Action bumps (3 workflow files)

- `.github/workflows/devtest.yml`: `checkout@v7` (×2), `setup-dotnet@v6`,
  `cache@v6`, `setup-node@v7`.
- `.github/workflows/codeql.yml`: `checkout@v7`.
- `.github/workflows/secret-scan.yml`: `checkout@v7`.
- NOT bumped (not flagged): `github/codeql-action@v3`,
  `gitleaks/gitleaks-action@v2`, `azure/login@v2`.

### 2. eslint override for vendored shadcn (7 warnings)

In the frontend eslint config, disable
`react-refresh/only-export-components` for `src/components/ui/**` with a
comment explaining: vendored shadcn components ship variants/hooks alongside
components by upstream design; editing them falls back to full-reload HMR.

### 3. App-code splits (7 warnings), per house convention

- `components/auth/current-user-context.tsx`,
  `current-company-context.tsx`, `user-access-context.tsx`;
  `components/theme/theme-provider.tsx`;
  `app/settings/permissions/permission-editor-context.tsx`:
  move the context object + `useX` hook into a sibling `.ts` file; the
  Provider component stays in `.tsx`. Update import sites and barrels.
  Existing public import paths (via barrels) must keep working.
- `app/team/columns.tsx`, `app/customers/columns.tsx`: move the exported
  component(s) into their own `.tsx` file(s); the column defs stay.
- Exact member-by-member moves are pinned in the implementation plan after
  reading each file; the strategy above is fixed.

## Error handling / risk

- Action major bumps are validated by the CI run itself; any breaking change
  fails visibly and is rolled back by re-pinning the previous major.
- Frontend splits are pure moves (no behavior change); `npm run build`
  (tsc -b) catches any missed import.

## Verification

- Local: `npm run lint` → 0 errors, 0 warnings; `npm run build` green.
- CI: push → devtest run green with an empty ANNOTATIONS section
  (`gh run view` shows none).

## Out of scope

- `github/codeql-action`, `gitleaks-action`, `azure/login` versions.
- Any visual/behavioral frontend change.
- The parked items (ACR migration, gitleaks license).
