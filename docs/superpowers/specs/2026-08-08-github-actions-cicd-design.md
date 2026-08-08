# GitHub Actions CI/CD — design

**Date:** 2026-08-08
**Status:** approved

## Goal

Add a GitHub Actions CI/CD pipeline with feature parity to the existing GitLab
pipeline (`.gitlab-ci.yml`), so deployment works from GitHub
(`bravodev-hub/appointme`). GitLab pipeline stays untouched — this is a SaaS
template and both CI providers must remain supported.

## Context

- Azure devtest infra is live in subscription `AppointMe-DevTest`
  (`9187eacf-3a7f-4877-98fe-7f6b4b25ff5c`): resource group
  `rg-appointme-devtest`, CI identity `id-appointme-devtest-ci`
  (clientId `6d4ca079-55eb-4ab2-ac6e-d33e72aa8352`), ACR
  `acrappointmedevtestze5tkm`, Web App `app-appointme-devtest-ze5tkm`.
- A stale `.github/workflows/devtest.yml` exists ("Initial version", never
  wired up). It uses yarn against an npm repo (broken), buildx + `AcrPush`
  instead of GitLab's `az acr build`, and divergent secret names.
- The CI identity has only a GitLab federated credential; no GitHub secrets
  exist. That is why deploys from GitHub don't work.

## Decisions

1. **Image build: `az acr build`** (server-side), mirroring GitLab. Works with
   the roles already granted; keeps both pipelines symmetric. Rejected:
   buildx + push (needs AcrPush, structurally divergent).
2. **SAST parity: add CodeQL** for C# and JS/TS (free on public repos).
3. **Secrets setup: automated via gh CLI** (install with Homebrew, one-time
   interactive auth), not manual UI or browser automation.

## Deliverables

### 1. Rewrite `.github/workflows/devtest.yml`

Mirror `.gitlab-ci.yml` job-for-job, same secret names:

- **test** — every PR + push to `main`. .NET 10 SDK, NuGet cache keyed on
  `Directory.Packages.props`, `dotnet restore/build/test`. Frontend steps
  fixed to npm (`npm ci`, `npm run lint`, `npm run build`) — kept from the old
  file even though GitLab lacks them (cheap, intended, approved).
- **build-image** — `main` + `workflow_dispatch` only, needs test.
  `azure/login@v2` OIDC, then `az acr build` tagging
  `appointme-api:<short-sha-8>` and `:latest`.
- **deploy-devtest** — same gating, needs build-image. GitHub environment
  `devtest` with app URL. `az webapp config container set` + `az webapp
  restart` (EF migrations run on container startup).
- Concurrency group cancels superseded PR runs (mirrors GitLab
  `interruptible: true`).
- `permissions: id-token: write, contents: read`.
- Secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`,
  `AZURE_RESOURCE_GROUP`, `ACR_NAME`, `APP_SERVICE_NAME` — identical names to
  GitLab CI/CD variables.

### 2. New `.github/workflows/codeql.yml`

CodeQL scan, languages `csharp` and `javascript-typescript`, on PRs, pushes to
`main`, and a weekly schedule.

### 3. Azure one-time setup (az CLI, run during implementation)

On `id-appointme-devtest-ci`, add two federated credentials (GitLab credential
untouched), issuer `https://token.actions.githubusercontent.com`, audience
`api://AzureADTokenExchange`:

- `github-main` — subject `repo:bravodev-hub/appointme:ref:refs/heads/main`
  (build-image job).
- `github-env-devtest` — subject
  `repo:bravodev-hub/appointme:environment:devtest` (deploy job — a job with
  `environment:` gets a different OIDC subject; without this credential the
  deploy job's login silently fails).

Verify role assignments by **principalId** (earlier check wrongly used
clientId): Contributor scoped to the ACR, Website Contributor scoped to the
Web App. Create if missing.

### 4. GitHub setup (gh CLI)

- `brew install gh`; user authenticates once (`gh auth login --web`).
- Create the 6 repo Actions secrets with real values from the
  `AppointMe-DevTest` subscription.
- Create the `devtest` environment on the repo.

### 5. Docs

Add a "GitHub OIDC" section to `infra/README.md` alongside the GitLab one:
federated credential commands, secret table, note that both CI providers are
supported. `.gitlab-ci.yml` stays as-is.

## Error handling

- Deploy only runs after a successful image build (`needs:` chain).
- OIDC login failures surface in the `azure/login` step; the two-credential
  setup above is the known failure mode and is handled up front.
- `az webapp restart` forces a pull of the new tag; a failed container start
  is visible in App Service logs, not CI — unchanged from GitLab behavior.

## Verification

Commit to `main`, watch the run (`gh run watch`), confirm all three jobs pass,
then confirm the Web App serves the new image
(`https://app-appointme-devtest-ze5tkm.azurewebsites.net`).

## Out of scope

- Any change to `.gitlab-ci.yml` or GitLab CI/CD variables.
- Branch protection / environment protection rules.
- Prod environments (devtest only, per project posture).
