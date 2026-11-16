# Plan: The browser is told how to protect the app

- **Branch:** `fix/security-headers`
- **Status:** In progress
- **Date:** 2026-11-16
- **Requirements:** [security-headers.md](../feature-requirements/security-headers.md)

## Goal

Every answer of Monthwise tells the browser to run only the app's own scripts, not to show the app
inside a frame, not to guess file types, not to send the Monthwise address to other sites and,
over HTTPS, to keep using HTTPS for a year (FR-1 to FR-4). The app keeps working as before (FR-5).

## Scope

- In scope: one API middleware that adds the security headers to every answer, its place in the
  pipeline, API tests, a release build check in a browser, and a short note in the docs.
- Out of scope: everything in section 5 of the requirements (login, cross-site request checks,
  HTTP to HTTPS redirect, HSTS preload, violation reports). The Vite development server is not
  changed; it is a development tool, not a way of running a release.

## Design

- **Domain / data model changes:** none.
- **API (`Hosting/SecurityHeaders.cs`, new):** `UseSecurityHeaders()` registers a middleware that
  adds the headers in `Response.OnStarting`. Setting them when the answer starts (not before
  `next`) keeps them on answers rebuilt by the exception handler, which clears the headers of the
  failed answer (FR-1). The header values are constants in this one class (NFR-1):
  - `Content-Security-Policy` (FR-2, FR-3):
    `default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:;
    font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'none';
    form-action 'self'; frame-src 'none'; frame-ancestors 'none'`.
    - `script-src 'self'` without `'unsafe-inline'` or `'unsafe-eval'`: only the app's own script
      files run, so text the user types or imports cannot run as a script.
    - `style-src 'unsafe-inline'`: sonner (notifications) adds its styles as a `<style>` element
      at run time (FR-2 third bullet). This allows styles only, not scripts.
    - `img-src data:`: small images (icons) that the build or components put inline as data
      addresses.
    - `connect-src 'self'`: the API calls, export download and import upload use the own origin.
  - `X-Frame-Options: DENY` for browsers that do not know `frame-ancestors` (FR-3).
  - `X-Content-Type-Options: nosniff` (FR-3).
  - `Referrer-Policy: no-referrer` (FR-3).
  - `Permissions-Policy: camera=(), microphone=(), geolocation=(), payment=(), usb=(),
    browsing-topics=()` (FR-3).
  - `Strict-Transport-Security: max-age=31536000` only when `Request.IsHttps` (FR-4). Behind the
    trusted proxy the forwarded headers middleware has already set the scheme to `https`, so the
    same check covers Azure; plain HTTP installations get no HSTS. No `includeSubDomains` and no
    `preload` (out of scope; a home install may share a domain with other services). The built-in
    `UseHsts` is not used: it skips `localhost` and sets the header before `next`, so the
    exception handler would drop it.
  - Development only: answers under `/scalar` and `/openapi` get no `Content-Security-Policy`,
    because the API reference page runs inline scripts and loads fonts from Scalar's servers
    (FR-5 last bullet). The other headers still apply. In a release these addresses do not exist.
- **Pipeline (`Program.cs`):** `app.UseSecurityHeaders()` right after `app.UseProxyHosting()`
  (so the scheme is already the forwarded one) and before the exception handler, the status code
  pages, the migration gate, the import gate and the front-end, so every answer, including 404s
  and gate refusals, passes through it (FR-1). Docker, the native install and Azure all run this
  same pipeline.
- **Front-end:** no change. The built `index.html` loads only `/assets/...` module scripts and
  style sheets, and the fonts are bundled (`@fontsource-variable`). If the release build check
  finds a violation, the fix goes in the policy constants, not in a relaxed default.
- **Database migrations:** none.
- **Docs:** `docs/INIT.md` (or the hosting section that describes the proxy) gets one paragraph:
  the app sends its security headers itself, where they are set, and that HSTS is sent only over
  HTTPS.

## Steps

1. Add `Hosting/SecurityHeaders.cs` with the constants and the middleware.
2. Call `app.UseSecurityHeaders()` in `Program.cs` after `UseProxyHosting()`.
3. Add `SecurityHeadersTests.cs` (below) and run the API test suite.
4. Build the front-end, run a release build locally and go through the screens in a browser,
   checking the console for policy violations (acceptance 4) and a framing page (acceptance 3).
5. Open the API reference page in development (acceptance 5).
6. Add the docs paragraph.

## Tests

- **API / integration tests (`SecurityHeadersTests.cs`, using `MonthwiseApiFactory` with a
  temporary `wwwroot` like `ReleaseHostingTests`):**
  - A screen (`/month`), a script file (`/assets/index-abc123.js`), an API answer
    (`/api/health`), an unknown API address (404) and an unknown file (404) carry the CSP,
    `X-Frame-Options`, `nosniff`, `Referrer-Policy` and `Permissions-Policy` (acceptance 1).
  - An answer refused by the import gate (gate held while the request is made, as in
    `DataEndpointTests`) and one refused by the migration gate carry them too (acceptance 1).
  - An answer rebuilt by the exception handler (a 500) carries them too (FR-1).
  - Over `https://` the answers carry `Strict-Transport-Security` with `max-age` of at least one
    year; over `http://` they do not (acceptance 2).
  - Behind the proxy, a request from a trusted network with `X-Forwarded-Proto: https` gets HSTS,
    and the same request from an untrusted address does not (acceptance 2).
  - The CSP has no `'unsafe-inline'` or `'unsafe-eval'` in `script-src` (FR-2), so a later change
    cannot weaken it unnoticed (NFR-2).
  - In development `/scalar` has no CSP but keeps the other headers.
- **Unit tests:** none beyond the above; the middleware has no logic outside the pipeline.
- **Manual checks:** step 4 and 5: every screen of a release build with a chart, a notification,
  a dialog, export and import, with no CSP violation in the console; a local page that frames
  Monthwise shows nothing; the API reference page opens in development.

## Open questions

- None.
