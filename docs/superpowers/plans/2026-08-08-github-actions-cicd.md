# GitHub Actions CI/CD Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a GitHub Actions CI/CD pipeline (test → ACR build → devtest deploy) with feature parity to `.gitlab-ci.yml`, and wire up the Azure federated credentials + GitHub secrets so deploys from `bravodev-hub/appointme` actually work.

**Architecture:** Three-job workflow mirroring the GitLab stages, authenticating to Azure with secret-less OIDC federation via `azure/login@v2`. Image builds run server-side with `az acr build`. A separate CodeQL workflow replaces GitLab's SAST template. One-time setup adds two federated credentials to the existing `id-appointme-devtest-ci` managed identity and six repo Actions secrets.

**Tech Stack:** GitHub Actions, azure/login@v2 (OIDC), az CLI, gh CLI, CodeQL, Azure Web App for Containers + ACR.

**Spec:** `docs/superpowers/specs/2026-08-08-github-actions-cicd-design.md`

## Global Constraints

- `.gitlab-ci.yml` and GitLab CI/CD variables must NOT be modified (both CI providers stay supported — SaaS template).
- Secret names must match the GitLab variable names exactly: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_RESOURCE_GROUP`, `ACR_NAME`, `APP_SERVICE_NAME`.
- Image repo name: `appointme-api`; tags: 8-char short SHA + `latest` (same as GitLab's `$CI_COMMIT_SHORT_SHA`).
- Frontend uses **npm** (`package-lock.json`), never yarn.
- Azure subscription: `AppointMe-DevTest` = `9187eacf-3a7f-4877-98fe-7f6b4b25ff5c`; resource group `rg-appointme-devtest`; identity `id-appointme-devtest-ci` (clientId `6d4ca079-55eb-4ab2-ac6e-d33e72aa8352`); ACR `acrappointmedevtestze5tkm`; Web App `app-appointme-devtest-ze5tkm`.
- **Do not `git push` until Task 4 is complete** — a push to `main` triggers the deploy pipeline, which fails without the federated credentials + secrets.
- `environment.url` in GitHub Actions cannot reference the `secrets` context — the deploy job derives the URL from a step output instead. Do not "simplify" this back to a secret reference.

---

### Task 1: Rewrite `.github/workflows/devtest.yml`

**Files:**
- Modify: `.github/workflows/devtest.yml` (full replacement)

**Interfaces:**
- Produces: workflow `devtest` with jobs `test`, `build-image`, `deploy-devtest`; consumes the six repo secrets listed in Global Constraints; deploy job uses GitHub environment `devtest` (Task 3 creates a federated credential whose subject names this environment — the names must stay in sync).

- [ ] **Step 1: Replace the file content**

Replace the entire content of `.github/workflows/devtest.yml` with:

```yaml
# CI/CD for AppointMe (GitHub Actions)
#
# Mirror of .gitlab-ci.yml — this repo is a SaaS template and both GitLab and
# GitHub pipelines are supported.
#
# Flow:
#   test           — build the solution and run all tests (every PR + main)
#   build-image    — build the API container in ACR (az acr build, server-side) on main
#   deploy-devtest — repoint the devtest Web App at the new image on main
#
# Azure auth is secret-less via GitHub OIDC federation: GitHub mints an ID token,
# Azure trusts it through federated credentials on the CI identity.
#
# Required repo Actions secrets (Settings → Secrets and variables → Actions):
#   AZURE_CLIENT_ID        clientId of the CI managed identity
#   AZURE_TENANT_ID        Azure tenant ID
#   AZURE_SUBSCRIPTION_ID  subscription holding the devtest resource group
#   AZURE_RESOURCE_GROUP   e.g. rg-appointme-devtest
#   ACR_NAME               ACR name (Bicep output containerRegistryName, no .azurecr.io)
#   APP_SERVICE_NAME       Web App name (Bicep output)
# See the one-time Azure setup in infra/README.md (GitHub OIDC section).

name: devtest

on:
  push:
    branches: [main]
  pull_request:
  workflow_dispatch:

permissions:
  id-token: write
  contents: read

