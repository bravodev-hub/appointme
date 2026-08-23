# `dotnet new appointme` template package — design

**Date:** 2026-08-22
**Status:** implemented — see the resolution note below for where the shipped design diverges

## Resolution note (added after implementation)

This spec records what was believed at design time. Three things below were
proven wrong or superseded during implementation and are **not** what shipped.
The document is annotated rather than rewritten, because the reasoning it
captures is part of the record.

1. **`.github/` does ship.** Decision 4 says it does not. The *Open question*
   at the end of this document recommended the opposite, and that recommendation
   is what was implemented: `devtest.yml`, `codeql.yml` and `secret-scan.yml`
   ship (they read secrets by name and are inert until populated), while
   `template.yml` is excluded — a generated project must never inherit a
   workflow that publishes to BravoDev's package id. The deciding factor was
   that `infra/README.md` documents `devtest.yml` by name, so shipping the IaC
   without its pipeline would have left those instructions dangling.

2. **The rename mechanism is different.** The *Lowercase — enumerated guarded
   symbols* and *PascalCase — `sourceName` with restricted forms* sections
   describe a design that does not work on SDK 10.0.100: `forms: {global:
   ["identity"]}` does not suppress the engine's derived lowercase form, the
   fallback's `isName` is not a real schema property, and any `symbols.name` of
   `type: parameter` is diverted to `-na`/`--param:name`, severing `-n`. What
   shipped instead is three `derived` symbols per token family with
   `onlyIf`-gated `replaces`, dispatching on the character *following* the
   token, plus a dot-stripped form for the ~20 bare C# identifiers
   (`AppointMeSql`, `AddAppointMeAuthentication`) that a dotted `-n` value would
   otherwise corrupt — which this spec did not anticipate at all. The
   follower-distribution table in this document is also a partial measurement;
   the authoritative enumerations are documented in `templates/README.md` and
   enforced by `templates/smoke-test.sh`.

3. **The wrangler placeholder differs.** *Known cosmetic artifacts* predicts
   `app.contoso-booking.dev`. The shipped dotted bucket produces
   `app.contoso.booking.dev`.

Also note the version: this spec and the plan both assume `1.1.0`, but that tag
was already released, so the template's first publishable version is `1.2.0`.

For the design as actually built, read `templates/README.md` (rename routing and
the closed enumerations) and the *Template package* subsection of `CLAUDE.md`.

## Goal

Publish AppointMe as a NuGet template package so a newcomer can run

```bash
dotnet new install BravoDev.AppointMe.Templates::1.1.0
dotnet new appointme -n Contoso.Booking
```

and get a renamed, buildable copy of the solution. The existing clone + Aspire
workflow stays exactly as it is; this adds a second, version-pinned distribution
path. This closes the distribution gap against FullStackHero, whose `dotnet new
fsh-webapi` install is its primary on-ramp.

## Context

- 17 `.csproj` projects across five bounded contexts, plus a Vite frontend, all
  named `AppointMe.*`. No project in the repo is currently packable; there is no
  `Version` property anywhere and no versioning tool (no MinVer / Nerdbank).
- Releases are hand-curated: `CHANGELOG.md` is written by hand, then a `v*` tag
  and a GitHub release are created manually. Current release: `v1.1.0`.
- CI is a single workflow, `.github/workflows/devtest.yml`: build + test +
  frontend lint/build on every PR and push, then ACR image build and devtest
  deploy on `main`. Plus `codeql.yml` and `secret-scan.yml`.
- `Directory.Build.props` sets `Company`/`Authors` = `BravoDev` and a
  `RepositoryUrl` of `https://github.com/bravodev/appointme.git`, which is wrong
  — the real remote is `bravodev-hub/appointme`.

### The naming collision that shapes the design

`AppointMe` appears 1,885 times; the lowercase form appears 761 times. But
**`appointme` is a prefix of `appointment`**, and `Appointment` / `appointments`
is core domain vocabulary: the aggregate, the `/appointments` route, the
`appointments.statistics:view` permission, the `AppointmentsController` surface,
frontend types.

Measured precisely:

| Form | Occurrences | Collides with domain vocabulary? |
|---|---|---|
| `AppointMe` (capital M) | 1,885 | **No** — `Appointment` has a lowercase `m` |
| `appointme` not followed by `n` | 430 | No — these are the real lowercase brand tokens |
| `appointme` followed by `n` | ~331 | **Yes** — every one of these is `appointment*` |

