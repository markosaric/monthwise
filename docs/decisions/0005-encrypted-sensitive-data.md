# 0005: Card numbers and IBANs encrypted in the application

- **Status:** Accepted
- **Date:** 2026-10-04

## Context
The card settings feature stores full card numbers and IBANs
([requirements](../feature-requirements/credit-card-settings.md), NFR-1). A database dump or backup
alone must not reveal them, they must never be logged, and the same number must not be registered
twice.

## Decision
- **Encryption in the application, not the database:** Infrastructure encrypts `CardNumber` and
  `Iban` with AES-256-GCM through EF Core value converters (`ISensitiveDataCipher`). Each value gets
  a random nonce. It is stored as `v1:<keyId>:<base64(nonce|ciphertext|tag)>`, with the key id bound
  as associated data.
- **Keys from configuration:** `SensitiveData:ActiveKeyId`, `SensitiveData:EncryptionKeys:<id>`
  and `SensitiveData:HashKey` are 256-bit base64 keys. Locally they live in user secrets, in
  production in the host's secret store, and never in `appsettings*.json` or the repository. The
  API refuses to start without valid keys.
- **Key rotation:** add a new key id, make it active, and keep the old key so existing values stay
  readable. Re-encrypting old values is a separate task when needed.
- **Uniqueness without decrypting:** a keyed HMAC-SHA256 of the normalised number is stored next to
  each value (`card_number_hash`, `iban_hash`). Partial unique indexes on those hashes skip archived
  cards.
- **Masking:** the last four digits (and the IBAN country) are stored unencrypted, so lists and the
  Dashboard never load the numbers. The numbers are a table-split entity (`CardSensitiveData`),
  loaded only on reveal and edit. API responses carry only masked values, except the reveal
  endpoint, which returns one number with `Cache-Control: no-store`.
- **No leaks through text:** `CardNumber.ToString()` and `Iban.ToString()` return the masked form;
  validation messages never repeat the value; EF sensitive data logging stays off.

## Consequences
- Losing the keys makes stored numbers unreadable: back them up separately from the database.
- The numbers cannot be searched or sorted in SQL; only equality checks through the hashes work.
- Changing the hash key requires recomputing all hashes.
- The EF model caches the converters and therefore the cipher once per process; all code in one
  process (including tests) must use the same keys.