# Cancel superseded runs on PRs (mirrors GitLab `interruptible: true`).
concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: ${{ github.event_name == 'pull_request' }}

env:
  IMAGE_REPO: appointme-api
  DOTNET_CLI_TELEMETRY_OPTOUT: "1"
  DOTNET_NOLOGO: "1"

jobs:
  test:
    name: Build and test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Cache NuGet packages
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: nuget-${{ runner.os }}-${{ hashFiles('Directory.Packages.props') }}
          restore-keys: nuget-${{ runner.os }}-

      - name: Restore
        run: dotnet restore AppointMe.sln

      - name: Build
        run: dotnet build AppointMe.sln -c Release --no-restore

      - name: Test
        run: dotnet test AppointMe.sln -c Release --no-build --logger "console;verbosity=normal"

      - name: Setup Node
        uses: actions/setup-node@v4
        with:
          node-version: '22'
          cache: npm
          cache-dependency-path: src/AppointMe.Frontend/package-lock.json

      - name: Frontend install
        working-directory: src/AppointMe.Frontend
        run: npm ci

      - name: Frontend lint
        working-directory: src/AppointMe.Frontend
        run: npm run lint

      - name: Frontend build
        working-directory: src/AppointMe.Frontend
        run: npm run build

  build-image:
    name: Build image in ACR
    runs-on: ubuntu-latest
    needs: test
    if: github.ref == 'refs/heads/main' && (github.event_name == 'push' || github.event_name == 'workflow_dispatch')
    steps:
      - uses: actions/checkout@v4

      - name: Azure login (OIDC)
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Build image (az acr build)
        run: |
          az acr build \
            --registry "${{ secrets.ACR_NAME }}" \
            --image "$IMAGE_REPO:${GITHUB_SHA::8}" \
            --image "$IMAGE_REPO:latest" \
            --file src/AppointMe.Api/Dockerfile \
            .

  deploy-devtest:
    name: Deploy to devtest
    runs-on: ubuntu-latest
    needs: build-image
    if: github.ref == 'refs/heads/main' && (github.event_name == 'push' || github.event_name == 'workflow_dispatch')
    # environment.url cannot reference the secrets context, so it comes from a
    # step output instead.
    environment:
      name: devtest
      url: ${{ steps.app-url.outputs.url }}
    steps:
      - name: Azure login (OIDC)
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Point Web App at new image
        run: |
          az webapp config container set \
            --name "${{ secrets.APP_SERVICE_NAME }}" \
            --resource-group "${{ secrets.AZURE_RESOURCE_GROUP }}" \
            --container-image-name "${{ secrets.ACR_NAME }}.azurecr.io/$IMAGE_REPO:${GITHUB_SHA::8}"

      # Force a pull of the new tag; EF migrations run on container startup.
      - name: Restart Web App
        run: |
          az webapp restart \
            --name "${{ secrets.APP_SERVICE_NAME }}" \
            --resource-group "${{ secrets.AZURE_RESOURCE_GROUP }}"

      - name: Resolve app URL
        id: app-url
        run: |
          HOST=$(az webapp show \
            --name "${{ secrets.APP_SERVICE_NAME }}" \
            --resource-group "${{ secrets.AZURE_RESOURCE_GROUP }}" \
            --query defaultHostName -o tsv)
          echo "url=https://$HOST" >> "$GITHUB_OUTPUT"
```

- [ ] **Step 2: Validate the workflow**

Run:

```bash
brew install actionlint 2>/dev/null || true
actionlint .github/workflows/devtest.yml
```

Expected: no output (clean). If `actionlint` cannot be installed, fall back to a YAML syntax check: `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/devtest.yml'))"` (expect silence).

- [ ] **Step 3: Commit (do NOT push)**

```bash
git add .github/workflows/devtest.yml
git commit -m "Rewrite GitHub Actions pipeline for parity with GitLab CI"
```

---

### Task 2: Add `.github/workflows/codeql.yml`

**Files:**
- Create: `.github/workflows/codeql.yml`

**Interfaces:**
- Produces: workflow `codeql` — the SAST-stage equivalent of GitLab's `Security/SAST.gitlab-ci.yml` include. Standalone; nothing else depends on it.

