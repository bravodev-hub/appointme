#!/usr/bin/env bash
# Packs the template, installs it from the local nupkg, generates a project,
# and asserts the result. This is the harness for the template work: it is what
# CI runs and what a human runs locally.
set -euo pipefail

# Pinned once, here, rather than prefixing individual sort/comm/grep calls: the
# manifest-parity check below sorts two streams and then diffs them with `comm`, and
# `comm` collates using its OWN locale, independent of whatever collated its inputs.
# If only the two `sort` calls were pinned to C and `comm` ran under the ambient
# locale (e.g. en_US.UTF-8), the two collations can disagree on ordering - observed
# concretely on this machine: docker/keycloak/certs/.gitkeep moves relative to
# LICENSE/README.md between the two collations, so `comm` reports the same file as
# simultaneously missing and extra. Pinning LC_ALL for the whole script means every
# sort/comm/grep in this file (present or future) collates identically, so the two
# halves of that comparison can't drift apart again.
export LC_ALL=C

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
# INT/TERM too, not just EXIT: without this, Ctrl-C during the multi-minute pack step
# leaves $PKG_ID installed globally on the machine instead of cleaning up. `cleanup`
# itself never calls exit, so `trap cleanup INT TERM` alone would just resume the
# script after the signal instead of terminating it - fine if the interrupt lands
# during the pack step (the next lines fail on an empty $NUPKG and exit anyway), but
# not if it lands during `dotnet new install`, which would then carry on against a
# deleted $OUT_DIR. Explicit `exit` on INT/TERM triggers the EXIT trap in turn, so
# `cleanup` still runs exactly once, and the exit code reflects the signal.
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

fail() { echo "FAIL: $*" >&2; FAILURES=$((FAILURES + 1)); }
pass() { echo "ok: $*"; }

# unzip is required by every nupkg assertion below; fail fast with a clear message
# rather than have `unzip -Z1`/`unzip -l` produce an empty listing that every
# assert_nupkg_absent call would then pass against vacuously.
if ! command -v unzip >/dev/null 2>&1; then
  fail "unzip is required by this script but was not found on PATH"
  exit 1
fi

# --- pack -------------------------------------------------------------------
echo "== packing =="
rm -rf "$REPO_ROOT/artifacts"
dotnet pack "$REPO_ROOT/templates/AppointMe.Templates.csproj" \
  -c Release -o "$REPO_ROOT/artifacts"

# `|| true` matters under `set -eo pipefail`: without it, a failing `ls` (no match)
# makes this whole assignment's exit status non-zero, and `set -e` aborts the script
# right here - before the `[[ -z "$NUPKG" ]]` guard below ever runs, so the intended
# "FAIL: no nupkg produced" line would never print.
NUPKG="$(ls "$REPO_ROOT"/artifacts/$PKG_ID.*.nupkg 2>/dev/null | head -1 || true)"
if [[ -z "$NUPKG" ]]; then fail "no nupkg produced"; exit 1; fi
pass "packed $(basename "$NUPKG")"

# --- assertions: nupkg contents ---------------------------------------------
# These check the .nupkg itself, not the generated project: some packaging defects
# (local-only files leaking in, tracked dotfiles silently dropped, extensionless
# files getting nested under a same-named folder) live entirely at pack time and
# would otherwise go unnoticed since the template engine strips/reshapes some paths
# on generation. Patterns are anchored to the full path under content/appointme/ and
# to end-of-line, so a bare filename can't match an unrelated longer path.
echo "== asserting nupkg contents =="
NUPKG_LISTING="$(unzip -l "$NUPKG")"

assert_nupkg_absent() {
  if grep -qE "content/appointme/$1\$" <<< "$NUPKG_LISTING"; then
    fail "$1 should not be in the nupkg"
  else
    pass "absent from nupkg: $1"
  fi
}
assert_nupkg_present() {
  if grep -qE "content/appointme/$1\$" <<< "$NUPKG_LISTING"; then
    pass "present in nupkg: $1"
  else
    fail "$1 missing from the nupkg"
  fi
}

