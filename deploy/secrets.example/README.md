# Example secrets folder

Placeholders only. A real installation keeps one file per secret in `<config folder>/secrets/`,
created by the start or install script, readable only by the user's account. The file name is the
setting with `__` for `:` (`SensitiveData__HashKey` is `SensitiveData:HashKey`).

| File | Value |
|---|---|
| `ConnectionStrings__Monthwise` | PostgreSQL connection string with the password |
| `SensitiveData__ActiveKeyId` | `k1` |
| `SensitiveData__EncryptionKeys__k1` | base64 of 32 random bytes |
| `SensitiveData__HashKey` | base64 of 32 random bytes |
| `postgres_password` | the database password (used by PostgreSQL itself) |