- [ ] **Step 1: Create the file**

Create `.github/workflows/codeql.yml` with:

```yaml
# SAST for AppointMe (GitHub Actions) — parity with the GitLab pipeline's
# Security/SAST.gitlab-ci.yml include. Results appear under
# Security → Code scanning. Free for public repos.

name: codeql

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
  schedule:
    - cron: '30 5 * * 1'

permissions:
  contents: read

jobs:
  analyze:
    name: Analyze (${{ matrix.language }})
    runs-on: ubuntu-latest
    permissions:
      security-events: write
      packages: read
      actions: read
      contents: read
    strategy:
      fail-fast: false
      matrix:
        include:
          - language: csharp
            build-mode: none
          - language: javascript-typescript
            build-mode: none
    steps:
      - uses: actions/checkout@v4

      - name: Initialize CodeQL
        uses: github/codeql-action/init@v3
        with:
          languages: ${{ matrix.language }}
          build-mode: ${{ matrix.build-mode }}

      - name: Perform CodeQL Analysis
        uses: github/codeql-action/analyze@v3
        with:
          category: "/language:${{ matrix.language }}"
```

- [ ] **Step 2: Validate the workflow**

Run: `actionlint .github/workflows/codeql.yml`
Expected: no output (clean).

- [ ] **Step 3: Commit (do NOT push)**

```bash
git add .github/workflows/codeql.yml
git commit -m "Add CodeQL workflow (SAST parity with GitLab pipeline)"
```

---

### Task 3: Azure federated credentials + role verification

No repo files change in this task — it is `az` CLI work against the `AppointMe-DevTest` subscription. The GitLab federated credential (`gitlab-main`) must remain untouched.

**Interfaces:**
- Consumes: GitHub environment name `devtest` and repo `bravodev-hub/appointme` as referenced by Task 1's workflow.
- Produces: federated credentials `github-main` and `github-env-devtest` on `id-appointme-devtest-ci`; verified role assignments for the identity.

- [ ] **Step 1: Create the two federated credentials**

```bash
SUB=9187eacf-3a7f-4877-98fe-7f6b4b25ff5c

# For the build-image job (no environment → subject is the branch ref)
az identity federated-credential create \
  --identity-name id-appointme-devtest-ci \
  --resource-group rg-appointme-devtest \
  --subscription $SUB \
  --name github-main \
  --issuer https://token.actions.githubusercontent.com \
  --subject "repo:bravodev-hub/appointme:ref:refs/heads/main" \
  --audiences api://AzureADTokenExchange

# For the deploy-devtest job (jobs with `environment:` get a DIFFERENT subject)
az identity federated-credential create \
  --identity-name id-appointme-devtest-ci \
  --resource-group rg-appointme-devtest \
  --subscription $SUB \
  --name github-env-devtest \
  --issuer https://token.actions.githubusercontent.com \
  --subject "repo:bravodev-hub/appointme:environment:devtest" \
  --audiences api://AzureADTokenExchange
```

Expected: each command returns JSON containing the new credential's `name` and `subject`.

- [ ] **Step 2: Verify all three credentials exist**

```bash
az identity federated-credential list \
  --identity-name id-appointme-devtest-ci \
  --resource-group rg-appointme-devtest \
  --subscription $SUB \
  --query "[].{name:name, subject:subject}" -o table
```

Expected: three rows — `gitlab-main`, `github-main`, `github-env-devtest`.

- [ ] **Step 3: Verify role assignments by principalId**

(The design-phase check wrongly queried by clientId; role assignments hang off the principalId.)

```bash
PRINCIPAL_ID=$(az identity show -n id-appointme-devtest-ci -g rg-appointme-devtest \
  --subscription $SUB --query principalId -o tsv)
az role assignment list --assignee $PRINCIPAL_ID --subscription $SUB --all \
  --query "[].{role:roleDefinitionName, scope:scope}" -o table
```

Expected: `Contributor` scoped to `.../registries/acrappointmedevtestze5tkm` and `Website Contributor` scoped to `.../sites/app-appointme-devtest-ze5tkm`. **If either is missing**, create it:

