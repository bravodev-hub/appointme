# Free-Tier Devtest via Cloudflare Worker Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Serve `app.appointme.dev` through a Cloudflare Worker host-rewrite proxy so the App Service custom-domain binding can be removed and the plan downgraded from B1 to F1 (free).

**Architecture:** A Worker on route `app.appointme.dev/*` forwards to the azurewebsites origin with `X-Original-Host` carrying the public hostname; the app's ForwardedHeaders middleware maps that header so OIDC redirects and generated URLs stay on the custom domain. Once traffic flows through the Worker, the Azure hostname binding and managed certificate are deleted and the plan drops to F1.

**Tech Stack:** ASP.NET Core ForwardedHeaders, Cloudflare Workers (wrangler), az CLI, Bicep, GitHub Actions devtest workflow.

**Spec:** `docs/superpowers/specs/2026-08-08-free-tier-cloudflare-worker-design.md`

## Global Constraints

- Azure: subscription `AppointMe-DevTest` = `9187eacf-3a7f-4877-98fe-7f6b4b25ff5c`; resource group `rg-appointme-devtest`; Web App `app-appointme-devtest-ze5tkm`; plan `asp-appointme-devtest`; managed cert resource `mc-app-appointme-dev`.
- Hostnames: public `app.appointme.dev`; origin `app-appointme-devtest-ze5tkm.azurewebsites.net`; forwarded-host header name is exactly `X-Original-Host` in BOTH the Worker and `Program.cs`.
- **Ordering is hard:** do not remove the binding (Task 3) until Task 2 proves requests flow through the Worker; do not downgrade (Task 4) until Task 3's verification passes.
- Keep `custom-domain*.bicep` modules in the repo (paid-tier template option).
- Before any wrangler command, load the `wrangler` skill (per its trigger).
- `az keyvault`-style data-plane quirks don't apply here, but all az commands must pass `--subscription`.

---

### Task 1: Forwarded-host support in the app

**Files:**
- Modify: `src/AppointMe.Api/Program.cs:53-58`

**Interfaces:**
- Produces: the app honors `X-Original-Host` as the public host. Task 2's Worker sends exactly this header name.

- [ ] **Step 1: Extend ForwardedHeadersOptions**

In `src/AppointMe.Api/Program.cs`, change:

```csharp
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
```

to:

```csharp
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    // The Cloudflare Worker fronting devtest rewrites Host to the azurewebsites
    // origin and carries the public hostname here instead.
    options.ForwardedHostHeaderName = "X-Original-Host";
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
```

- [ ] **Step 2: Build**

Run: `dotnet build AppointMe.sln -c Release`
Expected: 0 errors.

- [ ] **Step 3: Commit and push; watch CI**

```bash
git add src/AppointMe.Api/Program.cs
git commit -m "Honor X-Original-Host as forwarded public hostname"
git push origin main
sleep 15
RUN_ID=$(gh run list --repo bravodev-hub/appointme --workflow devtest --limit 1 --json databaseId -q '.[0].databaseId')
gh run watch $RUN_ID --repo bravodev-hub/appointme --exit-status --interval 30
```

Expected: all three jobs pass.

- [ ] **Step 4: Verify site unaffected**

Run: `curl -s -o /dev/null -w "%{http_code}\n" https://app.appointme.dev/`
Expected: `200` (binding still in place; nothing user-visible changed yet).

---

### Task 2: Cloudflare Worker + route

**Files:**
- Create: `infra/cloudflare-worker/wrangler.toml`
- Create: `infra/cloudflare-worker/src/index.js`

**Interfaces:**
- Consumes: header contract `X-Original-Host` from Task 1.
- Produces: all `app.appointme.dev/*` traffic reaches the origin under the azurewebsites hostname — the precondition for Task 3.

- [ ] **Step 1: Create `infra/cloudflare-worker/wrangler.toml`**

```toml
name = "appointme-devtest-proxy"
main = "src/index.js"
compatibility_date = "2026-08-01"
workers_dev = false

routes = [
  { pattern = "app.appointme.dev/*", zone_name = "appointme.dev" }
]
```

