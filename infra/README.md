# AppointMe — Azure devtest infrastructure

Bicep modules that provision a single `devtest` environment for AppointMe.

## What this provisions

| Module                          | Resource                                                    |
| ------------------------------- | ----------------------------------------------------------- |
| `modules/log-analytics.bicep`   | Log Analytics workspace (PerGB2018, 30-day retention)       |
| `modules/app-insights.bicep`    | Workspace-linked Application Insights                       |
| `modules/key-vault.bicep`       | Key Vault (RBAC mode, standard tier, soft-delete on)        |
| `modules/sql.bicep`             | Azure SQL Server + database (`S0`) + `Allow Azure services` firewall rule |
| `modules/container-registry.bicep` | Azure Container Registry (Basic)                          |
| `modules/storage.bicep`         | Storage Account + private blob container `data-protection-keys` |
| `modules/service-bus.bicep`     | Service Bus namespace (Standard)                            |
| `modules/app-service-plan.bicep` | Linux App Service Plan (B1)                                |
| `modules/app-service.bicep`     | Web App for Containers + system-assigned managed identity + Key Vault references for secrets |
| `modules/role-assignments.bicep` | `AcrPull` + `Key Vault Secrets User` on the App Service identity |

## What this does NOT provision

Outside the Bicep deliberately:

