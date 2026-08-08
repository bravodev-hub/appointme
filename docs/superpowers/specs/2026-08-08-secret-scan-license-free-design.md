# License-free secret-scan — design

**Date:** 2026-08-08
**Status:** approved

## Goal

`secret-scan` workflow passes without a `GITLEAKS_LICENSE`:
`gitleaks-action@v2` demands a license key for organization repos; the OSS
gitleaks binary does not.

## Change

In `.github/workflows/secret-scan.yml`, replace the `gitleaks/gitleaks-action@v2`
step (and its license comment block) with a pinned binary download:

```yaml
      - name: Run gitleaks
        env:
          GITLEAKS_VERSION: 8.30.1
        run: |
          curl -sSfL "https://github.com/gitleaks/gitleaks/releases/download/v${GITLEAKS_VERSION}/gitleaks_${GITLEAKS_VERSION}_linux_x64.tar.gz" | tar -xz gitleaks
          ./gitleaks git . --config .gitleaks.toml --no-banner --redact
```

Unchanged: `checkout@v7` with `fetch-depth: 0` (full-history scan via
`gitleaks git`), the repo's `.gitleaks.toml`, fail-on-finding semantics.
`--redact` keeps matched values out of CI logs. Binary over Docker avoids
container git-ownership friction.

## Verification

Push → `secret-scan` run green (first ever pass); job log shows gitleaks
scanned the full history with the repo config.
