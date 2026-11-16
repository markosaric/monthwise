import { z } from 'zod'

// The content policy forbids eval (security headers FR-2). Without this, zod probes for it with
// `new Function` on its first parse, and the browser reports that as a policy violation. Imported
// first in main.tsx, so it runs before any module parses with zod.
z.config({ jitless: true })