```bash
ACR_ID=$(az acr show -n acrappointmedevtestze5tkm --subscription $SUB --query id -o tsv)
APP_ID=$(az webapp show -n app-appointme-devtest-ze5tkm -g rg-appointme-devtest --subscription $SUB --query id -o tsv)
az role assignment create --assignee $PRINCIPAL_ID --role Contributor --scope $ACR_ID --subscription $SUB
az role assignment create --assignee $PRINCIPAL_ID --role "Website Contributor" --scope $APP_ID --subscription $SUB
```

---

### Task 4: gh CLI install, auth, secrets, environment

**Interfaces:**
- Consumes: secret names from Task 1's workflow; Azure values from Global Constraints.
- Produces: six repo Actions secrets and the `devtest` environment on `bravodev-hub/appointme`.

- [ ] **Step 1: Install gh**

Run: `brew install gh`
Expected: `gh --version` prints a version.

- [ ] **Step 2: Authenticate (USER ACTION REQUIRED)**

Run `gh auth status`. If not authenticated, **stop and ask the user** to run:

```
! gh auth login --web --hostname github.com
```

then re-run `gh auth status`. Expected: `Logged in to github.com`. Do not proceed until it succeeds.

- [ ] **Step 3: Create the six repo secrets**

```bash
REPO=bravodev-hub/appointme
SUB=9187eacf-3a7f-4877-98fe-7f6b4b25ff5c
TENANT=$(az account show --subscription $SUB --query tenantId -o tsv)

gh secret set AZURE_CLIENT_ID       --repo $REPO --body "6d4ca079-55eb-4ab2-ac6e-d33e72aa8352"
gh secret set AZURE_TENANT_ID       --repo $REPO --body "$TENANT"
gh secret set AZURE_SUBSCRIPTION_ID --repo $REPO --body "$SUB"
gh secret set AZURE_RESOURCE_GROUP  --repo $REPO --body "rg-appointme-devtest"
gh secret set ACR_NAME              --repo $REPO --body "acrappointmedevtestze5tkm"
gh secret set APP_SERVICE_NAME      --repo $REPO --body "app-appointme-devtest-ze5tkm"
```

- [ ] **Step 4: Verify secrets and create the devtest environment**

```bash
gh secret list --repo $REPO
gh api -X PUT repos/$REPO/environments/devtest --silent && echo "environment devtest OK"
```

Expected: all six secret names listed; `environment devtest OK` printed.

---

### Task 5: Document GitHub OIDC in `infra/README.md`

**Files:**
- Modify: `infra/README.md` (insert new section after "### 3. GitLab CI/CD variables", i.e. immediately before the `---` / "## First deploy" heading)

**Interfaces:**
- Consumes: commands and values from Tasks 3–4 (documentation of what they did).

- [ ] **Step 1: Insert the GitHub OIDC section**

Insert the following markdown after the end of section "### 3. GitLab CI/CD variables" (after the paragraph "The pipeline derives the ACR login server as `$ACR_NAME.azurecr.io`, so no separate variable is needed for it.") and before the `---` that precedes "## First deploy":

````markdown
### 4. Bootstrap GitHub OIDC federated identity

The GitHub Actions pipeline (`.github/workflows/devtest.yml`) authenticates the
same way — OIDC federated credentials on the **same** CI identity created in
step 2. Both CI providers are supported side by side; add whichever federated
credentials match where you host the repo.

GitHub needs **two** federated credentials, because a job that targets a GitHub
*environment* presents a different `sub` claim than a plain branch job:

```bash
# For the build-image job (branch-scoped subject)
az identity federated-credential create \
  --identity-name id-appointme-devtest-ci \
  --resource-group rg-appointme-devtest \
  --name github-main \
  --issuer https://token.actions.githubusercontent.com \
  --subject "repo:<owner>/<repo>:ref:refs/heads/main" \
  --audiences api://AzureADTokenExchange

# For the deploy-devtest job (environment-scoped subject)
az identity federated-credential create \
  --identity-name id-appointme-devtest-ci \
  --resource-group rg-appointme-devtest \
  --name github-env-devtest \
  --issuer https://token.actions.githubusercontent.com \
  --subject "repo:<owner>/<repo>:environment:devtest" \
  --audiences api://AzureADTokenExchange
```