# local-only artifacts that must never ship (never repo content - see the Exclude
# list in AppointMe.Templates.csproj)
assert_nupkg_absent 'docker/keycloak/certs/keycloak\.crt'
assert_nupkg_absent 'docker/keycloak/certs/keycloak\.key'
assert_nupkg_absent '\.claude/settings\.local\.json'
assert_nupkg_absent '\.claude/scheduled_tasks\.lock'
assert_nupkg_present 'docker/keycloak/certs/\.gitkeep'

# the two most sensitive withheld paths (live Entra tenant/client id + demo password;
# Cloudflare account id + email) - asserted against the nupkg itself, not just
# generated output below, because template.json's generation-time exclude list is a
# second, independent layer that later tasks edit; today it happens to agree with the
# pack-time Exclude list, but that is not guaranteed to stay true.
assert_nupkg_absent 'src/AppointMe\.Api/appsettings\.Devtest\.json'
assert_nupkg_absent 'infra/cloudflare-worker/\.wrangler/.*'

# tracked repo dotfiles that NuGet's own default-exclude would otherwise silently drop
assert_nupkg_present '\.editorconfig'
assert_nupkg_present '\.gitignore'
assert_nupkg_present '\.dockerignore'
assert_nupkg_present 'src/AppointMe\.Frontend/\.prettierrc'

# extensionless files: NuGet nests the source path under any PackagePath whose final
# segment has no extension (e.g. LICENSE, Dockerfile), unless the packaging project
# special-cases them with a PackagePath ending in a directory separator - assert both
# the flat path and the absence of the doubled one
assert_nupkg_present 'LICENSE'
assert_nupkg_absent 'LICENSE/LICENSE'
assert_nupkg_present 'src/AppointMe\.Api/Dockerfile'
assert_nupkg_absent 'src/AppointMe\.Api/Dockerfile/src/AppointMe\.Api/Dockerfile'

# --- assertion: full nupkg manifest parity ----------------------------------
# The named checks above are easier to read when they fail, but they are necessarily
# a hand-picked sample - as of this writing they'd miss 6 of the ~10 dotfiles NuGet's
# default-exclude drops, any *new* untracked local file, and a third extensionless
# file added later. This is the structural check that closes that gap for good: derive
# the expected file set from git (source of truth for "the repo"), remove exactly the
# paths this package deliberately withholds, and diff that against what the nupkg
# actually contains. Deliberately bash-only (no Python dependency in the harness).
echo "== asserting full nupkg manifest parity =="

# True (exit 0) if $1 is a path this package deliberately withholds from the nupkg -
# mirrors the Exclude list and the two explicit re-includes in
# AppointMe.Templates.csproj. Keep the two lists in sync; this is what makes 1:1 nupkg
# parity with git-tracked content a guarantee instead of a spot check.
is_withheld() {
  local path="$1"
  # explicit exceptions first: tracked files that live under an otherwise-withheld
  # prefix/pattern below but DO ship, matching the individual re-include None items.
  # templates/overlay/* is here, not an oversight: templates/* below withholds this
  # packaging project's own templates/ folder (csproj, smoke-test.sh) wholesale, but
  # templates/overlay/** is packed by its own None item in AppointMe.Templates.csproj
  # (content/appointme/templates/overlay/...) precisely so it CAN be remapped onto the
  # generated project's root at generation time -- it must not be withheld here.
  case "$path" in
    docker/keycloak/certs/.gitkeep|src/AppointMe.Frontend/.env.development|templates/overlay/*)
      return 1 ;;
  esac
  case "$path" in
    artifacts/*|templates/*|.superpowers/*|docs/superpowers/*|docs/images/*|\
infra/cloudflare-worker/.wrangler/*|docker/keycloak/certs/*|\
CHANGELOG.md|README.md|docs/CODE_REVIEW_REPORT.md|src/AppointMe.Api/appsettings.Devtest.json|\
.claude/settings.json|AppointMe.sln.DotSettings.user|.github/workflows/template.yml|\
.claude/*.local.json|*/.claude/*.local.json|.claude/*.lock|*/.claude/*.lock|\
.env|*/.env|.env.*|*/.env.*)
      return 0 ;;
  esac
  return 1
}

# Built with a plain while-read loop rather than `mapfile`/`readarray`: this needs to
# run under whatever `bash` a contributor's machine resolves to, and macOS still ships
# bash 3.2 by default (no mapfile) unless a newer one is installed and put on PATH.
EXPECTED_FILES=()
while IFS= read -r f; do
  EXPECTED_FILES+=("$f")
