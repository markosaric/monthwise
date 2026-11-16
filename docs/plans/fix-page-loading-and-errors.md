# Plan: Screens load on demand and a failing screen can be recovered

- **Branch:** `fix/page-loading-and-errors`
- **Status:** In progress
- **Date:** 2026-11-16
- **Requirements:** [page-loading-and-errors.md](../feature-requirements/page-loading-and-errors.md)

## Goal

The app downloads the code of a screen only when it is first opened, so it starts with much less
code (FR-1, NFR-1, NFR-2). A failing screen shows a message with "Reload" and "Back to the
dashboard" inside the app shell instead of a blank page (FR-3), and an open tab survives a new
release by reloading itself once (FR-4).

## Scope

- In scope: loading every screen on demand, a delayed loading sign, error messages for a failing
  screen and a failing app shell, the one-time reload after a release, front-end tests, a release
  build check in a browser, and a short note in `docs/INIT.md`.
- Out of scope: everything in section 5 of the requirements (preloading screens in advance,
  offline use, changes to how API errors are shown, sending errors to a server). The back-end is
  not changed: the hashed script files are already cached for a year and `index.html` is always
  revalidated (`Hosting/FrontendHosting.cs`), which is what FR-4 needs.

## Design

- **Domain / data model changes:** none.
- **API changes:** none.
- **Front-end:**
  - **`src/lib/lazyScreen.tsx` (new):** `lazyScreen(load)` takes a function that imports a screen
    module and picks the screen component, for example
    `lazyScreen(() => import('@/pages/MonthPage').then((m) => m.MonthPage))`, and returns a
    component plus a `preload()` function. Vite turns each `import()` into its own script file, so
    a screen's code (and code only it uses, such as the charts) is downloaded on first use (FR-1).
    - Before the module has loaded, the component renders `React.lazy` inside its own `Suspense`
      boundary whose fallback is `ScreenLoading`. After the module has loaded once, the component
      renders the screen directly, so opening it again downloads nothing and shows no fallback
      (FR-1 second bullet).
    - The `Suspense` boundary is inside the screen, below `<Outlet />`, so the app shell stays
      visible and usable while a screen downloads (FR-2 first bullet).
    - **One reload after a release (FR-4):** when `import()` fails, the helper checks a flag in
      `sessionStorage` (`monthwise.reloadedForNewRelease`). If the flag is not set, it sets it and
      reloads the page, so the same address opens in the new release. If the flag is already set,
      or `sessionStorage` cannot be used, it does not reload and rethrows the error, so the error
      message of FR-3 is shown and the page never reloads in a loop. Every successful screen load
      clears the flag. The reload goes through a small exported `pageReload` object so tests can
      replace it.
  - **`src/components/layout/ScreenLoading.tsx` (new):** shows a small spinner with the text
    "Loading…" (`role="status"`) only after 300 ms, so a fast download shows nothing and does not
    flash (FR-2 second bullet). React Router 7 already runs navigations in a React transition;
    the delay covers the first load and screens opened for the first time.
  - **`src/components/layout/ScreenError.tsx` (new):** the error message of FR-3, in the style of
    the existing `PageHeader` screens: the title "This screen could not be shown", one sentence
    saying the user can reload or go back, a "Reload" button (`window.location.reload()`) and a
    "Back to the dashboard" link. It reads the error with `useRouteError()` and writes it to the
    console with `console.error` once, but never shows the error text or stack (FR-3 fourth
    bullet). A `fullPage` variant centres the same message on the page background, without the
    menu, for an error in the app shell itself (FR-3 last bullet).
  - **`src/routes.tsx`:** the static imports of all screens are replaced by `lazyScreen(...)`
    definitions, one per screen component; the route paths and elements stay the same, so every
    address, bookmark, back and forward navigation and the redirect keep working (FR-2 third
    bullet). The route tree gets error elements at two levels:
    - the layout route gets `errorElement: <ScreenError fullPage />` (the app shell failed);
    - all screen routes move under one pathless child route with
      `errorElement: <ScreenError />`, so a screen error (a render fault, or a screen whose code
      cannot be downloaded) is shown inside `<Outlet />` with the menu still there (FR-3). React
      Router resets this boundary on the next navigation, so opening another screen shows it
      normally (FR-3 third bullet).
    - `NotFoundPage` and the redirect stay static imports; they are tiny and keep "Page not
      found" immediate (FR-2 last bullet). `routes.tsx` also exports `preloadScreens()`, which
      preloads every screen (used by the tests).
  - **Bundle size (NFR-1, NFR-2):** a test build with on-demand screens gave a start file of about
    466 kB (today 1,388 kB) and put the charts in their own file. No manual chunk rules are added
    unless the build in step 6 shows a file over 500 kB; Vite's default warning limit of 500 kB
    stays in place so the build warns if a file grows past it again.
  - **Content policy (M5):** the screen files are `/assets/*.js` module scripts on the own origin,
    loaded with `import()`, which `script-src 'self'` allows. No inline script or `eval` is added.
- **Database migrations:** none.
- **Docs:** the front-end row of the architecture table in `docs/INIT.md` gets a note that every
  screen in `routes.tsx` is loaded on demand through `lazyScreen`, and that a new screen must be
  added the same way.

## Steps

1. Add `lazyScreen.tsx`, `ScreenLoading.tsx` and `ScreenError.tsx`.
2. Change `routes.tsx` to on-demand screens and the two error elements, and export
   `preloadScreens()`.
3. In `src/test/renderRoute.tsx`, `await preloadScreens()` once at the top of the module, so the
   existing route tests keep rendering screens at once and need no change.
4. Add the tests below and run `npm run lint`, `npm run typecheck`, `npm test` and
   `npm run format:check`.
5. Run `npm run build` and record the size of the start file and the largest file against the
   numbers of today (acceptance 1 and 3).
6. Run a release build (the release Docker image; if it cannot be built here, the built front-end
   served with the same security headers and `Cache-Control` rules as the API) and check in a
   browser: every screen opens, the start downloads only the shell and the opened
   screen, a screen without charts does not download the charts, and the console shows no content
   policy violation (acceptance 1, 2 and 6). Then replace the build in `wwwroot` with a new one
   while a tab is open and open a screen not opened before: the page reloads once and the screen
   opens (acceptance 5).
7. Add the docs note to `docs/INIT.md`.

## Tests

- **Unit tests (`src/lib/lazyScreen.test.tsx`):**
  - While the import is pending nothing is shown at first, and "Loading…" appears after 300 ms
    (fake timers).
  - After the import resolves the screen is shown; a second render shows it at once, without the
    loading sign.
  - A failed import with no flag sets the flag and calls `pageReload.reload` once.
  - A failed import with the flag already set does not reload and the error reaches the route's
    error element.
  - A failed import when `sessionStorage` throws does not reload and shows the error element.
  - A successful import clears the flag.
- **Component tests (`src/components/layout/ScreenError.test.tsx`):**
  - The message shows "Reload" and "Back to the dashboard", does not show the error text, and
    writes the error to `console.error`.
  - "Reload" calls `window.location.reload`.
  - The `fullPage` variant shows the same message without the menu.
- **Route tests (`src/App.test.tsx`, with `vi.mock` of one screen module that throws while
  rendering):**
  - Opening that screen shows the message inside the shell; the menu is still there (acceptance 4).
  - Clicking another menu item shows that screen and the message is gone (acceptance 4).
  - An unknown address still shows "Page not found".
- **Manual checks:** steps 5 and 6.

## Open questions

- None.
