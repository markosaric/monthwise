import { centsToEuroInput, formatCents, parseEuroInput, parseSignedEuroInput } from './money'

// Intl uses a non-breaking space before the currency sign.
const normalize = (value: string) => value.replace(/\s/g, ' ')

describe('formatCents', () => {
  it('formats cents as euros', () => {
    expect(normalize(formatCents(123456))).toBe('1.234,56 €')
  })

  it('formats zero and negative amounts', () => {
    expect(normalize(formatCents(0))).toBe('0,00 €')
    expect(normalize(formatCents(-5))).toBe('-0,05 €')
  })

  it('rejects fractional cents', () => {
    expect(() => formatCents(10.5)).toThrow(RangeError)
  })
})

describe('parseEuroInput', () => {
  it.each([
    ['1.234,56', 123456],
    ['1234,5', 123450],
    ['1234.56', 123456],
    ['1.500', 150000],
    ['250', 25000],
    ['  1 000,00 € ', 100000],
    ['0', 0],
  ])('parses "%s" as %i cents', (input, cents) => {
    expect(parseEuroInput(input)).toBe(cents)
  })

  it.each(['', 'abc', '-5', '1,234', '12,3,4'])('rejects "%s"', (input) => {
    expect(parseEuroInput(input)).toBeNull()
  })
})

describe('centsToEuroInput', () => {
  it('formats cents for editing', () => {
    expect(centsToEuroInput(123456)).toBe('1234,56')
    expect(centsToEuroInput(150000)).toBe('1500')
    expect(centsToEuroInput(5)).toBe('0,05')
  })

  it.each([
    [-1, '-0,01'],
    [-50, '-0,50'],
    [-99, '-0,99'],
    [-100, '-1'],
    [-150, '-1,50'],
    [0, '0'],
  ])('keeps the sign of %i cents', (cents, text) => {
    expect(centsToEuroInput(cents)).toBe(text)
  })

  it.each([-150, -99, -50, -1, 0, 5, 150, 123456])(
    'gives %i cents back after parsing a signed amount',
    (cents) => {
      expect(parseSignedEuroInput(centsToEuroInput(cents))).toBe(cents)
    },
  )
})
