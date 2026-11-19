import { currentLanguage, type Language } from './language'

/**
 * The Croatian texts of a texts module have exactly the shape of the English ones: the same keys,
 * and functions with the same parameters. A missing or extra text is a compile error
 * (croatian-language.md FR-9, NFR-1).
 */
export type Translation<T> = T extends string
  ? string
  : T extends (...args: infer A) => infer R
    ? (...args: A) => R extends string ? string : Translation<R>
    : T extends readonly (infer E)[]
      ? readonly Translation<E>[]
      : T extends object
        ? { readonly [K in keyof T]: Translation<T[K]> }
        : T

/** Widens literal strings of a texts object, so its translation may say something else. */
export type Texts<T> = Translation<T>

/** Picks the texts of the page's language (fixed for the life of the page). */
export function localized<T>(en: T, hr: Translation<T>, language: Language = currentLanguage()): T {
  return (language === 'hr' ? hr : en) as T
}

/**
 * The plural form of a count: English has one and other; Croatian has one (1, 21, 31 …),
 * few (2–4, 22–24 …) and other (0, 5–20, 25 …).
 */
export function plural(
  count: number,
  forms: { one: string; few?: string; other: string },
  language: Language = currentLanguage(),
): string {
  const rule = new Intl.PluralRules(language === 'hr' ? 'hr' : 'en').select(count)
  if (rule === 'one') return forms.one
  if (rule === 'few') return forms.few ?? forms.other
  return forms.other
}
