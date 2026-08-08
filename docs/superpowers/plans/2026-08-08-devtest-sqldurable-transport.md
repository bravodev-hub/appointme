# Devtest SqlDurable Transport + Service Bus Removal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Run devtest on Wolverine's `SqlDurable` transport (durable local queues, no broker), remove Azure Service Bus from the Bicep infra, and delete the live namespace + Key Vault secret to cut cost.

**Architecture:** Config-only app change (devtest stops overriding the default transport), followed by infra-as-code removal, followed by live-resource teardown. Strict ordering: the new image must be deployed and verified healthy BEFORE any Azure resource is touched, so the running app never references something deleted.

**Tech Stack:** .NET 10 / Wolverine 6, Bicep, az CLI, GitHub Actions (devtest workflow), gh CLI.

**Spec:** `docs/superpowers/specs/2026-08-08-devtest-sqldurable-transport-design.md`

## Global Constraints

- Keep the `AzureServiceBus` case in `WolverineHostBuilderExtensions.cs` and the `WolverineFx.AzureServiceBus` package reference — only infra and live resources are removed.
- `.gitlab-ci.yml` and both CI pipelines stay untouched.
- Azure: subscription `AppointMe-DevTest` = `9187eacf-3a7f-4877-98fe-7f6b4b25ff5c`; resource group `rg-appointme-devtest`; Web App `app-appointme-devtest-ze5tkm`; namespace `sb-appointme-devtest-ze5tkm`; Key Vault secret `AppointMeMessaging`.
- **Ordering is hard:** Task 4 (deletion) must not start until Task 2's verification (new image healthy on `SqlDurable`) has fully passed.
- `az bicep build` regenerates the committed `infra/main.json`; commit it together with the `.bicep` edits.

---

### Task 1: Switch devtest transport to SqlDurable, drop unused Aspire package

**Files:**
- Modify: `src/AppointMe.Api/appsettings.Devtest.json:26-28`
- Modify: `src/AppointMe.Api/appsettings.Devtest.example.json:26-28`
- Modify: `src/AppointMe.Aspire/AppointMe.Aspire.csproj:10`

**Interfaces:**
- Produces: devtest image whose `Wolverine:Transport` resolves to `SqlDurable`; Task 2 deploys and verifies it.

- [ ] **Step 1: Change the transport in both devtest appsettings files**

In `src/AppointMe.Api/appsettings.Devtest.json` AND `src/AppointMe.Api/appsettings.Devtest.example.json`, change:

```json
  "Wolverine": {
    "Transport": "AzureServiceBus"
  },
```

to:

```json
  "Wolverine": {
    "Transport": "SqlDurable"
  },
```

- [ ] **Step 2: Remove the unused Aspire Service Bus package reference**

In `src/AppointMe.Aspire/AppointMe.Aspire.csproj`, delete the line:

```xml
    <PackageReference Include="Aspire.Hosting.Azure.ServiceBus" />
```

(`src/AppointMe.Aspire/Program.cs` has no Service Bus usage — this reference is dead weight. Do NOT touch `WolverineFx.AzureServiceBus` in `Directory.Packages.props` or the API csproj.)

- [ ] **Step 3: Verify the solution builds**

Run: `dotnet build AppointMe.sln -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit (do NOT push yet)**

```bash
git add src/AppointMe.Api/appsettings.Devtest.json src/AppointMe.Api/appsettings.Devtest.example.json src/AppointMe.Aspire/AppointMe.Aspire.csproj
git commit -m "Switch devtest to SqlDurable Wolverine transport

