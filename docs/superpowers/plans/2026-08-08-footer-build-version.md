# Footer Build Version Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Footer shows `© 2026 AppointMe · <sha8>` where `<sha8>` is the deployed commit — identical to the container image tag both CIs already use.

**Architecture:** CI passes the git short SHA as a Docker build-arg; the Dockerfile exports it as `VITE_APP_VERSION` before the frontend build; Vite inlines it; a tiny `version.ts` reads it with a `dev` fallback; the footer renders it in an extra-muted span.

**Tech Stack:** Docker build args, Vite env inlining, React, GitHub Actions + GitLab CI.

**Spec:** `docs/superpowers/specs/2026-08-08-footer-build-version-design.md`

## Global Constraints

- `.gitlab-ci.yml` gets EXACTLY ONE change: the added `--build-arg` line in the `build-image` job. Nothing else in that file moves.
- The version value everywhere is the 8-char short SHA (`${GITHUB_SHA::8}` / `$CI_COMMIT_SHORT_SHA`) — same as the image tags.
- Frontend lint rule: no non-component exports in `.tsx` files — the env read lives in `version.ts`, not `footer.tsx`.
- Muted styling: SHA in `text-gray-400` (copyright itself is `text-gray-500`).

---

### Task 1: Frontend — version helper + footer

**Files:**
- Create: `src/AppointMe.Frontend/src/app/layouts/footer/version.ts`
- Modify: `src/AppointMe.Frontend/src/app/layouts/footer/footer.tsx`

**Interfaces:**
- Produces: `APP_VERSION: string` (value of `VITE_APP_VERSION` or `'dev'`), consumed only by `footer.tsx`. Tasks 2–3 make CI provide `VITE_APP_VERSION` at image build time.

- [ ] **Step 1: Create `version.ts`**

```ts
// Injected at image build time (Dockerfile ARG APP_VERSION → VITE_APP_VERSION);
// equals the git short SHA and the container image tag of the deployed build.
export const APP_VERSION: string = import.meta.env.VITE_APP_VERSION ?? 'dev';
```

- [ ] **Step 2: Render it in `footer.tsx`**

Replace the file content with:

```tsx
import { APP_VERSION } from './version';
import { FormattedDate } from '@/components/format';

export const Footer = () => {
    return (
        <footer className='p-4 text-center text-xs text-gray-500'>
            © <FormattedDate date={new Date()} format='year' />{' '}
            <span className='font-extralight'>
                Appoint<span className='font-semibold'>Me</span>
            </span>
            <span className='text-gray-400'> · {APP_VERSION}</span>
        </footer>
    );
};
```

- [ ] **Step 3: Verify lint and the inlined value**

```bash
cd src/AppointMe.Frontend
npm run lint
VITE_APP_VERSION=test123 npm run build
grep -rl "test123" dist/assets/ | head -1
```

Expected: lint 0 errors; grep prints one bundle file (value was inlined).

- [ ] **Step 4: Commit (no push yet)**

```bash
git add src/AppointMe.Frontend/src/app/layouts/footer/
git commit -m "Show build version in the footer"
```

---

### Task 2: Dockerfile — accept the build-arg

**Files:**
- Modify: `src/AppointMe.Api/Dockerfile:5-10` (frontend stage)

**Interfaces:**
- Consumes: `VITE_APP_VERSION` contract from Task 1.
- Produces: `APP_VERSION` build-arg (default `local`) that Tasks 3–4 set from CI.

- [ ] **Step 1: Add ARG/ENV to the frontend stage**

Change:

```dockerfile
# Stage 1: frontend bundle
FROM node:22-alpine AS frontend
WORKDIR /src
COPY src/AppointMe.Frontend/package.json src/AppointMe.Frontend/package-lock.json ./
RUN npm ci
COPY src/AppointMe.Frontend/ ./
RUN npm run build
```

to:

```dockerfile
# Stage 1: frontend bundle
FROM node:22-alpine AS frontend
# Git short SHA of the build; shown in the app footer. CI passes it explicitly.
ARG APP_VERSION=local
ENV VITE_APP_VERSION=$APP_VERSION
WORKDIR /src
COPY src/AppointMe.Frontend/package.json src/AppointMe.Frontend/package-lock.json ./
RUN npm ci
COPY src/AppointMe.Frontend/ ./
RUN npm run build
```