done < <(
  git -C "$REPO_ROOT" ls-files | while IFS= read -r f; do
    is_withheld "$f" || printf '%s\n' "$f"
  done | sort
)

NUPKG_FILES=()
while IFS= read -r f; do
  NUPKG_FILES+=("$f")
done < <(
  unzip -Z1 "$NUPKG" | grep '^content/appointme/' | sed 's#^content/appointme/##' | sort
)

MISSING="$(comm -23 <(printf '%s\n' "${EXPECTED_FILES[@]}") <(printf '%s\n' "${NUPKG_FILES[@]}"))"
EXTRA="$(comm -13 <(printf '%s\n' "${EXPECTED_FILES[@]}") <(printf '%s\n' "${NUPKG_FILES[@]}"))"

if [[ -z "$MISSING" && -z "$EXTRA" ]]; then
  pass "nupkg manifest matches git-tracked content minus withheld paths exactly (${#EXPECTED_FILES[@]} files)"
else
  while IFS= read -r line; do
    [[ -n "$line" ]] && fail "missing from nupkg (tracked, not withheld, not shipped): $line"
  done <<< "$MISSING"
  while IFS= read -r line; do
    [[ -n "$line" ]] && fail "unexpected extra in nupkg (not tracked, or should be withheld): $line"
  done <<< "$EXTRA"
fi

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
assert_present_file() {
  if [[ -f "$GEN_DIR/$1" ]]; then
    pass "present (file): $1"
  elif [[ -e "$GEN_DIR/$1" ]]; then
    fail "$1 exists in generated output but is not a regular file"
  else
    fail "$1 missing from generated output"
  fi
}

assert_absent "CHANGELOG.md"
assert_absent "docs/superpowers"
assert_absent "docs/CODE_REVIEW_REPORT.md"
assert_absent "docs/images"
assert_absent "src/$NAME.Api/appsettings.Devtest.json"
assert_absent "infra/cloudflare-worker/.wrangler"
assert_absent ".claude/settings.json"
assert_absent "templates/$NAME.Templates.csproj"
assert_absent ".template.config"
assert_absent ".github/workflows/template.yml"
assert_absent ".superpowers"

assert_present_file "LICENSE"
assert_present_file "src/$NAME.Api/Dockerfile"
assert_present_file ".gitignore"
assert_present_file ".editorconfig"
assert_present "THIRD-PARTY-NOTICES.md"
assert_present "global.json"
assert_present "compose.yaml"
assert_present "Directory.Packages.props"
assert_present "docs/identity-resolution.md"
assert_present "src/$NAME.Api/appsettings.Devtest.example.json"
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

# --- assertions: rename correctness ----------------------------------------
echo "== asserting rename correctness =="

# If generation silently produced an empty/wrong tree, every check below would
# either compare 0 == 0 (vacuous pass) or have grep hit a missing directory and
# swallow its I/O-error exit code the same way a "no matches" exit code gets
# swallowed - a real failure disguised as "no residual AppointMe". Fail loudly
# and skip straight to the build/test section instead of letting that happen.
if [[ ! -d "$GEN_DIR/src" ]]; then
  fail "$GEN_DIR/src does not exist -- generation must have failed; skipping rename-correctness checks"
else

# Domain vocabulary must survive byte-for-byte. Pinned to the literal baseline
# (not just "source count == generated count") so a broken grep that silently
# counts 0 on both sides can't pass vacuously; source is also asserted against
# the same literal so a legitimate future change to the domain code surfaces
# here as a deliberate "update this baseline" edit rather than silent drift.
EXPECTED_APPOINTMENT=792
SRC_APPOINTMENT="$(grep -rIoh "Appointment" "$REPO_ROOT/src" --exclude-dir=node_modules --exclude-dir=obj --exclude-dir=bin --exclude-dir=dist | wc -l | tr -d ' ')"
GEN_APPOINTMENT="$(grep -rIoh "Appointment" "$GEN_DIR/src" --exclude-dir=node_modules --exclude-dir=obj --exclude-dir=bin --exclude-dir=dist | wc -l | tr -d ' ')"
if [[ "$SRC_APPOINTMENT" != "$EXPECTED_APPOINTMENT" ]]; then
  fail "source Appointment count is $SRC_APPOINTMENT, expected $EXPECTED_APPOINTMENT -- update EXPECTED_APPOINTMENT if the source legitimately changed"
