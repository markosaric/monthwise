import {
  groupsOfFour,
  isValidCardNumber,
  isValidIban,
  normalizeCardNumber,
  normalizeIban,
} from './cardNumbers'

describe('card numbers', () => {
  it.each(['4111111111111111', '4111 1111 1111 1111', '5555-5555-5555-4444', '378282246310005'])(
    'accepts %s',
    (value) => expect(isValidCardNumber(value)).toBe(true),
  )

  it.each(['', '4111111111111112', '411111111111', '41111111111111111111', '4111a11111111111'])(
    'rejects "%s"',
    (value) => expect(isValidCardNumber(value)).toBe(false),
  )

  it('normalizes and groups', () => {
    expect(normalizeCardNumber('4111 1111-1111 1111')).toBe('4111111111111111')
    expect(groupsOfFour('4111111111111111')).toBe('4111 1111 1111 1111')
  })
})

describe('IBANs', () => {
  it.each(['HR1210010051863000160', 'hr12 1001 0051 8630 0016 0', 'DE89 3704 0044 0532 0130 00'])(
    'accepts %s',
    (value) => expect(isValidIban(value)).toBe(true),
  )

  it.each(['', 'HR1310010051863000160', 'HR121001005186300016', '1210010051863000160HR'])(
    'rejects "%s"',
    (value) => expect(isValidIban(value)).toBe(false),
  )

  it('normalizes', () => {
    expect(normalizeIban('hr12 1001 0051 8630 0016 0')).toBe('HR1210010051863000160')
  })
})
