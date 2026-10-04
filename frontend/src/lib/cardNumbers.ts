/**
 * Client-side checks for card numbers and IBANs, mirroring the back-end value objects
 * (`CardNumber`, `Iban`). The server stays the source of truth.
 */

/** Digits only: spaces and dashes removed. */
export function normalizeCardNumber(input: string): string {
  return input.replace(/[\s-]/g, '')
}

/** 13 to 19 digits that pass the Luhn check. */
export function isValidCardNumber(input: string): boolean {
  const digits = normalizeCardNumber(input)
  if (!/^\d{13,19}$/.test(digits)) return false
  let sum = 0
  let double = false
  for (let i = digits.length - 1; i >= 0; i--) {
    let digit = Number(digits[i])
    if (double) {
      digit *= 2
      if (digit > 9) digit -= 9
    }
    sum += digit
    double = !double
  }
  return sum % 10 === 0
}

const ibanLengths: Record<string, number> = {
  AT: 20,
  BA: 20,
  BE: 16,
  CH: 21,
  CZ: 24,
  DE: 22,
  DK: 18,
  ES: 24,
  FI: 18,
  FR: 27,
  GB: 22,
  HR: 21,
  HU: 28,
  IE: 22,
  IT: 27,
  LU: 20,
  ME: 22,
  NL: 18,
  NO: 15,
  PL: 28,
  PT: 25,
  RS: 22,
  SE: 24,
  SI: 19,
  SK: 24,
}

/** Upper case without spaces. */
export function normalizeIban(input: string): string {
  return input.replace(/\s/g, '').toUpperCase()
}

/** Country code, check digits, known country length and the mod-97 check. */
export function isValidIban(input: string): boolean {
  const iban = normalizeIban(input)
  if (!/^[A-Z]{2}\d{2}[A-Z0-9]{11,30}$/.test(iban)) return false
  const expectedLength = ibanLengths[iban.slice(0, 2)]
  if (expectedLength !== undefined && iban.length !== expectedLength) return false

  const rearranged = iban.slice(4) + iban.slice(0, 4)
  let remainder = 0
  for (const char of rearranged) {
    const value = /\d/.test(char) ? char : String(char.charCodeAt(0) - 55)
    for (const digit of value) {
      remainder = (remainder * 10 + Number(digit)) % 97
    }
  }
  return remainder === 1
}

/** Groups of four for display while typing, e.g. "4111 1111 1111 1111". */
export function groupsOfFour(value: string): string {
  return value.replace(/\s/g, '').replace(/(.{4})(?=.)/g, '$1 ')
}