`dotnet new`'s `sourceName` mechanism performs plain substring replacement and,
by default, auto-derives lower/upper/kebab case variants. A naive
`"sourceName": "AppointMe"` would therefore rewrite `appointments` →
`contosonts` across routes, permission strings and TypeScript. Avoiding this is
the central technical constraint of this design.

## Decisions

1. **Full identifier rename.** `-n Contoso.Booking` renames namespaces, project
   and folder names, the `.slnx`/`.sln`, the SQL database name, the Keycloak
   realm, compose/container names, Aspire resource names, and frontend
   identifiers. Domain vocabulary (`Appointment`, `/appointments`) is untouched.

2. **Name is the only parameter.** No `--auth`, no `--no-demo-data`, no module
   switches. The modules are deliberately interwoven — domain events cross
   CRM ↔ Booking ↔ Organizations — so a module-removal switch would emit code
   that does not compile. One parameter means one combination to smoke-test.

3. **`infra/` ships.** The Bicep IaC, its modules, and the Cloudflare Worker
   proxy are part of what makes this a production-grade foundation, and a
   starter kit without a deployment story is half a kit.

4. **`.github/` and `CHANGELOG.md` do not ship.** The workflows encode BravoDev's
   Azure OIDC federation, ACR name and devtest App Service; the changelog is
   AppointMe's release history. See *Open question* below — shipping `infra/`
   without the workflow leaves a dangling reference that needs a decision.

5. **The repo root is the template source; nothing is copied.** A single
   packaging project packs the root into the nupkg's `content/` folder through
   an exclude list. There is no second copy of the codebase, so the template
   cannot drift from the app.

6. **PascalCase rename via `sourceName` with forms restricted to identity;
   lowercase rename via explicitly enumerated symbols.** This is the collision
   mitigation, detailed below.

7. **Version is an explicit property in the repo, and the publish job enforces
   tag/version equality.** No new dependency, and `dotnet pack` at tag `v1.1.0`
   reproducibly produces `1.1.0`.

8. **A generated-output smoke test runs on every PR**, not just at release. This
   is what keeps the template from silently rotting as the app evolves.

## Architecture

### Two exclusion layers

These are distinct and must not be conflated:

- **Pack-time** (`templates/AppointMe.Templates.csproj` content globs) — decides
  which files enter the `.nupkg` at all.
- **Generation-time** (`template.json` `sources[].modifiers[].exclude`) — decides
  which of the packaged files land in the user's output directory.

The overlay directory (below) is packed but generation-excluded from its
original location, then re-mapped to the output root by a second `sources` entry.

### Files added

```
.template.config/
  template.json                    # the template definition
templates/
  AppointMe.Templates.csproj       # packaging-only project, NOT in AppointMe.sln
  overlay/
    README.md                      # the generated project's README
.github/workflows/
  template.yml                     # pack + smoke test + publish
```

### The packaging project

`templates/AppointMe.Templates.csproj`:

- `PackageType=Template`, `PackageId=BravoDev.AppointMe.Templates`
- `TargetFramework` omitted; `NoBuild=true`, `IncludeBuildOutput=false`,
  `SuppressDependenciesWhenPacking=true`, `NoDefaultExcludes` left off
- `ContentTargetFolders=content`
- `Version` — the single source of truth for the package version
- Content glob: `..\**\*` with the pack-time exclude list below
- **Not added to `AppointMe.sln` or `AppointMe.slnx`**, so `dotnet build
  AppointMe.sln` and `dotnet test` are entirely unaffected. CI packs it by path.

### Pack-time exclusions

Build and tool output: `bin/`, `obj/`, `node_modules/`, `dist/`, `.git/`,
`.vs/`, `.idea/`, `artifacts/`, `AppointMe.sln.DotSettings.user`.

Deliberately withheld:

| Path | Reason |
|---|---|
| `.github/` | BravoDev's OIDC federation, ACR, devtest App Service (Decision 4) |
| `CHANGELOG.md` | AppointMe's release history |
| `docs/superpowers/` | Internal specs and plans |
| `docs/CODE_REVIEW_REPORT.md` | Internal security review notes |
| `docs/images/` | Release marketing media; the overlay README does not reference them |
| `src/AppointMe.Api/appsettings.Devtest.json` | Contains BravoDev's live Entra tenant id, client id, and the demo account password. `appsettings.Devtest.example.json` ships in its place — that is exactly what it exists for. |
| `infra/cloudflare-worker/.wrangler/` | Tracked wrangler cache holding the Cloudflare account id and account email. See *Side finding* below. |
| `.claude/settings.json` | Local plugin enablement |
| `templates/AppointMe.Templates.csproj` | The packaging project must not ship inside its own output |