Durable local queues over the existing SQL outbox — no broker needed.
The AzureServiceBus code path stays available as a template option."
```

---

### Task 2: Deploy and verify devtest on SqlDurable

**Interfaces:**
- Consumes: Task 1's commit on `main`.
- Produces: verified-healthy devtest running without Service Bus — the gate for Task 4.

- [ ] **Step 1: Push and watch the pipeline**

```bash
git push origin main
sleep 15
RUN_ID=$(gh run list --repo bravodev-hub/appointme --workflow devtest --limit 1 --json databaseId -q '.[0].databaseId')
gh run watch $RUN_ID --repo bravodev-hub/appointme --exit-status --interval 30
```

Expected: jobs `test`, `build-image`, `deploy-devtest` all succeed.

- [ ] **Step 2: Verify the new image is live and responding**

```bash
SHA8=$(git rev-parse HEAD | cut -c1-8)
az webapp config container show \
  --name app-appointme-devtest-ze5tkm --resource-group rg-appointme-devtest \
  --subscription 9187eacf-3a7f-4877-98fe-7f6b4b25ff5c \
  --query "[?name=='DOCKER_CUSTOM_IMAGE_NAME'].value" -o tsv
curl -s -o /dev/null -w "%{http_code}\n" --max-time 120 https://app-appointme-devtest-ze5tkm.azurewebsites.net/
```

Expected: image ends `appointme-api:$SHA8`; curl returns `200` (retry a few times over ~3 minutes while the container warms up and runs EF migrations).

- [ ] **Step 3: Check container logs for messaging errors**

```bash
az webapp log config --name app-appointme-devtest-ze5tkm --resource-group rg-appointme-devtest \
  --subscription 9187eacf-3a7f-4877-98fe-7f6b4b25ff5c --docker-container-logging filesystem
timeout 60 az webapp log tail --name app-appointme-devtest-ze5tkm --resource-group rg-appointme-devtest \
  --subscription 9187eacf-3a7f-4877-98fe-7f6b4b25ff5c 2>&1 | grep -iE "servicebus|AppointMeMessaging|wolverine.*(error|fail)" || echo "NO MESSAGING ERRORS"
```

Expected: `NO MESSAGING ERRORS` (no ServiceBus/AppointMeMessaging references in startup logs).

- [ ] **Step 4: Exercise message handling end-to-end via demo login**

Devtest has `Demo.Enabled: true` (demo user `demo@appointme.dev`). Using browser automation (Claude-in-Chrome): open `https://app-appointme-devtest-ze5tkm.azurewebsites.net`, sign in via the demo-login affordance on the login page, register a new customer (Customers → add), and confirm the customer appears in the list. Command handlers run through Wolverine, so a successful registration proves messaging on local queues works in devtest.

Fallback if browser automation is unavailable: rely on Steps 2–3 plus absence of Wolverine errors in a second `az webapp log tail` window after clicking around the public pages; note the reduced verification in the final report.

---

### Task 3: Remove Service Bus from Bicep, regenerate main.json, update README

**Files:**
- Delete: `infra/modules/service-bus.bicep`
- Modify: `infra/main.bicep:43,106-113,166`
- Modify: `infra/modules/app-service.bicep:31-32,95-98`
- Regenerate: `infra/main.json`
- Modify: `infra/README.md:15,207,217,227-231`

**Interfaces:**
- Consumes: nothing from earlier tasks (pure IaC/docs).
- Produces: a template with no Service Bus; Task 4 assumes the template no longer declares `ConnectionStrings__AppointMeMessaging`.

- [ ] **Step 1: Delete the module file**

```bash
git rm infra/modules/service-bus.bicep
```

- [ ] **Step 2: Edit `infra/main.bicep`**

Remove the name entry (in the `names` object):

```bicep
  serviceBus: take('sb-appointme-${environmentName}-${shortSuffix}', 50)
```

Remove the module block:

```bicep
module serviceBus 'modules/service-bus.bicep' = {
  name: 'service-bus'
  params: {
    location: location
    namespaceName: names.serviceBus
    tags: defaultTags
  }
}
```

Remove the output:

```bicep
output serviceBusNamespace string = serviceBus.outputs.name
```

- [ ] **Step 3: Edit `infra/modules/app-service.bicep`**

Remove the param:

```bicep
@description('Name of the Key Vault secret holding the Service Bus connection string.')
param messagingConnectionStringSecretName string = 'AppointMeMessaging'
```

Remove the app setting entry:

```bicep
        {
          name: 'ConnectionStrings__AppointMeMessaging'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/${messagingConnectionStringSecretName}/)'
        }
```