- **Microsoft Entra External ID tenant**, user flows, and app registration. Tenant creation is portal-only; app registration on an existing tenant is doable via the Graph API but noisy in Bicep. See [Set up Entra External ID](#1-set-up-microsoft-entra-external-id) below.
- **Keycloak** — local dev only. The app's `Authentication:Provider` config selects between `Keycloak` (local) and `EntraExternalId` (devtest/prod).
- **Secrets in Key Vault** — the operator (or a follow-up script) seeds them after first deploy.
- **Custom domain + DNS** — devtest uses the generated `*.azurewebsites.net` hostname.
- **WAF, private endpoints, VNet integration, customer-managed keys** — production hardening, see [the prod-hardening checklist](#prod-hardening-checklist).
- **Deployment slots** — requires Standard (S1) plan or higher; devtest runs B1.

---

## One-time setup

### 1. Set up Microsoft Entra External ID

1. Create an External ID tenant in the Azure portal: **Microsoft Entra ID → External ID → Create a new tenant**. Choose a subdomain `<tenant>.ciamlogin.com`.
2. In the new tenant, **App registrations → New registration**:
   - Name: `appointme-api-devtest`
   - Supported account types: **Accounts in any organizational directory and personal Microsoft accounts** (or restricted, per your policy).
   - Redirect URI (Web): `https://<your-app-service>.azurewebsites.net/signin-oidc` — leave blank for now if you haven't deployed yet; update after first deploy.
3. **Authentication → Implicit grant and hybrid flows** — leave both off (we use authorization code flow).
4. **Certificates & secrets → New client secret** — create one, copy the value immediately (paste into Key Vault in step 5).
5. **Expose an API → Add a scope** — define a scope like `access_as_user`. Note the App ID URI (e.g. `api://<client-id>`) — this is your `ApiAudience`.
6. **User flows → New user flow** — create a Sign-up and sign-in flow. Wire it to the app registration.

You'll need these values for Bicep + Key Vault:

| Value                          | Where it comes from                                |
| ------------------------------ | -------------------------------------------------- |
| `entraExternalIdAuthority`     | `https://<tenant>.ciamlogin.com/<tenantId>/v2.0`  |
| `entraExternalIdClientId`      | App registration → Application (client) ID         |
| `entraExternalIdApiAudience`   | Expose an API → Application ID URI                 |
| `EntraExternalIdClientSecret`  | Certificates & secrets → secret value (Key Vault) |

The non-secret identifiers (authority, client ID, API audience, tenant ID) go in `src/AppointMe.Api/appsettings.Devtest.json`. Copy the template and fill in your tenant's values:

```bash
cp src/AppointMe.Api/appsettings.Devtest.example.json src/AppointMe.Api/appsettings.Devtest.json
```

The **client secret is never committed** — it lives only in Key Vault (step 5) and is injected as the `Authentication__EntraExternalId__ClientSecret` app setting via a Key Vault reference.

### 2. Bootstrap GitLab OIDC federated identity

The GitLab pipeline (`.gitlab-ci.yml`) authenticates to Azure with OIDC federated credentials — no static client secrets in the repo or CI. (The GitHub pipeline reuses the same identity — see step 4.)

```bash
# Create a user-assigned managed identity for the CI deployer
az identity create \
  --name id-appointme-devtest-ci \
  --resource-group rg-appointme-devtest

# Subject claim restricts which pipeline can use the identity. Lock to the main branch.
# GitLab's ID-token `sub` claim format is: project_path:<group>/<project>:ref_type:branch:ref:<branch>
az identity federated-credential create \
  --identity-name id-appointme-devtest-ci \
  --resource-group rg-appointme-devtest \
  --name gitlab-main \
  --issuer https://gitlab.com \
  --subject "project_path:bravo-dev/appointme:ref_type:branch:ref:main" \
  --audiences api://AzureADTokenExchange
```

The `--audiences` value must match the `aud` requested in the `id_tokens` block of `.gitlab-ci.yml` (`api://AzureADTokenExchange`).

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

### 3. GitLab CI/CD variables

Add these under **Settings → CI/CD → Variables**. Mark them **Protected** (exposed only to protected branches/tags — make sure `main` is a protected branch), and **Masked** where the value allows:

| Variable                | Value                                                        |
| ----------------------- | ------------------------------------------------------------ |
| `AZURE_CLIENT_ID`       | `clientId` of the user-assigned identity (`az identity show -n id-appointme-devtest-ci -g rg-appointme-devtest --query clientId -o tsv`) |
| `AZURE_TENANT_ID`       | Your Azure tenant ID                                         |
| `AZURE_SUBSCRIPTION_ID` | Subscription holding the devtest resource group              |
| `AZURE_RESOURCE_GROUP`  | `rg-appointme-devtest`                                       |
| `ACR_NAME`              | ACR name from Bicep output `containerRegistryName` (no `.azurecr.io`) |
| `APP_SERVICE_NAME`      | Web App name from Bicep output (no URL, just the name)       |

The pipeline derives the ACR login server as `$ACR_NAME.azurecr.io`, so no separate variable is needed for it.

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
| `AZURE_TENANT_ID`       | Azure tenant ID **of the tenant holding the devtest subscription** (`az account show --subscription <sub-id> --query tenantId -o tsv`) |
| `AZURE_SUBSCRIPTION_ID` | Subscription holding the devtest resource group              |
| `AZURE_RESOURCE_GROUP`  | `rg-appointme-devtest`                                       |
| `ACR_NAME`              | ACR name from Bicep output `containerRegistryName` (no `.azurecr.io`) |
| `APP_SERVICE_NAME`      | Web App name from Bicep output (no URL, just the name)       |

The deploy job targets a GitHub environment named `devtest`; it is created
automatically on first deploy (or pre-create it under **Settings →
Environments** to attach protection rules).

---

## First deploy

```bash
# 1. Create the resource group
az group create --name rg-appointme-devtest --location uksouth

# 2. Provide secrets/params via env vars (do NOT commit real values)
export SQL_ADMIN_PASSWORD='<a-strong-password>'
export ENTRA_AUTHORITY='https://<tenant>.ciamlogin.com/<tenantId>/v2.0'
export ENTRA_CLIENT_ID='<client-id>'
export ENTRA_API_AUDIENCE='api://<client-id>'
export HANGFIRE_ADMINS='you@example.com,colleague@example.com'

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

The first deploy uses `mcr.microsoft.com/azuredocs/aci-helloworld:latest` as a placeholder so App Service can start before any AppointMe image exists. CI replaces the image on the next `main` push.

## Seed Key Vault

The App Service reads secrets via Key Vault references. Populate these secret names after the first deploy:

| Secret name                    | How to populate                                                                                          |
| ------------------------------ | -------------------------------------------------------------------------------------------------------- |
| `AppointMeSql`                 | Full SQL connection string: `Server=tcp:<server>.database.windows.net,1433;Initial Catalog=appointme;User ID=<login>;Password=<password>;Encrypt=True;TrustServerCertificate=False;` |
| `AppointMeMessaging`           | `az servicebus namespace authorization-rule keys list --resource-group rg-appointme-devtest --namespace-name <sb-name> --name RootManageSharedAccessKey --query primaryConnectionString -o tsv` |
| `DataProtectionStorage`        | `az storage account show-connection-string --resource-group rg-appointme-devtest --name <storage-name> --query connectionString -o tsv` |
| `EntraExternalIdClientSecret`  | Client secret value from the External ID app registration (step 1.4)                                     |

Example seeding:

```bash
KV=$(az deployment group show -g rg-appointme-devtest -n main --query 'properties.outputs.keyVaultName.value' -o tsv)

az keyvault secret set --vault-name "$KV" --name AppointMeSql --value "..."
az keyvault secret set --vault-name "$KV" --name AppointMeMessaging --value "..."
az keyvault secret set --vault-name "$KV" --name DataProtectionStorage --value "..."
az keyvault secret set --vault-name "$KV" --name EntraExternalIdClientSecret --value "..."

# Restart the App Service so it picks up Key Vault references
az webapp restart --name <app-service-name> --resource-group rg-appointme-devtest
```

After the restart, EF migrations run on container startup before the API starts serving.

## Wolverine in Azure

`Wolverine:Transport=AzureServiceBus` is set in App Service settings. Wolverine auto-provisions queues based on the message types it discovers in the assembly (`.AutoProvision()` in `WolverineHostBuilderExtensions.cs`). No manual queue creation is required for devtest.

The SQL outbox is still active — Service Bus is the wire transport, SQL is the durability layer.

## Prod-hardening checklist

When promoting beyond devtest:

- [ ] App Service Plan → S1 or P1v3 for deployment slots and zone-redundancy
- [ ] Azure SQL → Entra-only auth (drop the SQL login + password), enable Microsoft Defender for SQL, tune backup retention
- [ ] Private endpoints for SQL, Storage, Service Bus, Key Vault, ACR
- [ ] VNet integration for the App Service
- [ ] WAF (Azure Front Door or App Gateway) in front of the App Service
- [ ] Customer-managed keys for Storage and Key Vault
- [ ] Custom domain + managed TLS
- [ ] Multi-region: Front Door + paired-region App Service + SQL geo-replication
- [ ] Tighten SQL firewall — remove "Allow Azure services" once VNet/private endpoints land
- [ ] Replace the Hangfire dashboard email allow-list with a role/permission integration once roles are projected into JWT claims
- [ ] Set Key Vault `enablePurgeProtection: true`
- [ ] Storage account `allowSharedKeyAccess: false` — switch Data Protection to managed identity blob access