Everything else ships, including `LICENSE`, `THIRD-PARTY-NOTICES.md`,
`.gitleaks.toml`, `.editorconfig`, `NuGet.config`, `global.json`,
`compose.yaml`, `docker/`, `docs/identity-resolution.md`, `.claude/skills/`,
`CLAUDE.md`, and all of `infra/`.

### The rename mechanism

**PascalCase — `sourceName` with restricted forms.**

```jsonc
"sourceName": "AppointMe",
"symbols": {
  "name": {
    "type": "parameter",
    "datatype": "string",
    "forms": { "global": [ "identity" ] }   // suppress auto lower/upper/kebab variants
  }
}
```

Restricting the name symbol's forms to `identity` is what prevents the engine
from deriving a lowercase `appointme` replacement — the exact behaviour that
would corrupt `appointments`. This one rule safely covers 1,885 of the 2,315
brand occurrences, plus path renames (`src/AppointMe.Api/` →
`src/Contoso.Booking.Api/`) and file renames (`AppointMe.slnx`).

**Lowercase — enumerated guarded symbols.**

The 430 lowercase brand occurrences are handled by explicit symbols whose
`replaces` token includes the following character, so the token can never match
inside `appointment`. `n` is deliberately absent from the set. Derived values
are built with the `join` generator from a `kebabCase` form of the name:

