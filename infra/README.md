# AppointMe — Azure devtest infrastructure

Bicep modules that provision a single `devtest` environment for AppointMe.
Deployment runs from **GitHub Actions** (`.github/workflows/devtest.yml`); a
GitLab pipeline (`.gitlab-ci.yml`) is maintained as an alternative for teams
hosting the template on GitLab — see [GitLab setup](#4-gitlab-setup-alternative).

## What this provisions

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
| `modules/custom-domain.bicep` + `custom-domain-ssl-binding.bicep` | *(optional, B1+ plans only)* hostname binding + free App Service Managed Certificate — see [Custom domain](#custom-domain-on-the-free-tier) |

## What this does NOT provision

Outside the Bicep deliberately:

- **Microsoft Entra External ID tenant**, user flows, and app registration. Tenant creation is portal-only; app registration on an existing tenant is doable via the Graph API but noisy in Bicep. See [Set up Entra External ID](#1-set-up-microsoft-entra-external-id) below.
- **Keycloak** — local dev only. The app's `Authentication:Provider` config selects between `Keycloak` (local) and `EntraExternalId` (devtest/prod).
- **Secrets in Key Vault** — the operator (or a follow-up script) seeds them after first deploy.
- **Custom domain + DNS** — on F1, App Service custom-domain bindings are not available; devtest serves the custom domain through a Cloudflare Worker instead — see [Custom domain](#custom-domain-on-the-free-tier).
- **WAF, private endpoints, VNet integration, customer-managed keys** — production hardening, see [the prod-hardening checklist](#prod-hardening-checklist).
- **Deployment slots** — requires Standard (S1) plan or higher; devtest runs F1.

---

## One-time setup

### 1. Set up Microsoft Entra External ID

1. Create an External ID tenant in the Azure portal: **Microsoft Entra ID → External ID → Create a new tenant**. Choose a subdomain `<tenant>.ciamlogin.com`.
2. In the new tenant, **App registrations → New registration**:
   - Name: `appointme-api-devtest`
   - Supported account types: **Accounts in any organizational directory and personal Microsoft accounts** (or restricted, per your policy).
   - Redirect URI (Web): `https://<your-app-service>.azurewebsites.net/signin-oidc` — leave blank for now if you haven't deployed yet; update after first deploy. If you front the app with a custom domain (see [Custom domain](#custom-domain-on-the-free-tier)), also register `https://<your-public-host>/signin-oidc`.
3. **Authentication → Implicit grant and hybrid flows** — leave both off (we use authorization code flow).
4. **Certificates & secrets → New client secret** — create one, copy the value immediately (paste into Key Vault when [seeding secrets](#seed-key-vault)).
5. **Expose an API → Add a scope** — define a scope like `access_as_user`. Note the App ID URI (e.g. `api://<client-id>`) — this is your `ApiAudience`.
6. **User flows → New user flow** — create a Sign-up and sign-in flow. Wire it to the app registration.

The non-secret identifiers (authority, client ID, API audience, tenant ID) go in `src/AppointMe.Api/appsettings.Devtest.json`. Copy the template and fill in your tenant's values:

```bash
cp src/AppointMe.Api/appsettings.Devtest.example.json src/AppointMe.Api/appsettings.Devtest.json
```

| Value           | Where it comes from                                |
| --------------- | -------------------------------------------------- |
| `Authority`     | `https://<tenant>.ciamlogin.com/<tenantId>/v2.0`  |
| `ClientId`      | App registration → Application (client) ID         |
| `ApiAudience`   | Expose an API → Application ID URI                 |

The **client secret is never committed** — it lives only in Key Vault and is injected as the `Authentication__EntraExternalId__ClientSecret` app setting via a Key Vault reference.

### 2. Create the CI deployer identity

Both CI providers authenticate to Azure with OIDC federated credentials on one
user-assigned managed identity — no static client secrets in the repo or CI.

```bash
# Create a user-assigned managed identity for the CI deployer
az identity create \
  --name id-appointme-devtest-ci \
  --resource-group rg-appointme-devtest
```

Grant the identity the roles it needs to do its job:

```bash
PRINCIPAL_ID=$(az identity show -n id-appointme-devtest-ci -g rg-appointme-devtest --query principalId -o tsv)

# Build images in ACR. `az acr build` runs a server-side ACR Task, which needs
# control-plane access (read the registry + schedule the build run) — AcrPush is
# data-plane only and is NOT sufficient. Contributor scoped to the single registry
# covers it. (If you switch the pipeline to docker build + push, AcrPush is enough.)
az role assignment create --assignee $PRINCIPAL_ID --role Contributor --scope $(az acr show -n <acr-name> -g rg-appointme-devtest --query id -o tsv)

# Update the web app's container settings
az role assignment create --assignee $PRINCIPAL_ID --role "Website Contributor" --scope $(az webapp show -n <web-app-name> -g rg-appointme-devtest --query id -o tsv)
```

### 3. GitHub setup

**Federated credentials.** GitHub needs **two**, because a job that targets a
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

The `--audiences` value must match what `azure/login` requests (`api://AzureADTokenExchange`, its default).

**Actions secrets** — add under **Settings → Secrets and variables → Actions**
(or via `gh secret set`):

| Secret                  | Value                                                        |
| ----------------------- | ------------------------------------------------------------ |
| `AZURE_CLIENT_ID`       | `clientId` of the user-assigned identity (`az identity show -n id-appointme-devtest-ci -g rg-appointme-devtest --query clientId -o tsv`) |
| `AZURE_TENANT_ID`       | Azure tenant ID **of the tenant holding the devtest subscription** (`az account show --subscription <sub-id> --query tenantId -o tsv`) |
| `AZURE_SUBSCRIPTION_ID` | Subscription holding the devtest resource group              |
| `AZURE_RESOURCE_GROUP`  | `rg-appointme-devtest`                                       |
| `ACR_NAME`              | ACR name from Bicep output `containerRegistryName` (no `.azurecr.io`) |
| `APP_SERVICE_NAME`      | Web App name from Bicep output (no URL, just the name)       |

The pipeline derives the ACR login server as `$ACR_NAME.azurecr.io`, so no separate secret is needed for it.

**Actions variables** — optional, same settings page:

| Variable         | Value                                                              |
| ---------------- | ------------------------------------------------------------------ |
| `APP_PUBLIC_URL` | Public URL shown on the deployments page (e.g. `https://app.example.com`). A *variable*, not a secret — GitHub refuses secret-derived environment URLs. |

**Environment.** The deploy job targets a GitHub environment named `devtest`;
it is created automatically on first deploy (or pre-create it under
**Settings → Environments** to attach protection rules). GitHub Actions must
be enabled on the repo (**Settings → Actions**) — fresh org repos may have it
disabled, which leaves dispatched runs stuck in `queued` forever.

### 4. GitLab setup (alternative)

The GitLab pipeline (`.gitlab-ci.yml`) reuses the same identity from step 2 —
add a GitLab federated credential alongside the GitHub ones (both CI providers
work side by side):

```bash
# GitLab's ID-token `sub` claim format is: project_path:<group>/<project>:ref_type:branch:ref:<branch>
az identity federated-credential create \
  --identity-name id-appointme-devtest-ci \
  --resource-group rg-appointme-devtest \
  --name gitlab-main \
  --issuer https://gitlab.com \
  --subject "project_path:<group>/<project>:ref_type:branch:ref:main" \
  --audiences api://AzureADTokenExchange
```

The `--audiences` value must match the `aud` requested in the `id_tokens`
block of `.gitlab-ci.yml` (`api://AzureADTokenExchange`).

**CI/CD variables** — add under **Settings → CI/CD → Variables**, same names
and values as the GitHub secrets table above (`AZURE_CLIENT_ID`,
`AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_RESOURCE_GROUP`,
`ACR_NAME`, `APP_SERVICE_NAME`). Mark them **Protected** (make sure `main` is
a protected branch) and **Masked** where the value allows.

---

## First deploy

```bash
# 1. Create the resource group
az group create --name rg-appointme-devtest --location westeurope

# 2. Provide the SQL admin password via env var (do NOT commit real values)
export SQL_ADMIN_PASSWORD='<a-strong-password>'

# Optional overrides read by main.bicepparam:
#   INITIAL_CONTAINER_IMAGE — placeholder image for the very first deploy
#                             (default: mcr.microsoft.com/azuredocs/aci-helloworld:latest)
#   CUSTOM_HOSTNAME         — binds a custom domain via App Service Managed
#                             Certificate. Requires a B1+ plan; leave UNSET on
#                             the default F1 plan (bindings are not supported
#                             there — see "Custom domain on the free tier").

# 3. Preview the plan
az deployment group what-if \
  --resource-group rg-appointme-devtest \
  --template-file infra/main.bicep \
  --parameters infra/main.bicepparam

# 4. Apply
az deployment group create \
  --resource-group rg-appointme-devtest \
  --template-file infra/main.bicep \
  --parameters infra/main.bicepparam \
  --query 'properties.outputs'
```

The first deploy uses a public hello-world container as a placeholder so App Service can start before any AppointMe image exists. CI replaces the image on the next `main` push.

## Custom domain on the free tier

The F1 plan does not allow App Service hostname bindings, so devtest serves
its custom domain entirely at the Cloudflare edge:

- Cloudflare terminates TLS for the domain (proxied DNS record).
- A Worker (`infra/cloudflare-worker/`, deploy with `npx wrangler deploy`)
  intercepts `your-host/*`, forwards to the `*.azurewebsites.net` origin, and
  carries the public hostname in an `X-Original-Host` header.
- The app maps that header via `ForwardedHeadersOptions` in `Program.cs`, so
  OIDC redirects and generated URLs stay on the public domain.
- Set the `APP_PUBLIC_URL` Actions variable (step 3) so the GitHub
  deployments page links to the right URL.

On a B1+ plan you can skip all of this: set `CUSTOM_HOSTNAME` and let
`modules/custom-domain.bicep` bind the domain with a free App Service Managed
Certificate.

## Seed Key Vault

The App Service reads secrets via Key Vault references. Populate these secret names after the first deploy:

| Secret name                    | How to populate                                                                                          |
| ------------------------------ | -------------------------------------------------------------------------------------------------------- |
| `AppointMeSql`                 | Full SQL connection string: `Server=tcp:<server>.database.windows.net,1433;Initial Catalog=appointme;User ID=<login>;Password=<password>;Encrypt=True;TrustServerCertificate=False;` |
| `DataProtectionStorage`        | `az storage account show-connection-string --resource-group rg-appointme-devtest --name <storage-name> --query connectionString -o tsv` |
| `EntraExternalIdClientSecret`  | Client secret value from the External ID app registration (step 1.4)                                     |

Example seeding:

```bash
KV=$(az deployment group show -g rg-appointme-devtest -n main --query 'properties.outputs.keyVaultName.value' -o tsv)

az keyvault secret set --vault-name "$KV" --name AppointMeSql --value "..."
az keyvault secret set --vault-name "$KV" --name DataProtectionStorage --value "..."
az keyvault secret set --vault-name "$KV" --name EntraExternalIdClientSecret --value "..."

# Restart the App Service so it picks up Key Vault references
az webapp restart --name <app-service-name> --resource-group rg-appointme-devtest
```

After the restart, EF migrations run on container startup before the API starts serving.

## Wolverine in Azure

Devtest runs `Wolverine:Transport=SqlDurable` (set in
`src/AppointMe.Api/appsettings.Devtest.json`): durable local queues on top of
the SQL outbox. Messages survive restarts, and no broker is provisioned —
Azure Service Bus was removed from this template's devtest footprint to cut
cost.

To opt back into a real broker, set `Wolverine:Transport=AzureServiceBus`,
provision a Service Bus namespace, seed an `AppointMeMessaging` connection
string secret in Key Vault, and add the corresponding
`ConnectionStrings__AppointMeMessaging` Key Vault reference back to
`infra/modules/app-service.bicep`. Wolverine auto-provisions queues on
startup (`.AutoProvision()` in `WolverineHostBuilderExtensions.cs`); the SQL
outbox stays active as the durability layer either way.

## Prod-hardening checklist

When promoting beyond devtest:

- [ ] App Service Plan → S1 or P1v3 for deployment slots and zone-redundancy
- [ ] Azure SQL → Entra-only auth (drop the SQL login + password), enable Microsoft Defender for SQL, tune backup retention
- [ ] Private endpoints for SQL, Storage, Key Vault, ACR (and Service Bus, if you opt back into it)
- [ ] VNet integration for the App Service
- [ ] WAF (Azure Front Door or App Gateway) in front of the App Service
- [ ] Customer-managed keys for Storage and Key Vault
- [ ] Custom domain + managed TLS
- [ ] Multi-region: Front Door + paired-region App Service + SQL geo-replication
- [ ] Tighten SQL firewall — remove "Allow Azure services" once VNet/private endpoints land
- [ ] Replace the Hangfire dashboard email allow-list with a role/permission integration once roles are projected into JWT claims
- [ ] Set Key Vault `enablePurgeProtection: true`
- [ ] Storage account `allowSharedKeyAccess: false` — switch Data Protection to managed identity blob access