- [ ] **Step 2: Create `infra/cloudflare-worker/src/index.js`**

```js
// Host-rewrite proxy: lets the App Service run on the Free (F1) tier, which
// forbids custom hostname bindings. Cloudflare terminates TLS for
// app.appointme.dev and this Worker forwards to the azurewebsites origin,
// carrying the public hostname in X-Original-Host (mapped by
// ForwardedHeadersOptions in Program.cs).
const ORIGIN_HOST = 'app-appointme-devtest-ze5tkm.azurewebsites.net';
const PUBLIC_HOST = 'app.appointme.dev';

export default {
    async fetch(request) {
        const url = new URL(request.url);
        url.hostname = ORIGIN_HOST;
        const upstream = new Request(url, request);
        upstream.headers.set('X-Original-Host', PUBLIC_HOST);
        return fetch(upstream);
    },
};
```

- [ ] **Step 3: Load the `wrangler` skill, then authenticate (USER ACTION if needed)**

Run: `cd infra/cloudflare-worker && npx wrangler whoami`
If not authenticated, **stop and ask the user** to run `! npx wrangler login` and re-check. Do not proceed until `whoami` shows the account owning the `appointme.dev` zone.

- [ ] **Step 4: Deploy the Worker**

Run: `cd infra/cloudflare-worker && npx wrangler deploy`
Expected: deploy succeeds and prints the route `app.appointme.dev/*`.

- [ ] **Step 5: Prove traffic flows through the Worker**

In one shell: `cd infra/cloudflare-worker && timeout 30 npx wrangler tail --format pretty` while another runs `curl -s -o /dev/null https://app.appointme.dev/`.
Expected: the tail shows the GET request. Also `curl -s -o /dev/null -w "%{http_code}\n" https://app.appointme.dev/` returns `200`.

- [ ] **Step 6: Commit**

```bash
git add infra/cloudflare-worker/
git commit -m "Add Cloudflare Worker host-rewrite proxy for devtest custom domain"
```

---

### Task 3: Remove the Azure binding and managed certificate

**Interfaces:**
- Consumes: Task 2's verification (Worker actively routing) — hard gate.
- Produces: a Web App with no custom hostname binding — the precondition for Task 4.

- [ ] **Step 1: Unbind SNI and delete the hostname binding**

```bash
SUB=9187eacf-3a7f-4877-98fe-7f6b4b25ff5c
TP=$(az webapp show --name app-appointme-devtest-ze5tkm -g rg-appointme-devtest --subscription $SUB \
  --query "hostNameSslStates[?name=='app.appointme.dev'].thumbprint" -o tsv)
az webapp config ssl unbind --certificate-thumbprint "$TP" \
  --name app-appointme-devtest-ze5tkm -g rg-appointme-devtest --subscription $SUB
az webapp config hostname delete --webapp-name app-appointme-devtest-ze5tkm -g rg-appointme-devtest \
  --subscription $SUB --hostname app.appointme.dev
az webapp config hostname list --webapp-name app-appointme-devtest-ze5tkm -g rg-appointme-devtest \
  --subscription $SUB -o table
```

Expected: final list shows ONLY `app-appointme-devtest-ze5tkm.azurewebsites.net`.

- [ ] **Step 2: Delete the managed certificate**

```bash
az resource delete -g rg-appointme-devtest --subscription $SUB \
  --resource-type Microsoft.Web/certificates --name mc-app-appointme-dev
az resource list -g rg-appointme-devtest --subscription $SUB \
  --resource-type Microsoft.Web/certificates --query "[].name" -o tsv
```

Expected: empty list.

- [ ] **Step 3: Verify the site through the Worker**

```bash
curl -s -o /dev/null -w "custom domain: %{http_code}\n" https://app.appointme.dev/
curl -s -o /dev/null -w "origin direct: %{http_code}\n" https://app-appointme-devtest-ze5tkm.azurewebsites.net/
```

