import { cardMonthToYearMonth, formatCardMonthInput, yearMonthToCardMonth } from './dates'

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
