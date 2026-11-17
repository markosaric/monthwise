# Requirements: Screens load on demand and a failing screen can be recovered

- **Branch:** `fix/page-loading-and-errors`
- **Status:** Approved (2026-11-16)
- **Date:** 2026-11-16
- **Source:** code review of `main`, finding M7 (medium priority)

> This document describes **what** the fix must do, not **how** it is built.
> The implementation plan (`docs/plans/fix-page-loading-and-errors.md`) is written after these
> requirements are approved.

## 1. Goal

When Monthwise opens, the browser downloads the code of every screen at once, even though the
user sees only one of them: today that is a single script file of about 1,390 kB (about 395 kB
compressed). And when a screen fails while it is being shown, the whole app turns into a blank
page with no menu and no way back except reloading the browser by hand. The main branch code review
lists both as finding M7.

After this fix, the app downloads the code of a screen only when the user first opens it, so it
starts with much less code. When a screen fails, the menu stays, a short message says that the
screen could not be shown, and the user can reload or go elsewhere.

## 2. Terms

| Term | Meaning |
|---|---|
| Screen | One page of the app reached by an address, for example the dashboard, a month, a form or a settings page. |
| App shell | The part shown around every screen: the menu on desktop and tablet, the bottom navigation on phones, the system status and the notifications. |
| Start | Opening Monthwise in a browser tab, or reloading it, at any address. |
| Release | A new version of Monthwise installed on the server (Docker, native or Azure). |
| Screen error | Anything that stops a screen from being shown: a fault in the screen itself, or its code that cannot be downloaded. |

## 3. Functional requirements

**FR-1 Screens load when they are opened**
- At start, the browser downloads the app shell and the screen of the address being opened, not
  the code of every other screen.
- The code of another screen is downloaded the first time the user opens it. Opening it again in
  the same tab does not download it again.
- Code that only some screens use (for example the charts) is downloaded only with those screens.

**FR-2 Opening a screen stays smooth**
- While the code of a screen is being downloaded, the app shell stays visible and usable.
- If the download takes longer than a moment, the screen area shows that it is loading. A fast
  download shows no loading sign, so it does not flash.
- Every address that works today keeps working, including addresses opened directly, bookmarks,
  the browser's back and forward buttons, links that return to an earlier screen, and the address
  that is redirected to the expense categories.
- An unknown address still shows "Page not found".

**FR-3 A failing screen shows a message, not a blank page**
- When a screen error happens, the app shell stays visible and usable, and the screen area shows a
  short message that the screen could not be shown.
- The message offers to reload the page and to go back to the dashboard.
- Going to another screen through the menu, the bottom navigation or the browser's back button
  shows that screen normally; the message is gone.
- The message does not show technical details (no error text, no code locations). The details are
  still written to the browser's developer console, so a problem can be investigated.
- If the app shell itself fails, the whole page shows the same message with the reload action
  instead of a blank page.

**FR-4 A new release does not break an open tab**
- When a release is installed while Monthwise is open in a tab, and the user then opens a screen
  whose code belonged to the previous release, the app reloads the page once by itself, so the
  user lands on the same address in the new release.
- If the screen still cannot be downloaded after that reload (for example the server is not
  reachable), the app shows the message of FR-3 instead of reloading again, so it never reloads in
  a loop.

## 4. Non-functional requirements

- At start on the dashboard, the browser downloads less than half of the script code it downloads
  today.
- No single script file of a release build is larger than 500 kB (before compression), so the build
  does not warn about large files.
- The fix works with the content policy of finding M5 (security headers): opening every screen of a
  release build causes no content policy violation in the browser console.
- Automated tests check the message of FR-3 for a failing screen and that the menu keeps working,
  so a later change cannot bring the blank page back unnoticed.

## 5. Out of scope

- Downloading the code of other screens in advance (for example when the pointer moves over a menu
  item).
- Working offline, or caching the app in the browser beyond what it does today.
- Showing an error message for a failed API request: screens already handle those today and they
  do not change.
- Sending screen errors to a server or a monitoring service.

## 6. Acceptance criteria

1. Opening the dashboard of a release build downloads less than half of today's script code, and
   the charts' code is not downloaded when opening a screen without charts.
2. Opening a screen not opened before downloads its code; the menu stays visible meanwhile.
3. A release build has no script file larger than 500 kB.
4. A screen that fails while shown displays the message with "Reload" and "Back to the dashboard";
   the menu still works and opening another screen shows it normally.
5. Opening a screen whose code is no longer on the server reloads the page once; if it still fails,
   the message of FR-3 is shown and the page does not reload again.
6. Every existing automated test still passes, and going through every screen of a release build
   shows no content policy violation in the browser console.
