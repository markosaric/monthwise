import { describe, expect, it } from 'vitest'
import { basisPointsToPercentInput, formatBasisPoints, parsePercentInput } from './percent'

describe('parsePercentInput', () => {
  it.each([
    ['8,33', 833],
    ['8.33', 833],
    ['12,75 %', 1275],
    ['5', 500],
    ['0,5', 50],
    ['100', 10_000],
  ])('parses %s as %d basis points', (text, expected) => {
    expect(parsePercentInput(text)).toBe(expected)
  })

  it.each(['', 'abc', '-1', '8,333', '1,2,3'])('refuses %s', (text) => {
    expect(parsePercentInput(text)).toBeNull()
  })
})

describe('basis points', () => {
  it('turn back into an editable percentage', () => {
    expect(basisPointsToPercentInput(833)).toBe('8,33')
    expect(basisPointsToPercentInput(1250)).toBe('12,50')
    expect(basisPointsToPercentInput(500)).toBe('5')
  })

  it('are shown as a percentage', () => {
    expect(formatBasisPoints(833)).toMatch(/^8,33\s%$/)
  })
})