| `replaces` token | Example source | Count |
|---|---|---|
| `appointme-` | `appointme-api`, `rg-appointme-devtest`, `appointme-realm.json` | 178 |
| `appointme.` | `app.appointme.dev`, `appointme.schemas.ts` | 155 |
| `appointme/` | `bravodev-hub/appointme`, `/realms/appointme/` | 21 |
| `appointme'`, `appointme"`, `` appointme` ``, `appointme:`, `appointme,`, `appointme;`, `appointme{`, `appointme$`, `appointme_`, `appointme ` (space) | quoted realm names, prose, shell snippets | 60 |
| `appointmed` | `acrappointmedevtestze5tkm` | 11 |
| `appointmeA` | `appointmeApi` | 3 |

Plus `fileRename` entries for the lowercase-named files: `appointme-realm.json`,
`appointme.ts`, `appointme.schemas.ts`.

**Why not the alternatives.** Plain `sourceName` with default forms corrupts the
domain vocabulary and cannot be fixed without renaming the `Appointment`
aggregate. A `dotnet new` script post-action doing precise regex renames would
be more expressive but requires `--allow-scripts`, is platform-specific, and
degrades the install UX.

**The proof is the assertion, not the enumeration.** The token list above is
mechanical but hard to prove exhaustive by inspection, so correctness is
enforced by the smoke test rather than by careful reading:

- No `AppointMe` (capital M) survives anywhere in generated output.
- No lowercase `appointme` survives **except** where followed by `n`.
- `Appointment`, `/appointments`, and `appointments.statistics:view` survive
  verbatim, with occurrence counts matching the source repo.

### Known cosmetic artifacts of a full rename

Accepted, not fixed:

- `wrangler.jsonc` becomes `app.contoso-booking.dev` / zone
  `contoso-booking.dev` — a placeholder domain the user must edit. Called out in
  the overlay README.
- `infra/README.md` retains BravoDev's `uniqueString` resource suffixes
  (`ze5tkm`) in its worked example commands, e.g.
  `app-contoso-booking-devtest-ze5tkm`. These are illustrative command examples,
  not deployed values.

### The overlay README

The repo `README.md` is release marketing: it opens with the `app.appointme.dev`
live-demo link and the tour GIF, and links `docs/images/` which does not ship. A
generated project should not point its owner at someone else's demo. So
`README.md` is pack-time excluded and `templates/overlay/README.md` is mapped to
the output root instead, covering quick start, the module map, the Aspire
workflow, the placeholder values to change (Keycloak realm, wrangler domain,
`appsettings.Devtest.example.json`), and a pointer to the upstream repo.

## Versioning and publishing

`templates/AppointMe.Templates.csproj` carries `<Version>1.1.0</Version>` —
matching the current app release. `ContinuousIntegrationBuild=true` is set in CI
for deterministic output.

`.github/workflows/template.yml`:

| Trigger | Behaviour |
|---|---|
| `pull_request`, `push: main` | pack → smoke test → upload nupkg artifact. No publish. |
| `push: tags: v*` | pack → **guard `${TAG#v}` equals `$(Version)`, fail otherwise** → smoke test → `dotnet nuget push` |
| `workflow_dispatch` | pack → smoke test, no publish |

The guard makes tag/package drift impossible: a `v1.2.0` tag against a
`<Version>1.1.0</Version>` fails the run rather than publishing a mislabelled
package.

### Smoke test

Runs against the packed nupkg, not the working tree:

1. `dotnet new install ./artifacts/BravoDev.AppointMe.Templates.<v>.nupkg`
2. `dotnet new appointme -n Contoso.Booking -o /tmp/gen`
3. Vocabulary assertions (above)
4. `dotnet restore && dotnet build -c Release` on the generated solution
5. `dotnet test -c Release` on the generated solution
6. `npm ci && npm run lint && npm run build` in the generated frontend
7. `dotnet new uninstall BravoDev.AppointMe.Templates`

Step 5 is the one that matters most: it proves the rename produced a solution
whose full test suite still passes, which is a far stronger signal than "it
compiles".

## Documentation changes

- `README.md` — add an install-from-NuGet path alongside the clone path, as an
  alternative rather than a replacement.
- `CLAUDE.md` — document the packaging project, the two exclusion layers, and
  the rule that anything renaming a project or adding a lowercase `appointme`
  token needs the smoke test to pass.
- `Directory.Build.props` — fix `RepositoryUrl` to `bravodev-hub/appointme`,
  since that value is embedded in the published package metadata.

## What cannot be done in-session

The final `dotnet nuget push` needs a nuget.org API key. Handling that key is
out of scope for an agent session, and the account is BravoDev's. Two owner
actions are prerequisites for the first publish:

1. Reserve / confirm ownership of the `BravoDev.AppointMe.Templates` id (or the
   `BravoDev.*` prefix) on nuget.org.
2. Add `NUGET_API_KEY` as a repository Actions secret.

Everything short of the push is verifiable locally and will be verified: pack,
install from the local nupkg, generate, build, test. The push is the only step
that stays unproven until the owner tags a release.

## Side finding (outside this work)

`infra/cloudflare-worker/.wrangler/cache/wrangler-account.json` is committed and
contains the Cloudflare account id `a2fd8e764329bd5be409b4136b4fc167` and the
account name `Info@bravo-dev.com's Account`. A Cloudflare account id is not a
credential — it appears in dashboard URLs and commonly in `wrangler.toml` — but
it is a build-cache artifact that should not be tracked, and the account email is
gratuitous exposure in a public repo. gitleaks does not flag it because it
matches no credential pattern. Recommended independently of this work:
`git rm -r --cached infra/cloudflare-worker/.wrangler` plus a `.wrangler/`
gitignore entry. This design excludes the path from the package regardless.

## Open question

**Should `.github/workflows/devtest.yml` ship after all?** Decision 3 puts
`infra/` in the package, but `infra/README.md` references
`.github/workflows/devtest.yml` at line 5 and devotes its section 5 (lines
150–207) to wiring up that workflow — "the `devtest` workflow runs three jobs".
Shipping the IaC without its pipeline leaves those instructions pointing at a
file the generated project does not have.

Recommendation: ship `devtest.yml`, `codeql.yml` and `secret-scan.yml` — the
workflows read secrets by name and are inert until the user populates them, so
they are templates rather than leaks — and explicitly **never** ship
`template.yml`, which would otherwise give every generated project a workflow
that publishes to BravoDev's NuGet package id. If the answer is no, the
mitigation is an overlay copy of `infra/README.md` with section 5 rewritten,
which adds a second large file that can drift.

## Non-goals

- Changing or replacing the clone + Aspire workflow.
- Any application code change.
- Release marketing (announcement, badges, nuget.org README polish).
- Multi-template packages (an `appointme-module` sub-template for scaffolding a
  new bounded context is a plausible follow-up, explicitly out of scope here).