Expected: both `200`. If the custom domain fails here, the Worker is not routing — STOP and restore the binding via the custom-domain Bicep module before investigating.

---

### Task 4: Downgrade the plan to F1

**Interfaces:**
- Consumes: Task 3's verification — hard gate.

- [ ] **Step 1: Downgrade**

```bash
SUB=9187eacf-3a7f-4877-98fe-7f6b4b25ff5c
az appservice plan update --name asp-appointme-devtest -g rg-appointme-devtest --subscription $SUB --sku F1
az appservice plan show --name asp-appointme-devtest -g rg-appointme-devtest --subscription $SUB \
  --query sku.name -o tsv
```

Expected: `F1`.

- [ ] **Step 2: Verify with cold-start patience**

```bash
for i in $(seq 1 10); do
  CODE=$(curl -s -o /dev/null -w "%{http_code}" --max-time 120 https://app.appointme.dev/ 2>/dev/null)
  echo "attempt $i: HTTP $CODE"
  [ "$CODE" = "200" ] && break
  sleep 30
done
```

Expected: `200` within ~5 minutes (first hit re-warms the container and runs EF migrations).

- [ ] **Step 3: Full login round-trip via browser**

Using browser automation: open `https://app.appointme.dev/login/demo`, confirm the app loads signed-in on the `app.appointme.dev` address (no azurewebsites.net in the address bar at any point), open Customers, register a customer named "Freetier Check" with email `freetier.check@example.com`, confirm it appears, then delete it via the row menu. Close the tab afterwards.

Expected: every page stays on `app.appointme.dev`; create and delete both succeed.

---

### Task 5: Bicep + README updates

**Files:**
- Modify: `infra/modules/app-service-plan.bicep:10-14`
- Regenerate: `infra/main.json`
- Modify: `infra/README.md:15,26,28`

- [ ] **Step 1: Change the default plan SKU**

In `infra/modules/app-service-plan.bicep`, change:

```bicep
@description('Plan SKU. Devtest default: B1. Use S1+ for deployment slots.')
param sku object = {
  name: 'B1'
  tier: 'Basic'
}
```

to:

```bicep
@description('Plan SKU. Devtest default: F1 (free; no Always On, no custom-domain bindings — the Cloudflare Worker fronts the custom domain). Use B1+ for App Service-managed custom domains, S1+ for deployment slots.')
param sku object = {
  name: 'F1'
  tier: 'Free'
}
```

- [ ] **Step 2: Regenerate main.json**

Run: `az bicep build --file infra/main.bicep --outfile infra/main.json`
Expected: builds clean (the pre-existing `custom-domain.bicep` dependsOn lint warning is fine); `grep -c '"B1"' infra/main.json` reports 0.

- [ ] **Step 3: Update README**

Change the module-table row:

```markdown
| `modules/app-service-plan.bicep` | Linux App Service Plan (B1)                                |
```

to:

```markdown
| `modules/app-service-plan.bicep` | Linux App Service Plan (F1, free)                          |
```

Change:

```markdown
- **Custom domain + DNS** — devtest uses the generated `*.azurewebsites.net` hostname.
```

to:

```markdown
- **Custom domain + DNS** — on F1, App Service custom-domain bindings are not available; devtest serves `app.appointme.dev` through a Cloudflare Worker host-rewrite proxy (`infra/cloudflare-worker/`, deploy with `npx wrangler deploy`). On B1+ you can instead bind the domain directly via `modules/custom-domain.bicep` (App Service Managed Certificate).
```

Change:

```markdown
- **Deployment slots** — requires Standard (S1) plan or higher; devtest runs B1.
```

to:

```markdown
- **Deployment slots** — requires Standard (S1) plan or higher; devtest runs F1.
```

- [ ] **Step 4: Commit and push**

```bash
git add infra/
git commit -m "Default devtest plan to F1; document Cloudflare Worker custom domain"
git push origin main
```

Expected: CI redeploys identical app code — green.