The identity's role assignments from step 2 (Contributor on the ACR, Website
Contributor on the Web App) cover the GitHub pipeline too — nothing extra to
grant.

### 5. GitHub Actions secrets

Add these under **Settings → Secrets and variables → Actions** (or via
`gh secret set`). Names are identical to the GitLab variables in step 3:

| Secret                  | Value                                                        |
| ----------------------- | ------------------------------------------------------------ |
| `AZURE_CLIENT_ID`       | `clientId` of the user-assigned identity (`az identity show -n id-appointme-devtest-ci -g rg-appointme-devtest --query clientId -o tsv`) |
| `AZURE_TENANT_ID`       | Your Azure tenant ID                                         |
| `AZURE_SUBSCRIPTION_ID` | Subscription holding the devtest resource group              |
| `AZURE_RESOURCE_GROUP`  | `rg-appointme-devtest`                                       |
| `ACR_NAME`              | ACR name from Bicep output `containerRegistryName` (no `.azurecr.io`) |
| `APP_SERVICE_NAME`      | Web App name from Bicep output (no URL, just the name)       |

The deploy job targets a GitHub environment named `devtest`; it is created
automatically on first deploy (or pre-create it under **Settings →
Environments** to attach protection rules).
````

- [ ] **Step 2: Also update the intro of section 2**

In section "### 2. Bootstrap GitLab OIDC federated identity", the identity-creation part is shared by both providers. Change only the opening sentence from:

```markdown
The GitLab pipeline (`.gitlab-ci.yml`) authenticates to Azure with OIDC federated credentials — no static client secrets in the repo or CI.
```

to:

```markdown
The GitLab pipeline (`.gitlab-ci.yml`) authenticates to Azure with OIDC federated credentials — no static client secrets in the repo or CI. (The GitHub pipeline reuses the same identity — see step 4.)
```

- [ ] **Step 3: Commit (do NOT push)**

```bash
git add infra/README.md
git commit -m "Document GitHub OIDC setup alongside GitLab in infra README"
```

---

### Task 6: End-to-end verification

**Interfaces:**
- Consumes: everything above. Only run after Tasks 1–5 are ALL complete.

- [ ] **Step 1: Push main**

```bash
git push origin main
```

- [ ] **Step 2: Watch the devtest run**

```bash
gh run list --repo bravodev-hub/appointme --workflow devtest --limit 1
gh run watch --repo bravodev-hub/appointme --exit-status $(gh run list --repo bravodev-hub/appointme --workflow devtest --limit 1 --json databaseId -q '.[0].databaseId')
```

Expected: jobs `test`, `build-image`, `deploy-devtest` all succeed. If `azure/login` fails with `AADSTS70021` (no matching federated identity record), the credential subject and the workflow's ref/environment are out of sync — re-check Task 3 subjects character by character.

- [ ] **Step 3: Confirm the Web App serves the new image**

```bash
az webapp config container show \
  --name app-appointme-devtest-ze5tkm \
  --resource-group rg-appointme-devtest \
  --subscription 9187eacf-3a7f-4877-98fe-7f6b4b25ff5c \
  --query "[?name=='DOCKER_CUSTOM_IMAGE_NAME'].value" -o tsv
curl -s -o /dev/null -w "%{http_code}\n" --max-time 120 https://app-appointme-devtest-ze5tkm.azurewebsites.net/
```

Expected: image value ends with `appointme-api:<the pushed short sha>`; curl returns `200` (allow a couple of minutes for the container to warm up and run EF migrations — retry the curl a few times before concluding failure).

- [ ] **Step 4: Check the CodeQL run started**

```bash
gh run list --repo bravodev-hub/appointme --workflow codeql --limit 1
```

Expected: a run exists (in progress or completed). If the run fails with "code scanning is not enabled", enable it: **Settings → Advanced Security → Code scanning** (public repos: free) and re-run.