- [ ] **Step 2: Verify with a local docker build (if docker available)**

```bash
docker build -f src/AppointMe.Api/Dockerfile --build-arg APP_VERSION=abc12345 --target frontend -t footer-check . \
  && docker run --rm footer-check sh -c "grep -rl abc12345 /src/dist/assets | head -1"
```

Expected: prints a bundle path. If docker is unavailable locally, note it and rely on Task 4's deployed verification (Task 1's Vite check already proved the inlining mechanism).

- [ ] **Step 3: Commit (no push yet)**

```bash
git add src/AppointMe.Api/Dockerfile
git commit -m "Accept APP_VERSION build-arg for the frontend bundle"
```

---

### Task 3: Both CI pipelines pass the SHA

**Files:**
- Modify: `.github/workflows/devtest.yml` (build-image job, az acr build step)
- Modify: `.gitlab-ci.yml` (build-image job — the ONLY change in this file)

**Interfaces:**
- Consumes: `APP_VERSION` build-arg from Task 2.

- [ ] **Step 1: GitHub workflow**

In `.github/workflows/devtest.yml`, change:

```yaml
          az acr build \
            --registry "${{ secrets.ACR_NAME }}" \
            --image "$IMAGE_REPO:${GITHUB_SHA::8}" \
            --image "$IMAGE_REPO:latest" \
            --file src/AppointMe.Api/Dockerfile \
            .
```

to:

```yaml
          az acr build \
            --registry "${{ secrets.ACR_NAME }}" \
            --image "$IMAGE_REPO:${GITHUB_SHA::8}" \
            --image "$IMAGE_REPO:latest" \
            --build-arg APP_VERSION="${GITHUB_SHA::8}" \
            --file src/AppointMe.Api/Dockerfile \
            .
```

- [ ] **Step 2: GitLab pipeline (single added line)**

In `.gitlab-ci.yml`, change:

```yaml
    - az acr build
        --registry "$ACR_NAME"
        --image "$IMAGE_REPO:$CI_COMMIT_SHORT_SHA"
        --image "$IMAGE_REPO:latest"
        --file src/AppointMe.Api/Dockerfile
        .
```

to:

```yaml
    - az acr build
        --registry "$ACR_NAME"
        --image "$IMAGE_REPO:$CI_COMMIT_SHORT_SHA"
        --image "$IMAGE_REPO:latest"
        --build-arg APP_VERSION="$CI_COMMIT_SHORT_SHA"
        --file src/AppointMe.Api/Dockerfile
        .
```

- [ ] **Step 3: Validate and commit**

```bash
actionlint .github/workflows/devtest.yml
git diff --stat .gitlab-ci.yml   # expect: 1 insertion, 0 deletions
git add .github/workflows/devtest.yml .gitlab-ci.yml
git commit -m "Pass build SHA into the image as APP_VERSION"
```

---

### Task 4: Deploy and verify the live footer

- [ ] **Step 1: Push and watch CI**

```bash
git push origin main
sleep 15
RUN_ID=$(gh run list --repo bravodev-hub/appointme --workflow devtest --limit 1 --json databaseId -q '.[0].databaseId')
gh run watch $RUN_ID --repo bravodev-hub/appointme --exit-status --interval 30
```

Expected: all three jobs green.

- [ ] **Step 2: Verify the deployed footer shows the pushed SHA**

```bash
SHA8=$(git rev-parse HEAD | cut -c1-8)
# The SPA inlines the sha into the JS bundle; fetch the page's bundle and grep.
curl -s https://app.appointme.dev/ | grep -o 'src="[^"]*\.js"' | head -3
```

Then, via browser automation: open `https://app.appointme.dev`, scroll to the footer, confirm it reads `© 2026 AppointMe · <SHA8>` (screenshot for the user). Allow for F1 cold start on first load.

Expected: footer shows exactly the pushed commit's 8-char SHA.