elif [[ "$GEN_APPOINTMENT" != "$EXPECTED_APPOINTMENT" ]]; then
  fail "generated Appointment count is $GEN_APPOINTMENT, expected $EXPECTED_APPOINTMENT -- the rename corrupted the domain vocabulary"
else
  pass "Appointment survived intact ($GEN_APPOINTMENT occurrences)"
fi

for token in "appointments.statistics:view" "/appointments"; do
  if grep -rIq -- "$token" "$GEN_DIR/src" --exclude-dir=node_modules --exclude-dir=obj --exclude-dir=bin --exclude-dir=dist; then
    pass "survived: $token"
  else
    fail "$token missing from generated output"
  fi
done

# --- PascalCase brand token: where each generated form comes from ----------
# template.json drives the rename with three symbols, all matching the same
# literal search text "AppointMe" (case-sensitive), disambiguated purely by the
# single character that immediately follows the match (`onlyIf: [{"before":
# X}]`) -- never by a bare, ungated "AppointMe" catch-all, since two symbols
# both matching that shorter text would leave replacement order unspecified
# (the same trap sourceName's own derived lowercase form sits in for
# "appointments").
#
#   .  " ' \n \r ; - <  (identityBucket)       -> dotted, as typed: namespaces,
#                                                  paths, prose, connection
#                                                  strings. Also the only
#                                                  symbol with fileRename --
#                                                  every path in this repo is
#                                                  "AppointMe.Xyz" (dot-
#                                                  followed), and fileRename
#                                                  does not honor onlyIf the way
#                                                  replaces does, so a second
#                                                  symbol declaring fileRename
#                                                  would silently race it.
#   _                    (safeBucketUnderscore) -> dot_to_underscore. Exactly
#                                                  one case: Program.cs's
#                                                  AddProject<AppointMe_Api>
#                                                  must match the type name
#                                                  .NET Aspire's own SDK
#                                                  source-generates from the
#                                                  renamed .csproj file name
#                                                  (dots -> underscores).
#   S M A H O D E J 1    (safeBucketCompact)    -> invalid characters deleted,
#                                                  not substituted. AppointMeSql
#                                                  is simultaneously a real C#
#                                                  identifier (ConnectionStrings.cs,
#                                                  needs underscore-or-delete)
#                                                  and an Aspire resource-name
#                                                  string validated by
#                                                  ASPIRE006, which explicitly
#                                                  rejects underscores -- delete
#                                                  is the only form valid in
#                                                  both roles for the identical
#                                                  search text.
#
# This is a CLOSED enumeration over these 19 followers (9 + 1 + 9 above),
# independently verified against every file that reaches $GEN_DIR (git-tracked,
# minus withheld paths, minus template.json's own excludes) -- not a sample,
# except `\r`: on the LF checkout this repo and CI both use, `\r` has zero
# actual occurrences: it is a defensive hedge for a `core.autocrlf=true`
# checkout, where the one occurrence that currently sits right before a bare
# `\n` (README.md's "# AppointMe" heading) would have its follower become `\r`
# instead of `\n`, falling out of every bucket. Every other follower listed
# above was independently measured, not guessed. Adding a new
# AppointMe-prefixed C# identifier with a follower character outside this list
# (e.g. AddAppointMeBilling, follower "B") falls out of every bucket: the
# "no residual AppointMe" check below still catches it (loudly, in CI), but
# whoever adds it then has to add a `{ "before": "B" }` entry to
# safeBucketCompact (or identityBucket, if it's a non-identifier position) in
# template.json themselves. There is no way to make this self-updating; this
# comment is the only record of the rule.
#
# --- lowercase brand token: the second, independent token family -----------
# "appointme" (lowercase) is a PREFIX of "appointment" -- unlike "AppointMe"
# above, it cannot be matched bare at all: any bare-"appointme" symbol would
# also rewrite the Appointment aggregate, the /appointments route and
# appointments.statistics:view. So the guard character is baked directly into
# each symbol's own search text ("appointme-", "appointme.", ...) instead of
# being enforced via `onlyIf` the way the PascalCase family does it above --
# there is no bare "appointme" catch-all anywhere, gated or not.
#
# Three base value symbols, then one `generated`/`join` symbol per follower
# character that joins the base value with that literal follower (consuming
# and immediately re-emitting it, so the character itself is unchanged):
#
#   lowerDotted  (lowerCaseInvariant) -> case-folded, separators preserved
#                exactly as typed. Followers: . ' " / ` ; (tokDot, tokApos,
#                tokQuot, tokSlash, tokTick, tokSemi). tokDot is also the only
#                symbol with fileRename (the same fileRename-ignores-onlyIf
#                reason as identityBucket above -- see the fileRename note in
#                the assertions below). This bucket is NOT a free stylistic
#                choice: SuperAdminRegistryTests.should_match_email_case_
#                insensitively compares this rename's allowlist literal
#                ("demo@appointme.dev") against the PascalCase family's
#                identity-preserving rename of "Demo@AppointMe.DEV" -- both
#                must fold to the same string, which only holds if this bucket
#                preserves the user's own separators (e.g. the dot in
#                "Contoso.Booking") exactly as identityBucket does, rather
#                than normalizing them to hyphens. The same value is also what
#                keeps `'@/api/appointme'` import specifiers (tokApos) resolving
#                to the file tokDot renames, and what keeps main.bicep's
#                apostrophe-quoted values in sync with main.json's
#                double-quoted compiled equivalents (tokQuot) and with
#                appointme-realm.json's own realm name and URL-path segments
#                (tokQuot, tokSlash).
#   lowerKebab   (kebabCase) -> hyphen-normalized (dots/underscores become
#                hyphens). Followers: - \n \r (tokDash, tokNewline, tokCR).
#                Required, not stylistic: tokDash covers the three Aspire/
#                Keycloak resource-name strings ("appointme-sql",
#                "appointme-api", "appointme-frontend") that ASPIRE006
#                restricts to ASCII letters, digits and hyphens (no dots) --
#                the same constraint that drove safeBucketCompact's "delete"
#                transform above, just hyphen-safe instead of hyphen-free
#                since these are strings, not C# identifiers. tokNewline
#                covers compose.yaml's `name: appointme` Docker Compose
#                project name, which Compose validates as lowercase
#                alphanumeric plus hyphen/underscore -- no dots allowed --
#                alongside two harmless README.md shell examples that share
#                the same "followed by end-of-line" shape. tokCR is the same
#                `core.autocrlf=true` defensive hedge as identityBucket's own
#                `\r` follower above, extended to this family for symmetry:
#                on such a checkout, an `\n` follower becomes `\r`, and
#                without tokCR every one of tokNewline's occurrences --
#                compose.yaml's Compose project name and the two README.md
#                examples -- would fall out of every lowercase bucket instead
#                of just changing which one catches them. Like identityBucket's
#                `\r`, it has zero actual occurrences on the LF checkout this
#                repo and CI both use; it exists for the checkout that isn't
#                this one. tokDash is also the only lowercase symbol with
#                fileRename (renames appointme-realm.json).
#   lowerCompact (compactSafeNameLower: lowerCaseInvariant then delete every
#                non-alphanumeric character) -> Followers: A : $ { d _ (tokA,
#                tokColon, tokDollar, tokBrace, tokD, tokUnder). Each follower
#                here sits in a context stricter than either bucket above
#                allows: tokA is Program.cs's `var appointmeApi = ...` C# local
#                (a literal hyphen or dot mid-identifier would not compile);
#                tokColon is orval.config.ts's unquoted object key
#                `appointme: { ... }` (a hyphen there is a JS syntax error --
#                caught for real by this task's own `npm run lint`); tokDollar
#                and tokBrace are main.bicep's/main.json's ACR and storage-
#                account name construction ("acrappointme${...}" /
#                "acrappointme{0}{1}"), which Azure restricts to lowercase
#                alphanumeric only (no hyphens, no dots); tokD is the
#                illustrative "acrappointmedevtest.azurecr.io" description
#                string for that same ACR name, kept consistent with it rather
#                than given its own style; tokUnder is main.bicepparam's
#                `appointme_admin` SQL admin login example.
#
# This is a CLOSED enumeration over these 15 followers (6 + 3 + 6 above),
# independently measured with a byte-level scan (not `grep`, which -- see the
# residual-check note below -- silently strips line-terminating newlines
# before matching and would have missed the `\n` follower entirely) over the
# reachable file set (every git-tracked file minus is_withheld()'s paths minus
# template.json's own `modifiers.exclude` globs minus `.template.config/`),
# not a sample: 208 non-domain occurrences total on the LF checkout this repo
# and CI both use (`\r` contributes zero of them here -- see tokCR above),
# zero left over once all 15 followers are routed. Adding a new lowercase
# "appointme"-prefixed string with a follower
# character outside this list falls out of every bucket: the residual check
# below still catches it (loudly, in CI), but whoever adds it then has to add
# a new `tok*` symbol to template.json themselves, choosing whichever of the
# three base values above (or a new one) is valid for that occurrence's own
# context -- there is no way to make this self-updating; this comment is the
# only record of the rule.
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