- [ ] **Step 4: Regenerate the compiled ARM template and validate**

```bash
az bicep build --file infra/main.bicep --outfile infra/main.json
grep -ci servicebus infra/main.json || echo "0 servicebus references"
```

Expected: build succeeds with no errors/warnings about missing references; grep reports `0 servicebus references`.

- [ ] **Step 5: Update `infra/README.md`**

Remove the module-table row:

```markdown
| `modules/service-bus.bicep`     | Service Bus namespace (Standard)                            |
```

Remove the Key Vault seeding-table row:

```markdown
| `AppointMeMessaging`           | `az servicebus namespace authorization-rule keys list --resource-group rg-appointme-devtest --namespace-name <sb-name> --name RootManageSharedAccessKey --query primaryConnectionString -o tsv` |
```

Remove the seeding-example line:

```bash
az keyvault secret set --vault-name "$KV" --name AppointMeMessaging --value "..."
```

Replace the whole "Wolverine in Azure" section (heading plus its two paragraphs) with:

```markdown
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
```

- [ ] **Step 6: Commit and push**

```bash
git add infra/
git commit -m "Remove Azure Service Bus from devtest infra

Devtest now runs on Wolverine SqlDurable local queues; the namespace,
Key Vault reference, and module are gone from the template. Opting back
in is documented in infra/README.md."
git push origin main
```

(The push triggers a CI deploy of an identical-config image — harmless.)

---

### Task 4: Remove live app setting, delete namespace and secret, final verification

**Interfaces:**
- Consumes: Task 2's PASSED verification (hard gate) and Task 3's template state.

**Note (spec deviation, surfaced during planning):** the spec said "apply the Bicep deployment" to drop the live app setting, but a full template deployment needs operator-held parameters (`SQL_ADMIN_PASSWORD`, etc.). A targeted `az webapp config appsettings delete` reaches the identical end state without those secrets; the template (Task 3) is already consistent for the next full operator deploy.

- [ ] **Step 1: Remove the live App Service app setting**

```bash
SUB=9187eacf-3a7f-4877-98fe-7f6b4b25ff5c
az webapp config appsettings delete \
  --name app-appointme-devtest-ze5tkm --resource-group rg-appointme-devtest --subscription $SUB \
  --setting-names ConnectionStrings__AppointMeMessaging --output none
az webapp config appsettings list \
  --name app-appointme-devtest-ze5tkm --resource-group rg-appointme-devtest --subscription $SUB \
  --query "[?name=='ConnectionStrings__AppointMeMessaging']" -o tsv
```

Expected: second command prints nothing. The settings change restarts the container automatically.

- [ ] **Step 2: Confirm the site is healthy after the restart**

```bash
curl -s -o /dev/null -w "%{http_code}\n" --max-time 120 https://app-appointme-devtest-ze5tkm.azurewebsites.net/
```

Expected: `200` (retry over ~3 minutes for warmup).

- [ ] **Step 3: Delete the Service Bus namespace**

```bash
az servicebus namespace delete \
  --name sb-appointme-devtest-ze5tkm --resource-group rg-appointme-devtest --subscription $SUB
az servicebus namespace list --resource-group rg-appointme-devtest --subscription $SUB --query "[].name" -o tsv
```

Expected: delete completes (takes a minute or two); list prints nothing.

- [ ] **Step 4: Delete the Key Vault secret**

```bash
KV=$(az keyvault list --resource-group rg-appointme-devtest --subscription $SUB --query "[0].name" -o tsv)
az keyvault secret delete --vault-name "$KV" --name AppointMeMessaging --output none
az keyvault secret list --vault-name "$KV" --subscription $SUB --query "[].name" -o tsv
```

Expected: list no longer contains `AppointMeMessaging` (it moves to soft-deleted state; that's fine).

- [ ] **Step 5: Final sweep**

```bash
curl -s -o /dev/null -w "%{http_code}\n" https://app-appointme-devtest-ze5tkm.azurewebsites.net/
```

Expected: `200`. Report: transport switched, namespace + secret gone, app healthy, monthly Service Bus cost eliminated.
