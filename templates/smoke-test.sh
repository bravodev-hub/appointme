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
  # prefix/pattern below but DO ship, matching the individual re-include None items
  case "$path" in
    docker/keycloak/certs/.gitkeep|src/AppointMe.Frontend/.env.development)
      return 1 ;;
  esac
  case "$path" in
    artifacts/*|templates/*|.superpowers/*|docs/superpowers/*|docs/images/*|\
infra/cloudflare-worker/.wrangler/*|docker/keycloak/certs/*|\
CHANGELOG.md|docs/CODE_REVIEW_REPORT.md|src/AppointMe.Api/appsettings.Devtest.json|\
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
assert_absent "src/AppointMe.Api/appsettings.Devtest.json"
assert_absent "infra/cloudflare-worker/.wrangler"
assert_absent ".claude/settings.json"
assert_absent "templates/AppointMe.Templates.csproj"
assert_absent ".template.config"
assert_absent ".github/workflows/template.yml"
assert_absent ".superpowers"

assert_present_file "LICENSE"
assert_present_file "src/AppointMe.Api/Dockerfile"
assert_present_file ".gitignore"
assert_present_file ".editorconfig"
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