fi

# --- build and test the generated solution ---------------------------------
# Restore is split from build (`dotnet restore` then `dotnet build --no-restore`)
# rather than left as one combined `dotnet build`. Root cause, established from
# the exact failure across Tasks 2-4 (~1 run in 3): "error : The file
# '.../obj/<Project>.csproj.nuget.g.props' already exists." -- this solution has
# several projects sharing a common *.Contracts reference; a combined
# `dotnet build` triggers MSBuild's own implicit, per-entry-point restore, and
# when multiple entry-point projects that share that reference build in
# parallel, each independently restores its own transitive closure, so the
# shared project's generated .g.props/.g.targets get written more than once,
# concurrently, by unrelated build nodes. A single `dotnet restore` over the
# whole solution computes one combined project graph up front and restores
# each project exactly once before any build node starts, so there is nothing
# left to race by the time `--no-restore` build begins. This removes the
# race's trigger rather than tolerating it with a retry.
echo "== restoring generated solution =="
if ! dotnet restore "$GEN_DIR/$NAME.sln"; then
  fail "generated solution failed to restore"
fi

echo "== building generated solution =="
if dotnet build "$GEN_DIR/$NAME.sln" -c Release --no-restore; then
  pass "generated solution builds"
else
  fail "generated solution does not build"
