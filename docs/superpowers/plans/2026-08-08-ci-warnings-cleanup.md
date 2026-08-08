# CI Warnings Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Zero annotations on the devtest run: actions bumped to Node-24 majors; all 14 `react-refresh/only-export-components` warnings resolved (7 via eslint override for vendored ui, 7 via house-convention file splits).

**Architecture:** Pure mechanical changes — version strings in workflows, one eslint config override, and export moves that preserve every public import path (barrels re-export the new files). `tsc -b` (via `npm run build`) is the safety net for missed imports.

**Tech Stack:** GitHub Actions, eslint flat config, React 19 / TypeScript.

**Spec:** `docs/superpowers/specs/2026-08-08-ci-warnings-cleanup-design.md`

## Global Constraints

- Bump ONLY `actions/*`: `checkout@v7`, `setup-node@v7`, `setup-dotnet@v6`, `cache@v6`. Do NOT touch `github/codeql-action@v3`, `gitleaks/gitleaks-action@v2`, `azure/login@v2`.
- Public import paths must keep working — consumers import via the `components/auth` / `components/theme` barrels; barrels gain `export *` lines for new files.
- No behavior changes anywhere; moves only.
- Frontend conventions: kebab-case filenames, `use-x.ts` for hooks, 4-space indent, single quotes.

---

### Task 1: Bump actions to Node-24 majors

**Files:**
- Modify: `.github/workflows/devtest.yml` (checkout ×2, setup-dotnet, cache, setup-node)
- Modify: `.github/workflows/codeql.yml` (checkout)
- Modify: `.github/workflows/secret-scan.yml` (checkout)

- [ ] **Step 1: Apply version bumps**

In all three files replace exactly:
- `uses: actions/checkout@v4` → `uses: actions/checkout@v7`
- `uses: actions/setup-dotnet@v4` → `uses: actions/setup-dotnet@v6`
- `uses: actions/cache@v4` → `uses: actions/cache@v6`
- `uses: actions/setup-node@v4` → `uses: actions/setup-node@v7`

- [ ] **Step 2: Validate and commit**

```bash
actionlint .github/workflows/*.yml
grep -rn "actions/.*@v4" .github/workflows/ && echo "LEFTOVER v4!" || echo "clean"
git add .github/workflows/
git commit -m "Bump actions to Node 24 majors"
```

Expected: actionlint silent; "clean".

---

### Task 2: eslint override for vendored shadcn ui

**Files:**
- Modify: `src/AppointMe.Frontend/eslint.config.js`

- [ ] **Step 1: Append the override block**

Inside the `tseslint.config(...)` call, after the existing config object, add:

```js
    {
        // Vendored shadcn/ui components ship cva variants and hooks alongside
        // their components by upstream design. Keeping them unsplit makes
        // future shadcn updates diff cleanly; the trade-off is full-reload
        // HMR when editing these files.
        files: ['src/components/ui/**/*.tsx'],
        rules: {
            'react-refresh/only-export-components': 'off',
        },
    },
```

- [ ] **Step 2: Verify the 7 ui warnings are gone**

```bash
cd src/AppointMe.Frontend && npx eslint . 2>&1 | tail -2
```

Expected: `✖ 7 problems (0 errors, 7 warnings)` — only the app-code warnings remain.

- [ ] **Step 3: Commit**

```bash
git add src/AppointMe.Frontend/eslint.config.js
git commit -m "Exempt vendored shadcn ui from the fast-refresh export rule"
```

---

### Task 3: Split context hooks into sibling .ts files (5 files, 5 warnings)

**Files:**
- Create: `src/components/auth/use-current-user.ts`, `use-user-access.ts`, `use-current-company.ts`
- Create: `src/components/theme/use-theme.ts`
- Create: `src/app/settings/permissions/use-permission-editor-context.ts`
- Modify: the five `.tsx` provider files, both barrels (`components/auth/index.ts`, `components/theme/index.ts`), and any direct-import sites (`components/auth/use-permission.ts`, `components/format/use-time-zone.ts`, `app/layouts/sidebar/nav-main.tsx`, `app/settings/permissions/permissions.tsx`, `use-permission-editor.ts`, `permission-checkbox-cell.tsx` — final list from grep in Step 3)

**Interfaces:**
- Produces (unchanged public API, new home): `useCurrentUser()`, `useUserAccess()`, `useCurrentCompany()`, `useTheme()`, `usePermissionEditorContext()`, types `Theme`, `PermissionEditor`, `PermissionCellState`, `CurrentCompanyContextValue`. Context objects (`CurrentUserContext`, etc.) are exported from the new `.ts` files and imported by their providers.

- [ ] **Step 1: Move context + hook per file**

Pattern (identical for all five, shown for current-user): new `use-current-user.ts` receives the `createContext` call and the hook; the provider imports the context:

