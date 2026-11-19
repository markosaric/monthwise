import { Languages } from 'lucide-react'
import { sectionColors } from '@/components/design/colors'
import { IconTile } from '@/components/design/IconTile'
import {
  currentLanguage,
  languageReload,
  languages,
  storeLanguage,
  type Language,
} from '@/i18n/language'
import { cn } from '@/lib/utils'
import { appTexts } from './texts'

/** Each language's name is written in that language (croatian-language.md FR-1). */
const languageNames: Record<Language, string> = { en: 'English', hr: 'Hrvatski' }

/**
 * The Language choice in Settings (croatian-language.md FR-1, FR-2). Choosing a language stores
 * it on this device and reloads the page, so every text, already loaded or not, follows it. The
 * Settings screen has nothing to type, so nothing is lost.
 */
export function LanguageSetting() {
  const active = currentLanguage()

  function choose(language: Language) {
    if (language === active) return
    storeLanguage(language)
    languageReload.reload()
  }

  return (
    <section aria-labelledby="language-title" className="flex flex-col gap-2">
      <h2
        id="language-title"
        className="font-sans text-xs font-semibold tracking-wider text-muted-foreground uppercase"
      >
        {appTexts.settings.language.title}
      </h2>
      <div className="flex flex-wrap items-center gap-3 rounded-xl bg-card p-4 shadow-[0_2px_8px_rgb(20_24_40/0.04)] ring-1 ring-foreground/8">
        <IconTile icon={Languages} color={sectionColors.settings} solid />
        <p id="language-description" className="min-w-0 flex-1 text-sm text-muted-foreground">
          {appTexts.settings.language.description}
        </p>
        <div
          role="radiogroup"
          aria-labelledby="language-title"
          aria-describedby="language-description"
          className="inline-flex rounded-lg border bg-muted p-0.5"
        >
          {languages.map((language) => (
            <button
              key={language}
              type="button"
              role="radio"
              aria-checked={language === active}
              lang={language}
              onClick={() => choose(language)}
              className={cn(
                'rounded-md px-3 py-1.5 text-sm text-muted-foreground hover:text-foreground focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none',
                language === active && 'bg-card font-semibold text-foreground shadow-sm',
              )}
            >
              {languageNames[language]}
            </button>
          ))}
        </div>
      </div>
    </section>
  )
}
