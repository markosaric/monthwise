# Requirements: The browser is told how to protect the app

- **Branch:** `fix/security-headers`
- **Status:** Approved (2026-11-16)
- **Date:** 2026-11-16
- **Source:** code review of `main`, finding M5 (medium priority)

> This document describes **what** the fix must do, not **how** it is built.
> The implementation plan (`docs/plans/fix-security-headers.md`) is written after these
> requirements are approved.

## 1. Goal

Monthwise serves its screens and its API without any of the instructions a browser uses to protect
a web app. A browser therefore lets any other site show Monthwise inside a frame, may guess the
type of a file and run it as something else, and would run a script injected into a screen from
anywhere. The main branch code review lists this as finding M5: the app is served without a
content policy, without a "do not guess the type" instruction, without a frame rule and without
HTTPS enforcement.

After this fix, every answer of the app tells the browser to load the app's code only from the app
itself, not to show the app inside another site, not to guess file types, and, when the app is
reached over HTTPS, to keep using HTTPS. The app keeps working exactly as before.

## 2. Terms

| Term | Meaning |
|---|---|
| Answer | Anything the app sends back to the browser: a screen, a script, a style sheet, a font, an image, an API answer, an error answer, an export file. |
| Own origin | The address Monthwise itself is served from (scheme, host and port). |
| Content policy | The rule that tells the browser from where a page may load scripts, styles, fonts, images and data, and what else it may do. |
| HTTPS enforcement | The instruction that tells the browser to reach this address over HTTPS only, for a set time, even if the user types `http://`. |

## 3. Functional requirements

**FR-1 Every answer carries the protections**
- Every answer of the app carries the protections of FR-2 to FR-4, including error answers, answers
  for addresses that do not exist and answers refused before they reach the app's logic (for
  example while the database is being updated or an import is running).
- This holds for every way of running a release: Docker, the native install and Azure.

**FR-2 Content policy**
- The screens may load scripts, style sheets, fonts, images and data only from the own origin.
- Only the app's own script files run: scripts written into a page and scripts from any other
  address do not run.
- Styles that the app's own code adds while it runs (for example by the notification component)
  keep working; this does not allow scripts.
- No plugins or embedded objects may be loaded, forms may only be sent to the own origin, and the
  base address of the page cannot be changed.
- Nothing the user types or imports can run as a script, even if it ends up in a screen.

**FR-3 Framing, file types and leaks**
- No other site may show Monthwise inside a frame. Monthwise does not show itself in a frame either.
- The browser does not guess the type of an answer; it uses the type the app states.
- When the user follows a link from Monthwise to another site, that site is not told which
  Monthwise address the user came from.
- The browser does not give the app access to the camera, microphone, location or other device
  features, which it never uses.

**FR-4 HTTPS enforcement**
- When the app is reached over HTTPS, directly or through a trusted proxy (Azure), the browser is
  told to use HTTPS only for this address for at least one year.
- When the app is reached over plain HTTP (for example a Docker or native install used at home or
  on the local network), HTTPS enforcement is not sent, so those installations keep working over
  HTTP.

**FR-5 The app keeps working**
- Every screen, chart, notification, dialog, font and icon works as before.
- Export still downloads the file and import still uploads it.
- Using the app normally causes no content policy violations in the browser.
- In development, the API reference page keeps opening.

## 4. Non-functional requirements

- The protections are set in one place, so a later change (for example a new allowed source)
  touches one place.
- Automated tests check the protections on a screen, an API answer and an error answer, so a later
  change cannot drop them unnoticed.

## 5. Out of scope

- Login (finding H1) and refusing cross-site requests (finding H2).
- Redirecting plain HTTP to HTTPS: on Azure the platform already serves HTTPS only, and home
  installations may run on HTTP.
- Registering Monthwise in a browser's built-in HTTPS-only list (HSTS preload).
- Reporting content policy violations to a server.

## 6. Acceptance criteria

1. A screen, a script file, an API answer, a 404 answer and an answer refused by the database or
   import gate all carry the protections of FR-2 and FR-3.
2. Over HTTPS, directly or through a trusted proxy, the answers carry HTTPS enforcement of at least
   one year; over plain HTTP they do not.
3. A page on another site that shows Monthwise in a frame shows nothing.
4. Going through every screen of a release build, including a chart, a notification, export and
   import, shows no content policy violation in the browser console.
5. In development, the API reference page still opens.