```ts
// use-current-user.ts
import { GetCurrentUserResponse } from '@/api/appointme.schemas';
import { createContext, use } from 'react';

export const CurrentUserContext = createContext<GetCurrentUserResponse | null>(null);

export const useCurrentUser = () => {
    const context = use(CurrentUserContext);
    if (context === null) {
        throw new Error('useCurrentUser must be used within a CurrentUserProvider');
    }

    return context;
};
```

```tsx
// current-user-context.tsx (keeps ONLY the provider)
import { CurrentUserContext } from './use-current-user';
import { useGetCurrentUserSuspense } from '@/api/appointme';
import { type ReactNode } from 'react';

export const CurrentUserProvider = ({ children }: { children: ReactNode }) => { /* body unchanged */ };
```

Apply the same mechanical move to:
- `use-user-access.ts` ← `UserAccessContext` + `useUserAccess`
- `use-current-company.ts` ← `CurrentCompanyContextValue` interface + `CurrentCompanyContext` + `useCurrentCompany`
- `use-theme.ts` ← `Theme` type + `ThemeProviderState` type + `ThemeProviderContext` + `useTheme` (the `isTheme` guard and `ThemeProviderProps` stay in `theme-provider.tsx`; it imports `Theme` and `ThemeProviderContext`)
- `use-permission-editor-context.ts` ← `PermissionCellState` + `PermissionEditor` interfaces + `PermissionEditorContext` + `usePermissionEditorContext` (provider stays, imports the context and `PermissionEditor` type)

- [ ] **Step 2: Update the barrels**

`src/components/auth/index.ts` — add:

```ts
export * from './use-current-user';
export * from './use-user-access';
export * from './use-current-company';
```

`src/components/theme/index.ts` — add:

```ts
export * from './use-theme';
```

- [ ] **Step 3: Fix direct-import sites**

```bash
cd src/AppointMe.Frontend
grep -rln "from '.*current-user-context'\|from '.*user-access-context'\|from '.*current-company-context'\|from '.*theme-provider'\|from '.*permission-editor-context'" src
```

For each hit that imports a HOOK/TYPE (not the Provider), point it at the new `use-*` file (or the barrel). Providers keep importing from the `-context.tsx` / `theme-provider.tsx` files.

- [ ] **Step 4: Verify**

```bash
npm run build > /dev/null 2>&1; echo "build: $?"
npx eslint . 2>&1 | tail -2
```

Expected: build 0; `✖ 2 problems (0 errors, 2 warnings)` (only the two columns files left).

- [ ] **Step 5: Commit**

```bash
git add src/AppointMe.Frontend/src
git commit -m "Split context hooks into sibling .ts files per fast-refresh convention"
```

---

### Task 4: Move ActionsCell out of the two columns.tsx files (2 warnings)

**Files:**
- Create: `src/app/team/actions-cell.tsx`, `src/app/customers/actions-cell.tsx`
- Modify: `src/app/team/columns.tsx`, `src/app/customers/columns.tsx`

- [ ] **Step 1: Mechanical move (both files)**

In each `columns.tsx`: cut the entire `const ActionsCell = ({ row }: { row: { original: …Dto } }) => { … };` block into the new sibling `actions-cell.tsx`, change `const` to `export const`, move exactly the imports the block uses (dialogs, api hooks, modal-dialog, ui dropdown/button pieces, `usePermission`/`Can`, toast, query client, icon), and in `columns.tsx` add `import { ActionsCell } from './actions-cell';`. The `columns` export and its inline cell renderers stay put.

- [ ] **Step 2: Verify zero warnings**

```bash
cd src/AppointMe.Frontend
npm run lint 2>&1 | tail -2
npm run build > /dev/null 2>&1; echo "build: $?"
```

Expected: lint prints nothing above the npm epilogue (0 problems); build 0.

- [ ] **Step 3: Commit**

```bash
git add src/AppointMe.Frontend/src/app
git commit -m "Move row action cells out of columns files per fast-refresh convention"
```

---

### Task 5: Deploy and confirm zero annotations

- [ ] **Step 1: Push and watch**

```bash
git push origin main
sleep 15
RUN_ID=$(gh run list --repo bravodev-hub/appointme --workflow devtest --limit 1 --json databaseId -q '.[0].databaseId')
gh run watch $RUN_ID --repo bravodev-hub/appointme --exit-status --interval 30
```

Expected: three green jobs.

- [ ] **Step 2: Confirm annotations are gone**

```bash
gh run view $RUN_ID --repo bravodev-hub/appointme | sed -n '/ANNOTATIONS/,$p'
```

Expected: no ANNOTATIONS section (or an empty one) — no Node 20 warning, no eslint warnings.