fi

echo "== testing generated solution =="

# `|| true` matters under `set -e`: dotnet test's own exit code is nonzero
# whenever any test fails, which would otherwise abort the script before the
# assertions below get to inspect the output.
TEST_OUTPUT="$(dotnet test "$GEN_DIR/$NAME.sln" -c Release --no-build 2>&1)" || true
echo "$TEST_OUTPUT"

# Each `|| true` matters for the same reason: under `set -eo pipefail`, a
# `grep` that matches nothing (the expected case for a fully clean run) exits
# 1, pipefail propagates that through the rest of the pipe even though `awk`
# succeeds trivially on the resulting empty input, and `set -e` would abort
# the script right here -- before the zero-failure success branch below ever
# runs.
TOTAL_TESTS="$(grep -oE 'Total:[[:space:]]*[0-9]+' <<< "$TEST_OUTPUT" | grep -oE '[0-9]+' | awk '{s+=$1} END{print s+0}' || true)"
FAILED_TESTS="$(grep -oE 'Failed:[[:space:]]*[0-9]+' <<< "$TEST_OUTPUT" | grep -oE '[0-9]+' | awk '{s+=$1} END{print s+0}' || true)"

if [[ "$TOTAL_TESTS" != "217" ]]; then
  fail "generated solution ran $TOTAL_TESTS tests, expected 217 -- tests were lost, not just failed"
elif [[ "$FAILED_TESTS" -eq 0 ]]; then
  pass "generated solution tests pass (217/217)"
else
  FAILING_NAMES="$(grep -E '^  Failed ' <<< "$TEST_OUTPUT" | sed -E 's/^  Failed ([^ ]+).*/\1/' || true)"
  fail "generated solution tests failed: $FAILED_TESTS failing (expected 0): $FAILING_NAMES"
fi

