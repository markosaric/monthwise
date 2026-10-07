# CLAUDE.md

Monthwise: home finance app (React + TypeScript front-end, .NET 10 Web API back-end, PostgreSQL).

- Architecture, domain and data model: `docs/INIT.md`
- Working practices: `CONTRIBUTING.md`. Follow it strictly, in particular:
  - feature workflow: create the branch from the latest `main` first, then requirements, plan and implementation; nothing is committed or pushed before its step is approved, and every commit message is approved before the commit
  - after implementing, leave all changes staged (not committed) for review; commit and push only after the review
  - the final step is a pull request, merged with Squash and merge (one commit per feature on `main`, titled with the PR title)
  - every feature starts with an approved requirements document in `docs/feature-requirements/` (what, never how), committed first on the branch and pushed after approval
  - then a plan in `docs/plans/<branch-name>.md` (from `docs/plans/_TEMPLATE.md`) that links its requirements, committed and pushed after approval, before any code
  - requirements already merged to `main` are never rewritten: changes go in a numbered appendix at the end of the requirements document, and the plan of the changing branch links and repeats that appendix
  - one feature per branch (`feat/...`, `fix/...`), branched from the latest `main`
  - update branches with `git rebase origin/main`, never merge `main` into a branch
  - push rebased branches with `git push --force-with-lease`; never force-push `main`
  - every commit message follows Conventional Commits 1.0.0 (`type(scope): description`)
- Money is stored as integer cents (EUR); never use floating point for amounts.
- Never commit secrets or store them in code (passwords, connection strings with passwords, API keys, tokens, real financial data). Use `.env` and .NET user secrets; see "Secrets and sensitive data" in `CONTRIBUTING.md`.
