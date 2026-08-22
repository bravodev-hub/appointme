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
assert_absent ".superpowers"

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