# --- assertions: lowercase brand tokens -------------------------------------
# See the "lowercase brand token" comment above the PascalCase residual check
# for the full design (three base value symbols, one guarded tok* symbol per
# follower character, why each bucket is required rather than stylistic).
echo "== asserting lowercase brand tokens are gone =="

if [[ ! -d "$GEN_DIR/src" ]]; then
  fail "$GEN_DIR/src does not exist -- generation must have failed; skipping lowercase-rename checks"
else

# Every legitimate lowercase brand token is followed by something other than
# "n" -- "appointmen..." is always the domain word and must be left alone. So
# any lowercase "appointme" NOT followed by "n" is a leak. The `|$` alternative
# matters and is not decorative: plain `grep` strips each line's terminating
# newline before matching, so a bare `[^n]` class alone silently misses
# "appointme" sitting at the very end of a line -- exactly the `\n`-follower
# case documented above (two occurrences in README.md, one in compose.yaml
# before this rename). `$` (end of line) catches that case the same way
# `[^n]` catches a same-line follower; verified directly against both grep
# flavors on this machine (macOS's own /usr/bin/grep and this shell's `grep`)
# with a synthetic fixture before relying on it here.
LEAKS="$(grep -rIoE 'appointme([^n]|$)' "$GEN_DIR" \
  --exclude-dir=node_modules --exclude-dir=obj --exclude-dir=bin --exclude-dir=dist \
  2>/dev/null | sort | uniq -c | sort -rn || true)"
if [[ -n "$LEAKS" ]]; then
  echo "--- residual lowercase brand tokens ---" >&2
  echo "$LEAKS" >&2
  echo "--- files ---" >&2
  grep -rIlE 'appointme([^n]|$)' "$GEN_DIR" \
    --exclude-dir=node_modules --exclude-dir=obj --exclude-dir=bin --exclude-dir=dist >&2 || true
  fail "generated output still contains lowercase brand tokens"
else
  pass "no residual lowercase brand tokens"
fi

# Lowercase-named files were renamed too (tokDash and tokDot are the only
# lowercase symbols carrying fileRename, one per file above).
#
# The two src/api files are only covered indirectly above (the frontend's `tsc -b`
# has to resolve their import path, or the build fails) -- but nothing else asserts
# the realm file's *new* name positively, only the old name's absence. If tokDash's
# fileRename ever produced a name diverging from what src/AppointMe.Aspire/Program.cs
# passes to WithRealmImport(...), this harness would stay green and Keycloak would
# only fail at container-startup, at runtime. NAME_KEBAB mirrors the `kebabCase`
# value-transform (lowercased, separators normalized to hyphens) well enough for the
# harness's own fixed default name ("Contoso.Booking" -> "contoso-booking"); it is
# not a general-purpose kebab-case implementation.
NAME_KEBAB="$(echo "$NAME" | tr '[:upper:]' '[:lower:]' | tr '._' '-')"
assert_present_file "src/$NAME.Aspire/$NAME_KEBAB-realm.json"
assert_absent "src/$NAME.Aspire/appointme-realm.json"
assert_absent "src/$NAME.Frontend/src/api/appointme.ts"
assert_absent "src/$NAME.Frontend/src/api/appointme.schemas.ts"

# ...but the appointment-named paths must survive untouched. Counted
# case-insensitively (matching both the PascalCase "Appointment" aggregate/
# module paths under src/Booking and the lowercase "appointment" frontend
# route paths under src/AppointMe.Frontend) since an unguarded rename by
# EITHER token family could corrupt this vocabulary, and this is the
# regression check for both at once. Scoped to src/ (where all 65 of them
# live) and pruned of bin/obj/node_modules/dist identically on both sides:
# by this point in the script the generated solution has already been built
# and the frontend not yet, so an unpruned `find` on the generated side would
# additionally count hundreds of bin/obj paths that were never part of the
# template's own content and have nothing to do with this rename, while the
# source side (never built) has none of those -- an apples-to-oranges
# comparison that the brief's own version of this check does not avoid.
# Pinned against a literal baseline rather than "source == generated" alone,
# so a broken `grep`/`find` returning 0 on both sides can't pass vacuously.
# Counts relative to "$1/src" (via a subshell `cd`, not an absolute `find "$1/src"`):
# every line `find` prints otherwise carries the `$1` prefix verbatim, and both
# callers below pass a prefix that itself contains "appointment"-adjacent text
# ($REPO_ROOT / $GEN_DIR under a checkout or temp dir) — an absolute-path count
# would match on the PREFIX, not the file names under src/, and could pass
# vacuously (or overcount) regardless of what the rename actually did to the
# tree it's supposed to be checking.
count_appointment_paths() {
  ( cd "$1/src" && find . -type d \( -name bin -o -name obj -o -name node_modules -o -name dist \) -prune -o -type f -print ) \
    | grep -ic "appointment" || true
}
EXPECTED_APPOINTMENT_PATHS=65
SRC_PATHS="$(count_appointment_paths "$REPO_ROOT")"
GEN_PATHS="$(count_appointment_paths "$GEN_DIR")"
if [[ "$SRC_PATHS" != "$EXPECTED_APPOINTMENT_PATHS" ]]; then
  fail "source appointment-named path count is $SRC_PATHS, expected $EXPECTED_APPOINTMENT_PATHS -- update EXPECTED_APPOINTMENT_PATHS if the source legitimately changed"
