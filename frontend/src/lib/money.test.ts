import { formatCents } from './money'

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
