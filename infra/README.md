# AppointMe — Azure infrastructure & deployment

How to deploy your own copy of AppointMe to Azure. Everything is provisioned
by the Bicep templates in this folder and deployed continuously by GitHub
Actions (`.github/workflows/devtest.yml`): every push to `main` builds the
container image and updates the Web App.

Follow the steps in order — each one uses outputs from the previous.

## What you get

A single `devtest` environment sized for demos and evaluation (the App
Service plan is free; SQL is the smallest paid tier):

| Module                          | Resource                                                    |
| ------------------------------- | ----------------------------------------------------------- |
| `modules/log-analytics.bicep`   | Log Analytics workspace (PerGB2018, 30-day retention, 0.5 GB/day ingestion cap) |
| `modules/app-insights.bicep`    | Workspace-linked Application Insights                       |
| `modules/key-vault.bicep`       | Key Vault (RBAC mode, standard tier, soft-delete on)        |
| `modules/sql.bicep`             | Azure SQL Server + database (`Basic`) + `Allow Azure services` firewall rule |
| `modules/container-registry.bicep` | Azure Container Registry (Basic)                          |
| `modules/storage.bicep`         | Storage Account + private blob container `data-protection-keys` |
| `modules/app-service-plan.bicep` | Linux App Service Plan (F1, free)                          |
| `modules/app-service.bicep`     | Web App for Containers + system-assigned managed identity + Key Vault references for secrets |
| `modules/role-assignments.bicep` | `AcrPull` + `Key Vault Secrets User` on the App Service identity |
| `modules/custom-domain.bicep` + `custom-domain-ssl-binding.bicep` | *(optional, B1+ plans only)* hostname binding + free App Service Managed Certificate — see [Custom domain](#optional-custom-domain) |

Deliberately **not** in the Bicep:

- **Microsoft Entra External ID tenant** and app registration — tenant creation is portal-only (step 2).
- **Key Vault secret values** — you seed them once after provisioning (step 3).
- **Keycloak** — local development only; `Authentication:Provider` selects `Keycloak` (local) or `EntraExternalId` (deployed).
- **WAF, private endpoints, VNet integration, deployment slots** — see [the prod-hardening checklist](#prod-hardening-checklist).

**What to expect on the free (F1) plan:** the app unloads after ~20 minutes
idle and the next request cold-starts the container (30–90 s, EF migrations
included), and Azure enforces a daily CPU quota (HTTP 403 until midnight UTC
when exhausted). Fine for demos; move to B1+ when that hurts.

## Prerequisites

- An Azure subscription and the `az` CLI logged into it (`az login`).
- Your clone of this repo on GitHub, with permission to manage repo settings
  (`gh` CLI optional but handy).

## 1. Provision the infrastructure

```bash
# Resource group — pick your region
az group create --name rg-appointme-devtest --location westeurope

# SQL admin password via env var (never commit real values)
export SQL_ADMIN_PASSWORD='<a-strong-password>'

# Optional overrides read by main.bicepparam:
#   INITIAL_CONTAINER_IMAGE — placeholder image for the very first deploy
#                             (default: mcr.microsoft.com/azuredocs/aci-helloworld:latest)
#   CUSTOM_HOSTNAME         — binds a custom domain via App Service Managed
#                             Certificate. Requires a B1+ plan; leave UNSET on
#                             the default F1 plan (see "Custom domain" below).

# Preview
az deployment group what-if \
  --resource-group rg-appointme-devtest \
  --template-file infra/main.bicep \
  --parameters infra/main.bicepparam

# Apply — note the outputs; later steps use them
az deployment group create \
  --resource-group rg-appointme-devtest \
  --template-file infra/main.bicep \
  --parameters infra/main.bicepparam \
  --query 'properties.outputs'
```

The Web App starts with a public hello-world placeholder image so it can run
before your first CI build; step 6 replaces it.

## 2. Set up Microsoft Entra External ID

1. Create an External ID tenant in the Azure portal: **Microsoft Entra ID → External ID → Create a new tenant**. Choose a subdomain `<tenant>.ciamlogin.com`.
2. In the new tenant, **App registrations → New registration**:
   - Name: e.g. `appointme-api-devtest`
   - Supported account types: **Accounts in any organizational directory and personal Microsoft accounts** (or restricted, per your policy).
   - Redirect URI (Web): `https://<your-app-service>.azurewebsites.net/signin-oidc` (the hostname is in step 1's outputs). If you'll add a [custom domain](#optional-custom-domain), also register `https://<your-public-host>/signin-oidc`.
3. **Authentication → Implicit grant and hybrid flows** — leave both off (authorization code flow is used).
4. **Certificates & secrets → New client secret** — create one and copy the value immediately (you'll store it in Key Vault in step 3).
5. **Expose an API → Add a scope** — define a scope like `access_as_user`. Note the App ID URI (e.g. `api://<client-id>`) — this is your `ApiAudience`.
6. **User flows → New user flow** — create a Sign-up and sign-in flow and wire it to the app registration.

Put the non-secret identifiers into `src/AppointMe.Api/appsettings.Devtest.json`:

```bash
cp src/AppointMe.Api/appsettings.Devtest.example.json src/AppointMe.Api/appsettings.Devtest.json
```

| Value           | Where it comes from                                |
| --------------- | -------------------------------------------------- |
| `Authority`     | `https://<tenant>.ciamlogin.com/<tenantId>/v2.0`  |
| `ClientId`      | App registration → Application (client) ID         |
| `ApiAudience`   | Expose an API → Application ID URI                 |

The **client secret is never committed** — it lives only in Key Vault and reaches the app as the `Authentication__EntraExternalId__ClientSecret` setting via a Key Vault reference.

## 3. Seed Key Vault

The App Service reads secrets via Key Vault references. Populate these once:

| Secret name                    | Value                                                                                                     |
| ------------------------------ | -------------------------------------------------------------------------------------------------------- |
| `AppointMeSql`                 | Full SQL connection string: `Server=tcp:<server>.database.windows.net,1433;Initial Catalog=appointme;User ID=<login>;Password=<password>;Encrypt=True;TrustServerCertificate=False;` |
| `DataProtectionStorage`        | `az storage account show-connection-string --resource-group rg-appointme-devtest --name <storage-name> --query connectionString -o tsv` |
| `EntraExternalIdClientSecret`  | Client secret value from step 2.4                                                                         |

```bash
KV=$(az deployment group show -g rg-appointme-devtest -n main --query 'properties.outputs.keyVaultName.value' -o tsv)

az keyvault secret set --vault-name "$KV" --name AppointMeSql --value "..."
az keyvault secret set --vault-name "$KV" --name DataProtectionStorage --value "..."
az keyvault secret set --vault-name "$KV" --name EntraExternalIdClientSecret --value "..."

# Restart so the Web App picks up the Key Vault references
az webapp restart --name <app-service-name> --resource-group rg-appointme-devtest
```

## 4. Create the CI deployer identity

GitHub Actions authenticates to Azure with OIDC federated credentials on a
user-assigned managed identity — no static credentials in the repo or CI.

```bash
az identity create \
  --name id-appointme-devtest-ci \
  --resource-group rg-appointme-devtest

PRINCIPAL_ID=$(az identity show -n id-appointme-devtest-ci -g rg-appointme-devtest --query principalId -o tsv)

# Build images in ACR. `az acr build` runs a server-side ACR Task, which needs
# control-plane access (read the registry + schedule the build run) — AcrPush is
# data-plane only and is NOT sufficient. Contributor scoped to the single registry
# covers it. (If you switch the pipeline to docker build + push, AcrPush is enough.)
az role assignment create --assignee $PRINCIPAL_ID --role Contributor --scope $(az acr show -n <acr-name> -g rg-appointme-devtest --query id -o tsv)

# Update the Web App's container settings
az role assignment create --assignee $PRINCIPAL_ID --role "Website Contributor" --scope $(az webapp show -n <web-app-name> -g rg-appointme-devtest --query id -o tsv)
```

Use the `<acr-name>` and `<web-app-name>` from step 1's outputs.

## 5. Wire up GitHub Actions

**Enable Actions** on your repo first (**Settings → Actions**) — fresh
organization repos may have it disabled, which leaves runs stuck in `queued`.

**Federated credentials.** Two are needed, because a job that targets a
GitHub *environment* presents a different `sub` claim than a plain branch job:

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

The `--audiences` value must match what `azure/login` requests
(`api://AzureADTokenExchange`, its default).

**Actions secrets** — under **Settings → Secrets and variables → Actions**
(or `gh secret set`):

| Secret                  | Value                                                        |
| ----------------------- | ------------------------------------------------------------ |
| `AZURE_CLIENT_ID`       | `clientId` of the identity (`az identity show -n id-appointme-devtest-ci -g rg-appointme-devtest --query clientId -o tsv`) |
| `AZURE_TENANT_ID`       | Azure tenant ID **of the tenant holding your subscription** (`az account show --query tenantId -o tsv`) |
| `AZURE_SUBSCRIPTION_ID` | Your subscription ID                                         |
| `AZURE_RESOURCE_GROUP`  | `rg-appointme-devtest`                                       |
| `ACR_NAME`              | ACR name from step 1's `containerRegistryName` output (no `.azurecr.io`) |
| `APP_SERVICE_NAME`      | Web App name from step 1's output (just the name, no URL)    |

The pipeline derives the ACR login server as `$ACR_NAME.azurecr.io`.

**Actions variables** — optional, same settings page:

| Variable         | Value                                                              |
| ---------------- | ------------------------------------------------------------------ |
| `APP_PUBLIC_URL` | Public URL shown on the deployments page (e.g. `https://app.example.com`). A *variable*, not a secret — GitHub refuses secret-derived environment URLs. |

The deploy job targets a GitHub environment named `devtest`; it is created
automatically on first deploy (or pre-create it under **Settings →
Environments** to attach protection rules).

## 6. Push to deploy

Push (or merge) to `main`. The `devtest` workflow runs three jobs: build and
test → build the container image in ACR (tagged with the commit's short SHA,
also shown in the app's footer) → point the Web App at the new image and
restart it. EF migrations run on container startup before the API serves
traffic.

Verify: the run is green, and `https://<your-app-service>.azurewebsites.net`
responds (allow a couple of minutes for the first container start).

## Optional: custom domain

**On the default F1 plan** App Service hostname bindings are unavailable, so
the custom domain terminates at the edge instead. This template ships a
Cloudflare Worker for that (requires your DNS on Cloudflare — free plan is
enough):

- Edit `infra/cloudflare-worker/` — set your public hostname and
  azurewebsites origin in `src/index.js` and the route in `wrangler.jsonc` —
  then `npx wrangler deploy`.
- The Worker forwards requests to the origin and carries the public hostname
  in an `X-Original-Host` header; the app maps it via
  `ForwardedHeadersOptions` in `Program.cs`, keeping OIDC redirects and
  generated URLs on your domain.
- Register `https://<your-public-host>/signin-oidc` as a redirect URI
  (step 2.2) and set the `APP_PUBLIC_URL` variable (step 5).

**On B1 or higher** skip the Worker: set `CUSTOM_HOSTNAME` when deploying the
Bicep (step 1) and `modules/custom-domain.bicep` binds the domain with a free
App Service Managed Certificate. DNS preconditions are documented in that
module's header.

## Messaging (Wolverine)

The deployed app runs `Wolverine:Transport=SqlDurable` (set in
`appsettings.Devtest.json`): durable local queues on top of the SQL outbox.
Messages survive restarts and no broker is required.

To use Azure Service Bus instead: set `Wolverine:Transport=AzureServiceBus`,
provision a Service Bus namespace, seed an `AppointMeMessaging` connection
string secret in Key Vault, and add a `ConnectionStrings__AppointMeMessaging`
Key Vault reference to `infra/modules/app-service.bicep`. Wolverine
auto-provisions queues on startup (`.AutoProvision()` in
`WolverineHostBuilderExtensions.cs`); the SQL outbox stays active as the
durability layer either way.

## Prod-hardening checklist

When promoting beyond a demo environment:

- [ ] App Service Plan → S1 or P1v3 for deployment slots and zone-redundancy
- [ ] Azure SQL → Entra-only auth (drop the SQL login + password), enable Microsoft Defender for SQL, tune backup retention
- [ ] Private endpoints for SQL, Storage, Key Vault, ACR (and Service Bus, if you use it)
- [ ] VNet integration for the App Service
- [ ] WAF (Azure Front Door or App Gateway) in front of the App Service
- [ ] Customer-managed keys for Storage and Key Vault
- [ ] Custom domain + managed TLS
- [ ] Multi-region: Front Door + paired-region App Service + SQL geo-replication
- [ ] Tighten SQL firewall — remove "Allow Azure services" once VNet/private endpoints land
- [ ] Replace the Hangfire dashboard email allow-list with a role/permission integration once roles are projected into JWT claims
- [ ] Set Key Vault `enablePurgeProtection: true`
- [ ] Storage account `allowSharedKeyAccess: false` — switch Data Protection to managed identity blob access
