import { formatYearMonth } from '@/lib/dates'
import { formatCents } from '@/lib/money'
import { formatDate } from '@/components/cards/format'
import {
  LANGUAGE_KEY,
  browserLanguage,
  currentLanguage,
  dateLocale,
  detectLanguage,
  setLanguageForTests,
  storeLanguage,
} from './language'
import { localized, plural } from './texts'

afterEach(() => {
  window.localStorage.clear()
  setLanguageForTests('en')
  vi.restoreAllMocks()
})

describe('browserLanguage (croatian-language.md FR-2)', () => {
  it.each([
    [['hr'], 'hr'],
    [['hr-HR', 'en'], 'hr'],
    [['HR-hr'], 'hr'],
    [['en-US', 'hr'], 'en'],
    [['de-DE'], 'en'],
    [[], 'en'],
    [undefined, 'en'],
  ])('%j → %s', (preferred, expected) => {
    expect(browserLanguage(preferred)).toBe(expected)
  })
})

describe('detectLanguage', () => {
  it('uses the stored choice before the browser language', () => {
    vi.spyOn(window.navigator, 'languages', 'get').mockReturnValue(['hr-HR'])
    window.localStorage.setItem(LANGUAGE_KEY, 'en')
    expect(detectLanguage()).toBe('en')
  })

  it('follows the browser language while nothing is stored', () => {
    vi.spyOn(window.navigator, 'languages', 'get').mockReturnValue(['hr-HR'])
    expect(detectLanguage()).toBe('hr')
  })

  it('ignores a stored value that is not a language', () => {
    vi.spyOn(window.navigator, 'languages', 'get').mockReturnValue(['en-GB'])
    window.localStorage.setItem(LANGUAGE_KEY, 'fr')
    expect(detectLanguage()).toBe('en')
  })

  it('falls back to the browser language when storage is unavailable', () => {
    vi.spyOn(window.navigator, 'languages', 'get').mockReturnValue(['hr'])
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    expect(detectLanguage()).toBe('hr')
  })
})

describe('storeLanguage', () => {
  it('remembers the choice on this device', () => {
    storeLanguage('hr')
    expect(window.localStorage.getItem(LANGUAGE_KEY)).toBe('hr')
    expect(currentLanguage()).toBe('hr')
  })

  it('still applies the choice when storage is unavailable', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    storeLanguage('hr')
    expect(currentLanguage()).toBe('hr')
  })
})

describe('plural', () => {
  it.each([
    [1, 'rata'],
    [2, 'rate'],
    [4, 'rate'],
    [5, 'rata'],
    [11, 'rata'],
    [12, 'rata'],
    [21, 'rata'],
    [22, 'rate'],
    [0, 'rata'],
  ])('Croatian %i → %s', (count, expected) => {
    expect(plural(count, { one: 'rata', few: 'rate', other: 'rata' }, 'hr')).toBe(expected)
  })

  it('uses one and other in English', () => {
    const forms = { one: 'installment', few: 'never', other: 'installments' }
    expect(plural(1, forms, 'en')).toBe('installment')
    expect(plural(2, forms, 'en')).toBe('installments')
    expect(plural(5, forms, 'en')).toBe('installments')
  })
})

describe('localized', () => {
  it('picks the texts of the language', () => {
    expect(localized({ a: 'Save' }, { a: 'Spremi' }, 'en').a).toBe('Save')
    expect(localized({ a: 'Save' }, { a: 'Spremi' }, 'hr').a).toBe('Spremi')
  })
})

describe('formats (FR-5)', () => {
  it('names months in the language', () => {
    expect(formatYearMonth('2026-11')).toBe('November 2026')
    setLanguageForTests('hr')
    expect(dateLocale()).toBe('hr-HR')
    expect(formatYearMonth('2026-11')).toBe('studeni 2026.')
  })

  it('shows amounts and numeric dates the same in both languages', () => {
    const english = [formatCents(123456), formatDate('2026-11-19')]
    setLanguageForTests('hr')
    expect([formatCents(123456), formatDate('2026-11-19')]).toEqual(english)
    expect(english).toEqual(['1.234,56\u00a0€', '19.11.2026'])
  })
})
