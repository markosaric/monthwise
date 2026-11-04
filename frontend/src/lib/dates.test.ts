import {
  cardMonthToYearMonth,
  dateInputToIso,
  formatCardMonthInput,
  formatDateInput,
  isInvalidDateEntry,
  isoToDateInput,
  yearMonthToCardMonth,
} from './dates'

describe('formatCardMonthInput', () => {
  it.each([
    ['', ''],
    ['0', '0'],
    ['02', '02'],
    ['022', '02/2'],
    ['0228', '02/28'],
    ['02/28', '02/28'],
    ['02/', '02'],
    ['02282', '02/28'],
    ['02 / 28', '02/28'],
    ['ab', ''],
  ])('formats "%s" as "%s"', (input, expected) => {
    expect(formatCardMonthInput(input)).toBe(expected)
  })
})

describe('cardMonthToYearMonth', () => {
  it.each([
    ['02/28', '2028-02'],
    ['01/25', '2025-01'],
    ['12/99', '2099-12'],
  ])('converts "%s" to "%s"', (input, expected) => {
    expect(cardMonthToYearMonth(input)).toBe(expected)
  })

  it.each(['', '13/28', '00/28', '2/28', '02/2', '0228', 'ab/cd', '02/2028'])(
    'rejects "%s"',
    (input) => {
      expect(cardMonthToYearMonth(input)).toBeNull()
    },
  )
})

describe('yearMonthToCardMonth', () => {
  it('converts a stored month to MM/YY', () => {
    expect(yearMonthToCardMonth('2028-02')).toBe('02/28')
    expect(yearMonthToCardMonth('2025-01')).toBe('01/25')
  })
})

describe('formatDateInput', () => {
  it.each([
    ['', ''],
    ['1', '1'],
    ['16', '16'],
    ['161', '16.1'],
    ['1611', '16.11'],
    ['16112', '16.11.2'],
    ['16112026', '16.11.2026'],
    ['16.11.2026', '16.11.2026'],
    ['16/11/2026', '16.11.2026'],
    ['16. 11. 2026', '16.11.2026'],
    ['161120261', '16.11.2026'],
    ['16.', '16'],
    ['ab', ''],
  ])('formats "%s" as "%s"', (input, expected) => {
    expect(formatDateInput(input)).toBe(expected)
  })
})

describe('dateInputToIso', () => {
  it.each([
    ['16.11.2026', '2026-11-16'],
    ['01.01.2026', '2026-01-01'],
    ['31.12.2026', '2026-12-31'],
    ['29.02.2028', '2028-02-29'],
    ['29.02.2000', '2000-02-29'],
  ])('converts "%s" to "%s"', (input, expected) => {
    expect(dateInputToIso(input)).toBe(expected)
  })

  it.each([
    '',
    '31.02.2026',
    '29.02.2026',
    '29.02.2100',
    '31.04.2026',
    '00.11.2026',
    '16.13.2026',
    '16.00.2026',
    '16.11.26',
    '16.11',
    '16.11.0000',
    '6.11.2026',
    '2026-11-16',
  ])('rejects "%s"', (input) => {
    expect(dateInputToIso(input)).toBeNull()
  })
})

describe('isoToDateInput', () => {
  it('shows a stored date as DD.MM.YYYY and keeps anything else', () => {
    expect(isoToDateInput('2026-11-16')).toBe('16.11.2026')
    expect(isoToDateInput('')).toBe('')
    expect(isoToDateInput('16.1')).toBe('16.1')
  })
})

describe('isInvalidDateEntry', () => {
  it('is true only for a filled-in value that is not a date', () => {
    expect(isInvalidDateEntry('')).toBe(false)
    expect(isInvalidDateEntry('2026-11-16')).toBe(false)
    expect(isInvalidDateEntry('16.11')).toBe(true)
    expect(isInvalidDateEntry('31.02.2026')).toBe(true)
  })
})
