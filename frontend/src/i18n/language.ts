/**
 * The app's language (croatian-language.md FR-1, FR-2). It is fixed for the life of the page:
 * every text module picks its texts once, when it is first loaded, so no screen can mix the two
 * languages. Choosing another language stores it and reloads the page.
 */
export type Language = 'en' | 'hr'

export const languages: readonly Language[] = ['en', 'hr']

/** Where the chosen language is remembered, on this device and browser only (FR-2). */
export const LANGUAGE_KEY = 'monthwise.language'

function stored(): Language | null {
  try {
    const value = window.localStorage.getItem(LANGUAGE_KEY)
    return value === 'en' || value === 'hr' ? value : null
  } catch {
    // Storage can be unavailable (private mode); fall back to the browser language.
    return null
  }
}

/** Croatian when the browser's first preferred language is Croatian, English otherwise. */
export function browserLanguage(preferred: readonly string[] | undefined): Language {
  const first = preferred?.[0]?.toLowerCase() ?? ''
  return first === 'hr' || first.startsWith('hr-') ? 'hr' : 'en'
}

/** The stored choice, or the browser language the first time (FR-2). */
export function detectLanguage(): Language {
  if (typeof window === 'undefined') return 'en'
  return (
    stored() ??
    browserLanguage(
      window.navigator.languages?.length ? window.navigator.languages : [window.navigator.language],
    )
  )
}

let current: Language = detectLanguage()

/** The language of this page. */
export function currentLanguage(): Language {
  return current
}

/** Stores the choice; the caller reloads the page so every text follows it. */
export function storeLanguage(language: Language): void {
  try {
    window.localStorage.setItem(LANGUAGE_KEY, language)
  } catch {
    // Without storage the choice still applies to this visit.
  }
  current = language
}

/** Tests only: switch the language of the modules loaded after this call. */
export function setLanguageForTests(language: Language): void {
  current = language
}

/** The locale for month and day names; amounts and numeric dates do not follow it (FR-5). */
export function dateLocale(language: Language = current): string {
  return language === 'hr' ? 'hr-HR' : 'en-GB'
}

/** How the page is reloaded after a language change; tests replace it. */
export const languageReload = {
  reload: () => window.location.reload(),
}