elif [[ "$GEN_PATHS" != "$EXPECTED_APPOINTMENT_PATHS" ]]; then
  fail "appointment-named path count changed: generated $GEN_PATHS, expected $EXPECTED_APPOINTMENT_PATHS -- the rename corrupted a domain path"
else
  pass "appointment-named paths preserved ($GEN_PATHS)"
fi

# --- frontend ----------------------------------------------------------------
echo "== building generated frontend =="
(
  cd "$GEN_DIR/src/$NAME.Frontend"
  npm ci && npm run lint && npm run build
) && pass "generated frontend lints and builds" || fail "generated frontend failed"

fi

# --- assertions: overlay README --------------------------------------------
# This repo's own README.md is release marketing (live-demo link, tour GIF,
# docs/images references the template does not ship) -- it is withheld at pack
# time (AppointMe.Templates.csproj) and replaced by templates/overlay/README.md,
# which template.json's second `sources` entry maps onto the generated project's
# root. Checked against $GEN_DIR directly (not gated behind the `src/` existence
# checks above): the README lives at the generated root regardless of whether
# src/ generated correctly, so it can and should be asserted unconditionally.
echo "== asserting overlay README =="
# assert_present_file (-f), not assert_present (-e): a directory literally named
# "README.md" would satisfy -e and pass vacuously. This is the same -e-vs--f hole
# that hid the LICENSE/Dockerfile directory-nesting defect back in Task 1.
assert_present_file "README.md"
assert_absent        "templates/overlay"

# NOT `grep -q "app.appointme.dev"`: "appointme." is exactly tokDot's `replaces`
# search text, so template.json's already-shipped (Task 3) rename machinery
# rewrites that substring on EVERY generation, regardless of which README ships.
# The marketing README's own live-demo link (README.md:9, "app.appointme.dev")
# would be mangled into e.g. "app.contoso.booking.dev" whether or not this task's
# fix is in place -- so a literal-domain check can never fail for the reason its
# message would claim; it is vacuous. Verified empirically: reverting the overlay
# mapping and re-running left this exact check passing while the marketing
# README was still shipping (see the Task 4 fix-round report).
#
# "## Live demo" is rename-immune instead: it is the marketing README's own
# section heading (README.md:7), and it contains neither "AppointMe" nor
# "appointme" as a substring in any case, so no symbol in EITHER token family
# (the PascalCase family's `identityBucket`/`safeBucket*`, or the lowercase
# family's `tok*`) has any search text that could match inside it. Checked
# against both families, not just tokDot.
if grep -q "## Live demo" "$GEN_DIR/README.md" 2>/dev/null; then
  fail "generated README still contains the AppointMe marketing README's Live demo section"
else
  pass "generated README has no live-demo section"
fi

# "docs/images" is rename-immune by the same standard: it contains neither
# "AppointMe" nor "appointme" as a substring, so it cannot be touched by either
# token family's replace rules and this check is not vulnerable to the same
# vacuity as the literal-domain check above.
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

echo
if [[ $FAILURES -gt 0 ]]; then echo "$FAILURES assertion(s) failed" >&2; exit 1; fi
echo "smoke test passed"
