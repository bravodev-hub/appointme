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

# tracked repo dotfiles that NuGet's own default-exclude would otherwise silently drop
assert_nupkg_present '\.editorconfig'
assert_nupkg_present '\.gitignore'
assert_nupkg_present '\.dockerignore'
assert_nupkg_present 'src/AppointMe\.Frontend/\.prettierrc'

# extensionless files: NuGet nests the source path under any PackagePath whose final
# segment has no extension (e.g. LICENSE, Dockerfile), unless the packaging project
# repairs it post-pack - assert both the flat path and the absence of the doubled one
assert_nupkg_present 'LICENSE'
assert_nupkg_absent 'LICENSE/LICENSE'
assert_nupkg_present 'src/AppointMe\.Api/Dockerfile'
assert_nupkg_absent 'src/AppointMe\.Api/Dockerfile/src/AppointMe\.Api/Dockerfile'

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
