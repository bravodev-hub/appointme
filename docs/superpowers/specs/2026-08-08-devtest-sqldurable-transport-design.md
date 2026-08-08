# Devtest SqlDurable transport + Service Bus removal — design

**Date:** 2026-08-08
**Status:** approved

## Goal

Switch the devtest environment from Azure Service Bus to Wolverine's
`SqlDurable` transport (durable local queues over the existing SQL outbox),
remove Service Bus from the Bicep infrastructure, and delete the live
namespace from the Azure subscription to cut cost (~£7/mo for the Standard
SKU).

## Context

- `WolverineHostBuilderExtensions.cs` supports two transports via the
  `Wolverine:Transport` setting: `SqlDurable` (default; no broker) and
  `AzureServiceBus`. Only `appsettings.Devtest.json` selects
  `AzureServiceBus`.
- Local dev (Aspire) already runs without any Service Bus; the
  `Aspire.Hosting.Azure.ServiceBus` package reference in
  `AppointMe.Aspire.csproj` is unused dead weight.
- Live resources: namespace `sb-appointme-devtest-ze5tkm` (Standard) in
  `rg-appointme-devtest`, subscription `AppointMe-DevTest`
  (`9187eacf-3a7f-4877-98fe-7f6b4b25ff5c`); Key Vault secret
  `AppointMeMessaging`; App Service app setting
  `ConnectionStrings__AppointMeMessaging` (Key Vault reference).

## Decisions

1. **Transport: `SqlDurable`, not pure in-memory.** Non-durable local queues
   would lose in-flight messages on every restart/deploy; `SqlDurable` is
   broker-less AND durable, and is already the app default.
2. **Keep the `AzureServiceBus` code path** in
   `WolverineHostBuilderExtensions.cs` and the `WolverineFx.AzureServiceBus`
   package — zero Azure cost, template users can opt back in via config.
   Only infra and live resources are removed.
3. **Deploy before delete.** The running image must stop referencing the
   namespace and the Key Vault secret before either is deleted.

## Deliverables

### 1. Transport switch (code/config)

- `src/AppointMe.Api/appsettings.Devtest.json` and
  `appsettings.Devtest.example.json`: `"Wolverine": { "Transport": ... }` →
  `"SqlDurable"`.
- `src/AppointMe.Aspire/AppointMe.Aspire.csproj`: remove the
  `Aspire.Hosting.Azure.ServiceBus` package reference (unused in
  `Program.cs`).

### 2. Bicep removal

- Delete `infra/modules/service-bus.bicep`.
- `infra/main.bicep`: remove `names.serviceBus`, the `serviceBus` module
  block, and the `serviceBusNamespace` output.
- `infra/modules/app-service.bicep`: remove the
  `ConnectionStrings__AppointMeMessaging` app setting and the
  `messagingConnectionStringSecretName` param.
- Regenerate the committed `infra/main.json` via `az bicep build`.

### 3. Execution order

1. Push the transport switch; CI builds and deploys the new image; verify
   the app is healthy on `SqlDurable`.
2. Apply the Bicep deployment (`az deployment group create`) — replaces the
   App Service app-settings list without the messaging entry. Incremental
   mode does not delete resources, so:
3. Explicitly delete the namespace
   (`az servicebus namespace delete -n sb-appointme-devtest-ze5tkm`) and the
   `AppointMeMessaging` Key Vault secret.

### 4. Docs — `infra/README.md`

- Remove the `service-bus.bicep` row from the module table.
- Remove `AppointMeMessaging` from the Key Vault seeding table and example.
- Rewrite the "Wolverine in Azure" section: `SqlDurable` is the devtest
  transport (durable local queues, SQL outbox); Azure Service Bus remains an
  opt-in for template users (set `Wolverine:Transport=AzureServiceBus`,
  provision a namespace, seed the `AppointMeMessaging` secret, restore the
  app setting).

## Error handling

- The app on `SqlDurable` reads no messaging connection string, so deleting
  the secret/namespace after the deploy cannot break startup.
- If the new image fails health checks, the namespace still exists —
  rollback is redeploying the previous image tag; nothing is lost.

## Verification

- After step 1: site returns 200; exercise a domain event end-to-end
  (register a customer via the API, confirm the CRM attendee projection
  appears) to prove message handling works on local queues in devtest.
- After steps 2–3: `az servicebus namespace list` returns empty for the
  resource group; App Service app settings contain no
  `ConnectionStrings__AppointMeMessaging`; site still returns 200.

## Out of scope

- Removing the `AzureServiceBus` switch case or NuGet packages from the API.
- Any change to local development (already broker-less).
- GitLab pipeline (unaffected — image config changes flow through both CIs).
