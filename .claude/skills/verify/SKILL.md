---
name: verify
description: Build, launch, and drive the AppointMe stack to verify a change end-to-end at its runtime surface.
---

# Verifying AppointMe changes at runtime

## Launch

```bash
cd src/AppointMe.Aspire && dotnet run   # run in background; needs Docker up
```

API is reachable on `https://localhost:7233` after ~20–30s (poll `GET /api/v1/me`, returns 200 anonymous).
Frontend (Vite, proxies `/api` to the backend) on `https://localhost:5173`. Keycloak on `http://localhost:8082`.
All local certs are self-signed — always `curl -k`.

## Getting an authenticated cookie session without a browser

Demo mode is enabled in local dev. One curl call signs in and fills a cookie jar:

```bash
curl -sk -c jar.txt "https://localhost:7233/api/v1/login/demo"      # 302 = success, sets appointme.auth
curl -sk -b jar.txt -c jar.txt https://localhost:7233/api/v1/me     # confirm isAuthenticated:true; refreshes XSRF-TOKEN
```

## Antiforgery (CSRF) notes for mutations

Every GET issues `XSRF-TOKEN` (readable) + `appointme.csrf` (HttpOnly) cookies. Cookie-authenticated
unsafe methods under `/api` require header `X-XSRF-TOKEN: <XSRF-TOKEN cookie value>` or they 400 with
title "Antiforgery Validation Error". Bearer requests are exempt. Tokens are identity-bound — after
login/logout, refresh via any GET before mutating:

```bash
TOKEN=$(grep XSRF-TOKEN jar.txt | awk '{print $NF}')
curl -sk -b jar.txt -H "X-XSRF-TOKEN: $TOKEN" -X POST https://localhost:7233/api/v1/<route> ...
```

## Useful anonymous surfaces

- `GET /api/v1/me` — anonymous-friendly, cheap liveness + auth-state check.
- `POST /api/v1/signup` — anonymous mutation, handy for exercising middleware without login
  (note: empty `{}` body returns 500 from the handler itself).
- `POST /api/v1/logout` — real authenticated mutation; 302 to Keycloak end-session on success,
  and it cleans up the demo session.

## Gotchas

- Stop the Aspire process when done if it wasn't running before you started.
- `/admin/jobs` (Hangfire) redirects (302) when unauthenticated — not an error.
