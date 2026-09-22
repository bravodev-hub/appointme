# `dotnet new appointme` Template Package Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish AppointMe as a NuGet template package so `dotnet new install BravoDev.AppointMe.Templates::1.1.0` followed by `dotnet new appointme -n Contoso.Booking` produces a renamed, building, test-passing copy of the solution.

**Architecture:** The repo root *is* the template source — a single packaging project (`templates/AppointMe.Templates.csproj`) packs it into the nupkg's `content/appointme/` folder through an exclude list, so no copy of the codebase exists to drift. `.template.config/template.json` drives a full identifier rename. A bash smoke-test script is the test harness for every task and is also what CI runs; it packs, installs, generates, asserts, then builds and tests the *generated* solution.

**Tech Stack:** .NET 10 SDK (`dotnet pack`, `dotnet new install`), NuGet template packages (`PackageType=Template`), the dotnet templating engine's `sourceName`/`symbols`/`valueForms`, GitHub Actions, bash.

**Spec:** `docs/superpowers/specs/2026-08-22-dotnet-new-template-package-design.md`

## Global Constraints

- **Package id:** `BravoDev.AppointMe.Templates`. **Template short name:** `appointme`. **Template identity:** `BravoDev.AppointMe.ModularMonolith.CSharp`.
- **Version lives in exactly one place:** `<Version>` in `templates/AppointMe.Templates.csproj`. Starting value `1.1.0`. Nowhere else.
- **One template parameter only:** `-n`/`--name`. Do not add `--auth`, `--no-demo-data`, or module switches.
- **Never rename domain vocabulary.** `Appointment`, `appointments`, `/appointments`, `appointments.statistics:view` must survive generation byte-for-byte. The lowercase brand token `appointme` is only ever replaced when followed by a character that is **not** `n`.
- **Never add `templates/AppointMe.Templates.csproj` to `AppointMe.sln` or `AppointMe.slnx`.** `dotnet build AppointMe.sln` and `dotnet test` must stay unaffected. CI packs the project by path.
- **Two independent exclusion layers.** Pack-time = `None` item globs in the packaging csproj (what enters the nupkg). Generation-time = `sources[].modifiers[].exclude` in `template.json` (what lands in the user's output). Do not try to express one with the other.
- **`.template.config/` is stripped from generated output automatically** by the templating engine. Do not add it to the generation-time exclude list.
- **License:** MIT (`PackageLicenseExpression=MIT`), matching `LICENSE`.
- **`artifacts/` and `*.nupkg` are already gitignored** (`.gitignore:57`, `.gitignore:183`, `.gitignore:335`). Do not re-add them.
- **Central Package Management is on** (`Directory.Packages.props`, `ManagePackageVersionsCentrally=true`). The packaging project must declare **no** `PackageReference`, or CPM will demand a central `PackageVersion` entry.
- **`.github/` workflows DO ship** (`devtest.yml`, `codeql.yml`, `secret-scan.yml`) — this is the spec's *Open question* resolved to its recommendation, because `infra/README.md:5` and its section 5 (lines 150–207) document `devtest.yml` by name, and shipping the IaC without its pipeline leaves those instructions dangling. **`.github/workflows/template.yml` must NEVER ship** — it would give every generated project a workflow that publishes to BravoDev's package id. Reversing this decision is a two-line change: **add** `..\.github\**` to the `Exclude` list in Task 1 Step 3 (the workflows are otherwise swept up by the `..\**\*` include), and change `assert_present ".github/workflows/devtest.yml"` to `assert_absent` in Task 1 Step 1.
- **`src/AppointMe.Api/appsettings.Devtest.json` must never ship.** It holds BravoDev's live Entra tenant id (`8e4cba39-…`), client id, and the demo account password. `appsettings.Devtest.example.json` ships in its place.
- **`infra/cloudflare-worker/.wrangler/` must never ship.** Tracked cache holding a Cloudflare account id and account email.

---

### Task 1: Packaging project, verbatim template, and the smoke-test harness

Deliverable: `dotnet new appointme` installs and generates a byte-identical copy of the repo (no rename yet), with the withheld paths absent. The smoke-test script exists and passes.

**Files:**
- Create: `templates/AppointMe.Templates.csproj`
- Create: `.template.config/template.json`
- Create: `templates/smoke-test.sh`

**Interfaces:**
- Produces: `templates/smoke-test.sh` — the harness every later task extends. Contract: run from the repo root with no arguments, exit 0 on success, non-zero with a `FAIL:` line on any assertion failure. Accepts optional env overrides `NAME` (default `Contoso.Booking`) and `KEEP` (any non-empty value keeps the temp dir).
- Produces: nupkg at `artifacts/BravoDev.AppointMe.Templates.<version>.nupkg`, consumed by Task 5's workflow.

- [ ] **Step 1: Write the failing smoke test**

Create `templates/smoke-test.sh`:

```bash
#!/usr/bin/env bash
# Packs the template, installs it from the local nupkg, generates a project,
# and asserts the result. This is the harness for the template work: it is what
# CI runs and what a human runs locally.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NAME="${NAME:-Contoso.Booking}"
PKG_ID="BravoDev.AppointMe.Templates"
OUT_DIR="$(mktemp -d)"
GEN_DIR="$OUT_DIR/gen"
FAILURES=0

cleanup() {
  dotnet new uninstall "$PKG_ID" >/dev/null 2>&1 || true
  if [[ -z "${KEEP:-}" ]]; then rm -rf "$OUT_DIR"; fi
}
trap cleanup EXIT

fail() { echo "FAIL: $*" >&2; FAILURES=$((FAILURES + 1)); }
pass() { echo "ok: $*"; }

# --- pack -------------------------------------------------------------------
echo "== packing =="
rm -rf "$REPO_ROOT/artifacts"
dotnet pack "$REPO_ROOT/templates/AppointMe.Templates.csproj" \
  -c Release -o "$REPO_ROOT/artifacts"

NUPKG="$(ls "$REPO_ROOT"/artifacts/$PKG_ID.*.nupkg 2>/dev/null | head -1)"
if [[ -z "$NUPKG" ]]; then echo "FAIL: no nupkg produced" >&2; exit 1; fi
pass "packed $(basename "$NUPKG")"

# --- install + generate -----------------------------------------------------
echo "== installing =="
dotnet new uninstall "$PKG_ID" >/dev/null 2>&1 || true
dotnet new install "$NUPKG"

echo "== generating =="
dotnet new appointme -n "$NAME" -o "$GEN_DIR"

# --- assertions: withheld paths --------------------------------------------
echo "== asserting withheld paths =="
assert_absent() {
  if [[ -e "$GEN_DIR/$1" ]]; then fail "$1 should not be in generated output"; else pass "absent: $1"; fi
}
assert_present() {
  if [[ -e "$GEN_DIR/$1" ]]; then pass "present: $1"; else fail "$1 missing from generated output"; fi
}

assert_absent "CHANGELOG.md"
assert_absent "docs/superpowers"
assert_absent "docs/CODE_REVIEW_REPORT.md"
assert_absent "docs/images"
assert_absent "src/AppointMe.Api/appsettings.Devtest.json"
assert_absent "infra/cloudflare-worker/.wrangler"
assert_absent ".claude/settings.json"
assert_absent "templates/AppointMe.Templates.csproj"
assert_absent ".template.config"
assert_absent ".github/workflows/template.yml"

assert_present "LICENSE"
assert_present "THIRD-PARTY-NOTICES.md"
assert_present "global.json"
assert_present "compose.yaml"
assert_present "Directory.Packages.props"
assert_present "docs/identity-resolution.md"
assert_present "src/AppointMe.Api/appsettings.Devtest.example.json"
assert_present "infra/main.bicep"
assert_present "infra/modules/sql.bicep"
assert_present ".github/workflows/devtest.yml"

# --- no build output leaked ------------------------------------------------
echo "== asserting no build output leaked =="
for junk in bin obj node_modules dist; do
  if find "$GEN_DIR" -type d -name "$junk" -print -quit | grep -q .; then
    fail "generated output contains a $junk directory"
  else
    pass "no $junk directories"
  fi
done

echo
if [[ $FAILURES -gt 0 ]]; then echo "$FAILURES assertion(s) failed" >&2; exit 1; fi
echo "smoke test passed"
```

Make it executable:

```bash
chmod +x templates/smoke-test.sh
```

- [ ] **Step 2: Run it to verify it fails**

Run: `./templates/smoke-test.sh`
Expected: FAIL at the pack step — `MSB1009` / "project file does not exist" for `templates/AppointMe.Templates.csproj`.

- [ ] **Step 3: Create the packaging project**

Create `templates/AppointMe.Templates.csproj`. Note `None` items with an explicit `PackagePath` — files outside the project cone need it, or NuGet flattens them.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <PackageType>Template</PackageType>
    <PackageId>BravoDev.AppointMe.Templates</PackageId>
    <Version>1.1.0</Version>
    <Title>AppointMe — modular-monolith .NET 10 SaaS foundation</Title>
    <Description>A production-grade modular-monolith .NET 10 + React 19 SaaS foundation: multi-tenancy, OIDC auth, CQRS, domain events, durable messaging, an auto-discovered permission system, a business dashboard, and a one-command .NET Aspire local stack. Run: dotnet new appointme -n Your.Project</Description>
    <PackageTags>dotnet-new;template;templates;aspire;ddd;modular-monolith;multi-tenant;cqrs;react;saas</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>

    <!-- A template package carries content only: no assembly, no dependencies. -->
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <IncludeContentInPack>false</IncludeContentInPack>
    <SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>
    <EnableDefaultItems>false</EnableDefaultItems>
    <GenerateDependencyFile>false</GenerateDependencyFile>

    <!-- NU5128: no lib/ or ref/ assets in a package that declares a TFM. Expected. -->
    <NoWarn>$(NoWarn);NU5128</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <None Include="..\**\*"
          Exclude="..\**\bin\**;..\**\obj\**;..\**\node_modules\**;..\**\dist\**;..\.git\**;..\.vs\**;..\.idea\**;..\artifacts\**;..\templates\**;..\CHANGELOG.md;..\docs\superpowers\**;..\docs\CODE_REVIEW_REPORT.md;..\docs\images\**;..\src\AppointMe.Api\appsettings.Devtest.json;..\infra\cloudflare-worker\.wrangler\**;..\.claude\settings.json;..\AppointMe.sln.DotSettings.user;..\.github\workflows\template.yml"
          Pack="true"
          PackagePath="content\appointme\%(RecursiveDir)%(Filename)%(Extension)" />
  </ItemGroup>

</Project>
```

Two things to know about this glob:

- `..\templates\**` is excluded wholesale so the packaging project never ships inside its own output. Task 4 adds a narrower re-include for `templates/overlay/**`.
- `..\.template.config\**` is deliberately **not** excluded — the nupkg must contain `template.json`, or `dotnet new install` finds no template. The engine strips it from *generated output* on its own.

- [ ] **Step 4: Create the minimal template definition**

Create `.template.config/template.json`. No rename yet — that is Task 2 and Task 3.

```json
{
  "$schema": "http://json.schemastore.org/template",
  "author": "BravoDev",
  "classifications": [ "Web", "WebAPI", "React", "Aspire", "DDD", "Multi-tenant", "SaaS", "Solution" ],
  "identity": "BravoDev.AppointMe.ModularMonolith.CSharp",
  "name": "AppointMe modular-monolith SaaS foundation",
  "shortName": "appointme",
  "description": "A modular-monolith .NET 10 + React 19 multi-tenant SaaS foundation with Identity, Organizations, CRM and Booking bounded contexts, CQRS, domain events, a permission system, and a .NET Aspire local stack.",
  "tags": {
    "language": "C#",
    "type": "solution"
  },
  "preferNameDirectory": true,
  "sources": [
    {
      "source": "./",
      "target": "./",
      "modifiers": [
        {
          "exclude": [
            "templates/**",
            "artifacts/**",
            "**/bin/**",
            "**/obj/**",
            "**/node_modules/**",
            "**/dist/**"
          ]
        }
      ]
    }
  ]
}
```

- [ ] **Step 5: Run the smoke test to verify it passes**

Run: `./templates/smoke-test.sh`
Expected: PASS — `smoke test passed`, with every `ok:` line and no `FAIL:` lines.

If pack succeeds but `dotnet new install` reports no templates found, the `.template.config` folder did not make it into `content/appointme/`. Verify with:

```bash
unzip -l artifacts/BravoDev.AppointMe.Templates.1.1.0.nupkg | grep template.json
```
Expected: one entry at `content/appointme/.template.config/template.json`.

- [ ] **Step 6: Commit**

```bash
git add templates/AppointMe.Templates.csproj .template.config/template.json templates/smoke-test.sh
git commit -m "Add a dotnet new template package that ships the repo verbatim

Pack the repo root into content/appointme/ from a packaging-only project
that stays out of AppointMe.sln, so the template has no copied tree to
drift from. No renaming yet.

templates/smoke-test.sh packs, installs from the local nupkg, generates,
and asserts the withheld paths are absent and the kept ones present."
```

---

### Task 2: PascalCase rename

Deliverable: `dotnet new appointme -n Contoso.Booking` renames every `AppointMe` identifier, folder and file, and the generated solution builds and its tests pass. Domain vocabulary is provably untouched.

**Files:**
- Modify: `.template.config/template.json`
- Modify: `templates/smoke-test.sh`

**Interfaces:**
- Consumes: `templates/smoke-test.sh` from Task 1 (harness contract above).
- Produces: generated output whose project paths are `src/<Name>.Api/`, `src/CRM/<Name>.Crm/` etc., and whose solution file is `<Name>.slnx`. Task 3 assumes these paths.

- [ ] **Step 1: Add the vocabulary and build assertions to the smoke test**

In `templates/smoke-test.sh`, insert this block immediately before the final `echo` / exit block:

```bash
# --- assertions: rename correctness ----------------------------------------
echo "== asserting rename correctness =="

# Domain vocabulary must survive byte-for-byte. These counts come from the
# source repo; a rename that eats "appointments" will drop them.
SRC_APPOINTMENT="$(grep -rIoh "Appointment" "$REPO_ROOT/src" --exclude-dir=node_modules --exclude-dir=obj --exclude-dir=bin --exclude-dir=dist | wc -l | tr -d ' ')"
GEN_APPOINTMENT="$(grep -rIoh "Appointment" "$GEN_DIR/src" --exclude-dir=node_modules --exclude-dir=obj --exclude-dir=bin --exclude-dir=dist | wc -l | tr -d ' ')"
if [[ "$SRC_APPOINTMENT" == "$GEN_APPOINTMENT" ]]; then
  pass "Appointment survived intact ($GEN_APPOINTMENT occurrences)"
else
  fail "Appointment count changed: source $SRC_APPOINTMENT, generated $GEN_APPOINTMENT"
fi

for token in "appointments.statistics:view" "/appointments"; do
  if grep -rIq -- "$token" "$GEN_DIR/src" --exclude-dir=node_modules --exclude-dir=obj --exclude-dir=bin --exclude-dir=dist; then
    pass "survived: $token"
  else
    fail "$token missing from generated output"
  fi
done

# No PascalCase brand token may survive anywhere.
if grep -rIl "AppointMe" "$GEN_DIR" --exclude-dir=node_modules --exclude-dir=obj --exclude-dir=bin --exclude-dir=dist >/dev/null 2>&1; then
  echo "--- files still containing AppointMe ---" >&2
  grep -rIl "AppointMe" "$GEN_DIR" --exclude-dir=node_modules --exclude-dir=obj --exclude-dir=bin --exclude-dir=dist >&2
  fail "generated output still contains the PascalCase token AppointMe"
else
  pass "no residual AppointMe"
fi

# Paths were renamed.
assert_present "src/$NAME.Api/$NAME.Api.csproj"
assert_present "src/CRM/$NAME.Crm/$NAME.Crm.csproj"
assert_present "$NAME.slnx"
assert_absent  "src/AppointMe.Api"

# --- build and test the generated solution ---------------------------------
echo "== building generated solution =="
if dotnet build "$GEN_DIR/$NAME.sln" -c Release; then
  pass "generated solution builds"
else
  fail "generated solution does not build"
fi

echo "== testing generated solution =="
if dotnet test "$GEN_DIR/$NAME.sln" -c Release --no-build; then
  pass "generated solution tests pass"
else
  fail "generated solution tests fail"
fi
```

- [ ] **Step 2: Run the smoke test to verify the new assertions fail**

Run: `./templates/smoke-test.sh`
Expected: FAIL — `generated output still contains the PascalCase token AppointMe`, plus the three `assert_present` path failures, because Task 1's template does no renaming.

- [ ] **Step 3: Add the rename to the template definition**

In `.template.config/template.json`, add `sourceName` and a `symbols` block above `sources`. Restricting the name symbol's value forms to `identity` is the whole point: it stops the engine deriving a lowercase `appointme` replacement, which is what would corrupt `appointments`.

```json
  "sourceName": "AppointMe",
  "symbols": {
    "name": {
      "type": "parameter",
      "datatype": "string",
      "description": "The name for the generated solution, used for namespaces, project names and the solution file.",
      "forms": {
        "global": [ "identity" ]
      }
    }
  },
```

- [ ] **Step 4: Run the smoke test to verify it passes**

Run: `./templates/smoke-test.sh`
Expected: PASS, including `Appointment survived intact`, `no residual AppointMe`, `generated solution builds`, `generated solution tests pass`.

**If `Appointment survived intact` FAILS** — i.e. the count dropped and generated files contain `Contoso.Bookingnt` — then restricting `forms` to `identity` did not suppress the engine's derived lowercase form. Fall back to dropping `sourceName` entirely and driving the rename from an explicit symbol instead. Replace the `sourceName` line and `symbols` block from Step 3 with:

```json
  "symbols": {
    "name": {
      "type": "parameter",
      "datatype": "string",
      "isName": true,
      "description": "The name for the generated solution, used for namespaces, project names and the solution file.",
      "replaces": "AppointMe",
      "fileRename": "AppointMe"
    }
  },
```

`replaces` is a literal, case-sensitive substring replacement with no derived forms at all, so it cannot touch lowercase `appointment`. Re-run the smoke test; the same assertions apply. Note that without `sourceName` you lose nothing here — `fileRename` covers the path renaming that `sourceName` was doing.

- [ ] **Step 5: Commit**

```bash
git add .template.config/template.json templates/smoke-test.sh
git commit -m "Rename PascalCase AppointMe identifiers on generation

Restrict the name symbol's value forms to identity so the templating
engine does not derive a lowercase appointme replacement -- that derived
form is a prefix of appointment/appointments and would rewrite the
Appointment aggregate, the /appointments route and the
appointments.statistics:view permission.

The smoke test now asserts the Appointment occurrence count is identical
between source and generated output, that no PascalCase token survives,
and that the generated solution builds and its tests pass."
```

---

### Task 3: Lowercase brand-token rename

Deliverable: no lowercase `appointme` survives in generated output except where it is part of `appointment*`. The generated frontend lints and builds.

**Files:**
- Modify: `.template.config/template.json`
- Modify: `templates/smoke-test.sh`

**Interfaces:**
- Consumes: the renamed paths produced by Task 2.
- Produces: generated output free of lowercase brand tokens. No later task depends on internals of this one.

- [ ] **Step 1: Add the residual-token and frontend assertions**

In `templates/smoke-test.sh`, add before the final exit block:

```bash
# --- assertions: lowercase brand tokens ------------------------------------
echo "== asserting lowercase brand tokens are gone =="

# Every legitimate lowercase brand token is followed by something other than
# "n". "appointmen..." is always the domain word and must be left alone. So:
# any lowercase "appointme" NOT followed by "n" is a leak.
LEAKS="$(grep -rIoE "appointme[^n]" "$GEN_DIR" \
  --exclude-dir=node_modules --exclude-dir=obj --exclude-dir=bin --exclude-dir=dist \
  2>/dev/null | sort | uniq -c | sort -rn || true)"
if [[ -n "$LEAKS" ]]; then
  echo "--- residual lowercase brand tokens ---" >&2
  echo "$LEAKS" >&2
  echo "--- files ---" >&2
  grep -rIlE "appointme[^n]" "$GEN_DIR" \
    --exclude-dir=node_modules --exclude-dir=obj --exclude-dir=bin --exclude-dir=dist >&2 || true
  fail "generated output still contains lowercase brand tokens"
else
  pass "no residual lowercase brand tokens"
fi

# Lowercase-named files were renamed too.
assert_absent "src/$NAME.Aspire/appointme-realm.json"
assert_absent "src/$NAME.Frontend/src/api/appointme.ts"
assert_absent "src/$NAME.Frontend/src/api/appointme.schemas.ts"

# ...but the 65 paths containing lowercase "appointment" must NOT be renamed.
# An unguarded path rename would turn appointments/ into <name>nts/ and break
# every import in the frontend.
SRC_PATHS="$(cd "$REPO_ROOT" && git ls-files | grep -cE "appointment" || true)"
GEN_PATHS="$(cd "$GEN_DIR" && find . -path ./node_modules -prune -o -type f -print | grep -cE "appointment" || true)"
if [[ "$SRC_PATHS" == "$GEN_PATHS" ]]; then
  pass "appointment-named paths preserved ($GEN_PATHS)"
else
  fail "appointment-named path count changed: source $SRC_PATHS, generated $GEN_PATHS"
fi

# --- frontend ---------------------------------------------------------------
echo "== building generated frontend =="
(
  cd "$GEN_DIR/src/$NAME.Frontend"
  npm ci && npm run lint && npm run build
) && pass "generated frontend lints and builds" || fail "generated frontend failed"
```

- [ ] **Step 2: Run the smoke test to verify the new assertions fail**

Run: `./templates/smoke-test.sh`
Expected: FAIL — `generated output still contains lowercase brand tokens`, listing roughly 430 occurrences dominated by `appointme-` and `appointme.`, plus the three `assert_absent` file failures.

- [ ] **Step 3: Add the kebab-case form and the guarded token symbols**

In `.template.config/template.json`, extend `symbols`. `nameKebab` is the lowercase-hyphenated form of the name (`Contoso.Booking` → `contoso-booking`); each `tok*` symbol joins it with the guard character so the `replaces` token can never match inside `appointment`.

Add to the `symbols` object, after the `name` symbol:

```json
    "nameKebab": {
      "type": "derived",
      "valueSource": "name",
      "valueTransform": "kebabCase"
    },
    "tokDash":   { "type": "generated", "generator": "join", "replaces": "appointme-",  "fileRename": "appointme-", "parameters": { "symbols": [ { "type": "ref", "value": "nameKebab" }, { "type": "const", "value": "-" } ] } },
    "tokDot":    { "type": "generated", "generator": "join", "replaces": "appointme.",  "fileRename": "appointme.", "parameters": { "symbols": [ { "type": "ref", "value": "nameKebab" }, { "type": "const", "value": "." } ] } },
    "tokSlash":  { "type": "generated", "generator": "join", "replaces": "appointme/",  "parameters": { "symbols": [ { "type": "ref", "value": "nameKebab" }, { "type": "const", "value": "/" } ] } },
    "tokQuot":   { "type": "generated", "generator": "join", "replaces": "appointme\"", "parameters": { "symbols": [ { "type": "ref", "value": "nameKebab" }, { "type": "const", "value": "\"" } ] } },
    "tokApos":   { "type": "generated", "generator": "join", "replaces": "appointme'",  "parameters": { "symbols": [ { "type": "ref", "value": "nameKebab" }, { "type": "const", "value": "'" } ] } },
    "tokTick":   { "type": "generated", "generator": "join", "replaces": "appointme`",  "parameters": { "symbols": [ { "type": "ref", "value": "nameKebab" }, { "type": "const", "value": "`" } ] } },
    "tokColon":  { "type": "generated", "generator": "join", "replaces": "appointme:",  "parameters": { "symbols": [ { "type": "ref", "value": "nameKebab" }, { "type": "const", "value": ":" } ] } },
    "tokComma":  { "type": "generated", "generator": "join", "replaces": "appointme,",  "parameters": { "symbols": [ { "type": "ref", "value": "nameKebab" }, { "type": "const", "value": "," } ] } },
    "tokSemi":   { "type": "generated", "generator": "join", "replaces": "appointme;",  "parameters": { "symbols": [ { "type": "ref", "value": "nameKebab" }, { "type": "const", "value": ";" } ] } },
    "tokBrace":  { "type": "generated", "generator": "join", "replaces": "appointme{",  "parameters": { "symbols": [ { "type": "ref", "value": "nameKebab" }, { "type": "const", "value": "{" } ] } },
    "tokDollar": { "type": "generated", "generator": "join", "replaces": "appointme$",  "parameters": { "symbols": [ { "type": "ref", "value": "nameKebab" }, { "type": "const", "value": "$" } ] } },
    "tokUnder":  { "type": "generated", "generator": "join", "replaces": "appointme_",  "parameters": { "symbols": [ { "type": "ref", "value": "nameKebab" }, { "type": "const", "value": "_" } ] } },
    "tokSpace":  { "type": "generated", "generator": "join", "replaces": "appointme ",  "parameters": { "symbols": [ { "type": "ref", "value": "nameKebab" }, { "type": "const", "value": " " } ] } },
    "tokD":      { "type": "generated", "generator": "join", "replaces": "appointmed",  "parameters": { "symbols": [ { "type": "ref", "value": "nameKebab" }, { "type": "const", "value": "d" } ] } },
    "tokA":      { "type": "generated", "generator": "join", "replaces": "appointmeA",  "parameters": { "symbols": [ { "type": "ref", "value": "nameKebab" }, { "type": "const", "value": "A" } ] } }
```

The `fileRename` on `tokDash` and `tokDot` is what renames the three
lowercase-named files — no `sources[].rename` map is needed, and none should be
added:

| Source path | `fileRename` that catches it | Result |
|---|---|---|
| `src/AppointMe.Aspire/appointme-realm.json` | `tokDash` (`appointme-`) | `contoso-booking-realm.json` |
| `src/AppointMe.Frontend/src/api/appointme.ts` | `tokDot` (`appointme.`) | `contoso-booking.ts` |
| `src/AppointMe.Frontend/src/api/appointme.schemas.ts` | `tokDot` (`appointme.`) | `contoso-booking.schemas.ts` |

**Do not put `fileRename` on a bare `appointme` token.** 65 tracked paths contain
lowercase `appointment` — `src/…/app/appointments/`,
`appointments/cancel-appointment/`, `appointment-content.tsx`,
`Booking.Contracts/Appointments/Events/` — and an unguarded path rename would
corrupt every one of them. The guard characters `-` and `.` are what make these
two safe: `appointment` continues with `n`, so neither token can match inside it.

- [ ] **Step 4: Run the smoke test to verify it passes**

Run: `./templates/smoke-test.sh`
Expected: PASS — `no residual lowercase brand tokens`, `generated frontend lints and builds`, and all Task 2 assertions still green.

If the leak report still lists tokens, read the `--- residual lowercase brand tokens ---` output: the first column is the count and the second is the token including its guard character. Add one more `tok*` symbol per distinct guard character shown, following the pattern above. Do **not** add a symbol whose `replaces` is bare `appointme` — that is the one thing guaranteed to break `appointments`.

- [ ] **Step 5: Commit**

```bash
git add .template.config/template.json templates/smoke-test.sh
git commit -m "Rename lowercase appointme brand tokens on generation

Every real lowercase brand token is followed by a character other than
'n'; anything followed by 'n' is the domain word appointment. So replace
guarded two-character tokens (appointme-, appointme., appointme/, ...)
rather than the bare word, and rename the three lowercase-named files.

The smoke test now fails on any lowercase appointme not followed by 'n',
printing the offending tokens and files, and builds the generated
frontend."
```

---

### Task 4: Overlay README for generated projects

Deliverable: generated projects get their own README instead of AppointMe's release-marketing one.

**Files:**
- Create: `templates/overlay/README.md`
- Modify: `templates/AppointMe.Templates.csproj`
- Modify: `.template.config/template.json`
- Modify: `templates/smoke-test.sh`

**Interfaces:**
- Consumes: the `nameKebab` symbol defined in Task 3 Step 3, and the PascalCase `AppointMe` replacement from Task 2 — both apply to the overlay file's contents, so the overlay can write `AppointMe.Aspire` and have it renamed.
- Produces: `README.md` at the generated project root.

- [ ] **Step 1: Add the README assertions**

In `templates/smoke-test.sh`, add before the final exit block:

```bash
# --- assertions: overlay README --------------------------------------------
echo "== asserting overlay README =="
assert_present "README.md"
assert_absent  "templates/overlay"

if grep -q "app.appointme.dev" "$GEN_DIR/README.md" 2>/dev/null; then
  fail "generated README still points at the AppointMe live demo"
else
  pass "generated README has no live-demo link"
fi

if grep -q "docs/images" "$GEN_DIR/README.md" 2>/dev/null; then
  fail "generated README references docs/images, which is not shipped"
else
  pass "generated README has no broken image links"
fi

if grep -q "$NAME" "$GEN_DIR/README.md" 2>/dev/null; then
  pass "generated README is renamed"
else
  fail "generated README does not mention $NAME"
fi
```

- [ ] **Step 2: Run the smoke test to verify it fails**

Run: `./templates/smoke-test.sh`
Expected: FAIL — `generated README still points at the AppointMe live demo` and `generated README references docs/images`, because the repo `README.md` is still what ships.

- [ ] **Step 3: Write the overlay README**

Create `templates/overlay/README.md`. Write `AppointMe` wherever the project name belongs — the Task 2 replacement rewrites it to the generated name.

```markdown
# AppointMe

Generated from the [AppointMe](https://github.com/bravodev-hub/appointme) template —
a modular-monolith .NET 10 + React 19 multi-tenant SaaS foundation.

## Run it

```bash
cd src/AppointMe.Aspire && dotnet run
```

.NET Aspire starts SQL Server, Keycloak, Mailpit, the API and the frontend, applies
database migrations, and seeds demo data. The frontend comes up on
https://localhost:5173.

Prefer to run the backing services yourself? `compose.yaml` brings up SQL Server,
Keycloak and Mailpit on the same ports; then run `dotnet run --project src/AppointMe.Api`
and `npm run dev` in `src/AppointMe.Frontend`.

## What's inside

- **Modular monolith** — Identity, Organizations, CRM and Booking, each a bounded
  context with its own `DbContext` and schema, organized by vertical slice.
- **Auth** — OIDC with a hybrid scheme: JWT Bearer for the API, cookies for browser
  flows. Keycloak locally, Entra External ID for the Azure deployment.
- **Multi-tenancy** — company resolution via header/claim, EF Core query filters on
  the command path, Dapper reads carrying the tenant predicate by convention.
- **CQRS + DDD** — EF Core aggregates and domain events for writes, Dapper for reads,
  Wolverine for async messaging over a durable SQL transport.
- **Permissions** — auto-discovered, role-based, with default grant policies.
- **Business dashboard** — KPIs, trends, staff load and a peak-hours heatmap.
- **Typed frontend** — TanStack Query hooks and TypeScript types generated from the
  backend OpenAPI spec via orval.
- **Deployment** — `infra/` holds Bicep IaC for Azure App Service, SQL, Key Vault,
  Container Registry and Application Insights, plus a Cloudflare Worker proxy.

`CLAUDE.md` documents the architecture, slice conventions and naming rules in depth.
`docs/identity-resolution.md` explains how the app separates identity from principal.

## Change these before deploying

The template renamed identifiers for you, but these carry placeholder values:

- `src/AppointMe.Api/appsettings.Devtest.example.json` — copy to
  `appsettings.Devtest.json` and fill in your Entra tenant, client id, base URL,
  super-admin email and demo account. It is the deployment config; nothing works
  without it.
- `infra/cloudflare-worker/wrangler.jsonc` — the `routes` pattern and `zone_name`
  point at a domain derived from your project name, which almost certainly is not
  yours.
- `infra/README.md` — its worked examples use resource-name suffixes from the
  upstream project's deployments. Your Bicep deployment generates its own.
- `.github/workflows/devtest.yml` — expects the Azure OIDC secrets listed in
  `infra/README.md` section 5. It fails until you set them, and does nothing
  harmful in the meantime.

## License

MIT — see `LICENSE`. Third-party notices are in `THIRD-PARTY-NOTICES.md`.
```

- [ ] **Step 4: Pack the overlay and map it over the root README**

In `templates/AppointMe.Templates.csproj`, add `..\README.md` to the `Exclude` list of the existing `None` item, and add a second `None` item that re-includes the overlay (Task 1's glob excludes `..\templates\**` wholesale):

```xml
    <None Include="..\templates\overlay\**\*"
          Pack="true"
          PackagePath="content\appointme\templates\overlay\%(RecursiveDir)%(Filename)%(Extension)" />
```

In `.template.config/template.json`, add a second `sources` entry after the first, which maps the overlay onto the output root:

```json
    {
      "source": "./templates/overlay/",
      "target": "./"
    }
```

The first `sources` entry already excludes `templates/**` at generation time, so the overlay directory itself does not appear in the output — only its contents, remapped to the root.

- [ ] **Step 5: Run the smoke test to verify it passes**

Run: `./templates/smoke-test.sh`
Expected: PASS, including `generated README has no live-demo link`, `generated README has no broken image links`, `generated README is renamed`, `absent: templates/overlay`.

- [ ] **Step 6: Commit**

```bash
git add templates/overlay/README.md templates/AppointMe.Templates.csproj .template.config/template.json templates/smoke-test.sh
git commit -m "Give generated projects their own README

The repo README is release marketing: it opens with the app.appointme.dev
live-demo link and the tour GIF, and links docs/images, which the template
does not ship. Generated projects get a short overlay README instead --
quick start, module map, and the placeholder values a new owner has to
change before deploying."
```

---

### Task 5: CI workflow — pack, smoke test, and tag-guarded publish

Deliverable: every PR proves the template still generates a building, test-passing solution; a `v*` tag publishes the package, and cannot publish a mislabelled one.

**Files:**
- Create: `.github/workflows/template.yml`

**Interfaces:**
- Consumes: `templates/smoke-test.sh` (harness contract from Task 1) and the nupkg it produces in `artifacts/`.
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Write the workflow**

Create `.github/workflows/template.yml`:

```yaml
# Template package CI/CD.
#
#   verify   — pack the template, generate a project from the packed nupkg, then
#              build, test and lint the GENERATED output. Runs on every PR and
#              push to main. This is what stops the template rotting as the app
#              changes underneath it.
#   publish  — on a v* tag only: re-verify, assert the tag matches the packaged
#              version, then push to nuget.org.
#
# Required repo Actions secret:
#   NUGET_API_KEY   nuget.org push key scoped to BravoDev.AppointMe.Templates
#
# NOTE: this workflow is deliberately excluded from the template package itself
# (see templates/AppointMe.Templates.csproj) — a generated project must not
# inherit a job that publishes to BravoDev's package id.

name: template

on:
  push:
    branches: [main]
    tags: ['v*']
  pull_request:
  workflow_dispatch:

permissions:
  contents: read

concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: ${{ github.event_name == 'pull_request' }}

env:
  DOTNET_CLI_TELEMETRY_OPTOUT: "1"
  DOTNET_NOLOGO: "1"
  PKG_ID: BravoDev.AppointMe.Templates

jobs:
  verify:
    name: Pack and verify generated output
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7

      - name: Setup .NET
        uses: actions/setup-dotnet@v6
        with:
          dotnet-version: '10.0.x'

      - name: Setup Node
        uses: actions/setup-node@v7
        with:
          node-version: '22'

      - name: Cache NuGet packages
        uses: actions/cache@v6
        with:
          path: ~/.nuget/packages
          key: nuget-${{ runner.os }}-${{ hashFiles('Directory.Packages.props') }}
          restore-keys: nuget-${{ runner.os }}-

      - name: Pack, generate, build and test the generated solution
        run: ./templates/smoke-test.sh
        env:
          ContinuousIntegrationBuild: "true"

      - name: Upload package
        uses: actions/upload-artifact@v5
        with:
          name: template-package
          path: artifacts/*.nupkg
          if-no-files-found: error

  publish:
    name: Publish to nuget.org
    runs-on: ubuntu-latest
    needs: verify
    if: startsWith(github.ref, 'refs/tags/v')
    environment: nuget
    steps:
      - uses: actions/checkout@v7

      - name: Setup .NET
        uses: actions/setup-dotnet@v6
        with:
          dotnet-version: '10.0.x'

      - name: Download package
        uses: actions/download-artifact@v6
        with:
          name: template-package
          path: artifacts

      - name: Assert the tag matches the packaged version
        run: |
          set -euo pipefail
          TAG="${GITHUB_REF_NAME#v}"
          NUPKG="$(ls artifacts/$PKG_ID.*.nupkg | head -1)"
          PKG_VERSION="$(basename "$NUPKG" .nupkg)"
          PKG_VERSION="${PKG_VERSION#$PKG_ID.}"
          echo "tag=$TAG packaged=$PKG_VERSION"
          if [[ "$TAG" != "$PKG_VERSION" ]]; then
            echo "::error::Tag v$TAG does not match the packaged version $PKG_VERSION."
            echo "::error::Bump <Version> in templates/AppointMe.Templates.csproj to $TAG, or tag v$PKG_VERSION instead."
            exit 1
          fi

      - name: Push
        run: |
          dotnet nuget push artifacts/$PKG_ID.*.nupkg \
            --api-key "${{ secrets.NUGET_API_KEY }}" \
            --source https://api.nuget.org/v3/index.json \
            --skip-duplicate
```

- [ ] **Step 2: Verify the workflow parses and the guard logic is right**

Run the guard logic locally against the real nupkg to prove both branches:

```bash
./templates/smoke-test.sh   # leaves artifacts/ populated
PKG_ID=BravoDev.AppointMe.Templates
NUPKG="$(ls artifacts/$PKG_ID.*.nupkg | head -1)"
PKG_VERSION="$(basename "$NUPKG" .nupkg)"; PKG_VERSION="${PKG_VERSION#$PKG_ID.}"
echo "packaged version: $PKG_VERSION"
[[ "1.1.0" == "$PKG_VERSION" ]] && echo "match branch OK"
[[ "1.2.0" != "$PKG_VERSION" ]] && echo "mismatch branch OK"
```
Expected: `packaged version: 1.1.0`, `match branch OK`, `mismatch branch OK`.

- [ ] **Step 3: Confirm the workflow is not shipped**

Run:

```bash
unzip -l artifacts/BravoDev.AppointMe.Templates.1.1.0.nupkg | grep -c "workflows/template.yml" || echo "not shipped (correct)"
unzip -l artifacts/BravoDev.AppointMe.Templates.1.1.0.nupkg | grep -c "workflows/devtest.yml"
```
Expected: `not shipped (correct)` for the first, `1` for the second.

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/template.yml
git commit -m "Add template package CI with a tag-guarded publish

verify packs the template, generates a project from the packed nupkg, and
builds, tests and lints the generated output on every PR -- proving the
rename still produces a working solution as the app changes.

publish runs only on a v* tag and hard-fails when the tag does not match
the packaged version, so a mislabelled package cannot reach nuget.org."
```

---

### Task 6: Documentation and package metadata

Deliverable: the NuGet install path is documented alongside the clone path, the repo's own guidance covers the packaging project, and the published package metadata points at the right repository.

**Files:**
- Modify: `README.md` (after the "Live demo" section, before "Dashboard")
- Modify: `CLAUDE.md` (Build and Development Commands section)
- Modify: `Directory.Build.props:6`

**Interfaces:**
- Consumes: the package id and short name from the Global Constraints.
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Fix the repository URL in package metadata**

In `Directory.Build.props`, change line 6 from:

```xml
    <RepositoryUrl>https://github.com/bravodev/appointme.git</RepositoryUrl>
```

to:

```xml
    <RepositoryUrl>https://github.com/bravodev-hub/appointme.git</RepositoryUrl>
```

Verify the packed metadata picks it up:

```bash
./templates/smoke-test.sh
cd artifacts && unzip -o -q BravoDev.AppointMe.Templates.1.1.0.nupkg BravoDev.AppointMe.Templates.nuspec && grep repository BravoDev.AppointMe.Templates.nuspec; cd ..
```
Expected: a `<repository ... url="https://github.com/bravodev-hub/appointme.git" />` element.

- [ ] **Step 2: Add the install path to the README**

In `README.md`, insert a new section immediately after the live-demo image line and before `## Dashboard`:

```markdown
## Start from the template

Two ways in. Either clone this repo (see [Quick start](#quick-start)), or install the
template package and generate a renamed copy:

```bash
dotnet new install BravoDev.AppointMe.Templates::1.1.0
dotnet new appointme -n Contoso.Booking
cd Contoso.Booking/src/Contoso.Booking.Aspire && dotnet run
```

`dotnet new appointme` renames namespaces, projects, the solution file, the database,
the Keycloak realm and the container names to your project name, and leaves the domain
vocabulary alone. It ships the Bicep infrastructure and CI workflows; it does not ship
this project's changelog, release media, or deployment config.

Pin the version — `::1.1.0` — so a generation is reproducible. The template version
tracks the app release it contains.
```

- [ ] **Step 3: Document the packaging project in CLAUDE.md**

In `CLAUDE.md`, add this subsection at the end of the "Build and Development Commands" section, immediately before `## Architecture`:

```markdown
### Template package (`dotnet new appointme`)

The repo doubles as a `dotnet new` template. `.template.config/template.json` defines
the template; `templates/AppointMe.Templates.csproj` packs the **repo root** into the
package, so there is no copied tree to keep in sync. That project is deliberately not
in `AppointMe.sln` — `dotnet build`/`dotnet test` are unaffected by it.

```bash
./templates/smoke-test.sh   # pack, install locally, generate, build + test the output
```

Two things to respect when changing anything the template touches:

- **Two exclusion layers.** Pack-time (`None` globs in the csproj) decides what enters
  the nupkg; generation-time (`sources[].modifiers[].exclude` in `template.json`)
  decides what lands in a user's output. They are not interchangeable.
- **Never replace the bare lowercase token `appointme`.** It is a prefix of
  `appointment`/`appointments` — the aggregate, the `/appointments` route, the
  `appointments.statistics:view` permission. The template replaces guarded tokens
  (`appointme-`, `appointme.`, …) precisely to avoid this. If you add a new lowercase
  brand identifier, add the matching guarded symbol and re-run the smoke test.

Renaming a project, adding a lowercase brand identifier, or adding a path that should
not ship all require `./templates/smoke-test.sh` to pass. CI runs it on every PR
(`.github/workflows/template.yml`).
```

- [ ] **Step 4: Verify the docs are accurate**

Run: `./templates/smoke-test.sh`
Expected: PASS. Then confirm the README's claimed command works verbatim against the local package:

```bash
dotnet new uninstall BravoDev.AppointMe.Templates 2>/dev/null || true
dotnet new install ./artifacts/BravoDev.AppointMe.Templates.1.1.0.nupkg
dotnet new appointme -n Contoso.Booking -o /tmp/readme-check
ls /tmp/readme-check/src/Contoso.Booking.Aspire/Contoso.Booking.Aspire.csproj
dotnet new uninstall BravoDev.AppointMe.Templates
rm -rf /tmp/readme-check
```
Expected: the csproj path listed, confirming the README's `cd` line is correct.

- [ ] **Step 5: Commit**

```bash
git add README.md CLAUDE.md Directory.Build.props
git commit -m "Document the template install path and fix the repository URL

README gains a 'Start from the template' section next to the clone path,
with a version-pinned install so generation is reproducible. CLAUDE.md
documents the packaging project, the two exclusion layers, and the rule
against replacing the bare lowercase appointme token.

RepositoryUrl said github.com/bravodev/appointme; the real remote is
bravodev-hub/appointme, and that value is embedded in the published
package metadata."
```

---

## Owner actions before the first publish

Neither can be done from a coding session — the first needs the nuget.org account, the second is a credential:

1. Reserve or confirm ownership of the `BravoDev.AppointMe.Templates` id (or the `BravoDev.*` prefix) on nuget.org.
2. Add `NUGET_API_KEY` as a repository Actions secret, scoped to that id. The `publish` job references it and also targets a `nuget` GitHub environment, so add that environment too if you want an approval gate on publishes.

Until then, everything through Task 6 is verifiable locally and in CI — pack, install, generate, build, test, lint. The `dotnet nuget push` step is the only one that stays unproven, and it only ever runs on a `v*` tag.

## Follow-up, explicitly out of scope

- An `appointme-module` sub-template that scaffolds a new bounded context (aggregate, `DbContext`, slice folders, module registration) into an existing generated project.
- Release marketing for the package: announcement, nuget.org README polish, shields.io badges.
- Removing `infra/cloudflare-worker/.wrangler` from git history (`git rm -r --cached` plus a `.wrangler/` gitignore entry) — recommended in the spec's *Side finding*, unrelated to this plan.
