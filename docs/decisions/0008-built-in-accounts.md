# 0008: Built-in accounts with ASP.NET Core Identity

- **Status:** Accepted
- **Date:** 2026-11-27

## Context
Every screen and API call needs a signed-in user of the household, and only a household admin may
export and import ([login requirements](../feature-requirements/login.md)). Monthwise runs in local
development, as the Docker one-click and the native background service on the user's own
computer (only the same computer by default, optionally the home network or Tailscale over plain
HTTP) and on Azure Container Apps (public HTTPS, scales to zero). The login must behave the same on
all of them and work without an internet connection.

Options considered:
- **Built-in accounts** (username and password kept by Monthwise).
- **Sign-in with an outside service** (Microsoft Entra ID, Google, OpenID Connect): every
  installation would need its own app registration, client secret and redirect address; a home
  network address cannot be registered, and sign-in fails without internet.
- **Sign-in in front of the app** (Azure Container Apps authentication, oauth2-proxy, Cloudflare
  Access, Tailscale identity): different on every target, nothing equivalent for Docker and
  native, and the app would still need its own users and roles, read from headers.
- **Passkeys only:** they need HTTPS or `localhost`, so they fail on the home network over HTTP.

## Decision
- **ASP.NET Core Identity** (`IdentityUserContext<AppUser, Guid>` in `MonthwiseDbContext`) with
  cookie authentication. The role (household admin or member) is a column of `app_user`, not
  Identity's role tables.
- **Session keys in the database:** the Data Protection keys that protect the cookie are kept in
  `data_protection_key`, so sessions survive restarts, updates and Azure starts after scaling to
  zero, on every target.
- **First admin with a setup code:** a secret `Setup:Code`, kept like the other secrets
  ([ADR 0005](0005-encrypted-sensitive-data.md)) and created by the deploy scripts; a used code is
  remembered by its keyed hash in `setup_code_use`. A new code (one command per target) lets the
  installer reset an admin's password when nobody can sign in.
- **Cookie and request protection:** `HttpOnly`, `SameSite=Strict`, `Secure` over HTTPS; every
  changing API call needs the header `X-Monthwise-Request`, which other sites cannot send.
  Lockout after 5 wrong passwords and a rate limit per client address on sign-in and setup.
- **Every endpoint needs a signed-in user** (fallback policy); the anonymous ones are named
  explicitly. The security stamp is checked on every request, so deactivating a user or changing a
  role takes effect at once.
- The login tables are **installation tables** ([ADR 0007](0007-data-export-and-import.md)): never
  exported, never replaced by an import.

## Consequences
- Monthwise is responsible for password storage (Identity's salted PBKDF2 hasher), lockout and
  password resets; there is no email, so a forgotten password is reset by a household admin or,
  for the last admin, with a new setup code.
- One extra query per request for the security stamp check; fine for a household.
- Passkeys can be added later on top of Identity (supported in .NET 10) where HTTPS is available,
  with passwords as the fallback.
- A new anonymous endpoint must be marked on purpose; a test fails when an endpoint answers
  without a session.
