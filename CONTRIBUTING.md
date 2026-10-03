# Contributing to Monthwise

Working practices for this repository. Architecture and domain are described in [docs/INIT.md](docs/INIT.md).

## Feature plans

Every feature (and every non-trivial fix or refactor) starts with a written plan in `docs/plans/`.

- File name matches the branch name: branch `feat/card-reconciliation` → `docs/plans/feat-card-reconciliation.md`.
- Copy [docs/plans/_TEMPLATE.md](docs/plans/_TEMPLATE.md) and fill it in **before** writing code.
- The plan is the **first commit** on the feature branch:
  `docs(plans): add plan for card reconciliation`
- If the approach changes during implementation, update the plan in the same branch.
- When the feature is done, set the plan's status to `Done`; plans stay in the repo as history.

## Secrets and sensitive data

Never commit secrets or store them in code. This covers passwords, connection strings with
passwords, API keys, tokens, certificates and private keys, as well as personal financial data
(real bank statements, account or card numbers, exported budget files).

- Local settings go in `.env` (git-ignored; only `.env.example` with placeholder values is committed)
  and in .NET user secrets (`dotnet user-secrets`), never in `appsettings*.json`.
- CI secrets go in GitHub Actions secrets; values used only by throwaway CI containers may stay in the
  workflow.
- Test data uses made-up values only. Real data stays in git-ignored folders such as `data/private/`.
- Before committing, check `git diff --staged` for anything sensitive.
- If a secret is committed by mistake, treat it as leaked: rotate it first, then remove it from
  history before pushing. Rewriting history does not undo a push.

## Working with Git

### Branches

- `main` is always buildable and its history is **linear** (no merge commits).
- Every feature or fix gets its own short-lived branch from the latest `main`:

  | Prefix | Use |
  |---|---|
  | `feat/<short-name>` | new feature, e.g. `feat/card-reconciliation` |
  | `fix/<short-name>` | bug fix |
  | `chore/`, `docs/`, `refactor/`, `test/`, `ci/` | matching commit types (see below) |

- One branch = one feature. Do not mix unrelated changes.

### Rebase workflow

1. Start from an up-to-date `main`:
   ```bash
   git switch main && git pull --rebase
   git switch -c feat/card-reconciliation
   ```
2. Add the feature plan as the first commit (see [Feature plans](#feature-plans)), then commit in small,
   meaningful steps (Conventional Commits, below).
3. Keep the branch current by **rebasing**, never by merging `main` into it:
   ```bash
   git fetch origin
   git rebase origin/main
   ```
4. Before opening or merging a pull request, tidy the history with an interactive rebase
   (squash fixups, reword unclear messages) so every commit on `main` is meaningful and builds:
   ```bash
   git rebase -i origin/main
   ```
5. After a rebase, push with lease (never plain `--force`):
   ```bash
   git push --force-with-lease
   ```
6. Merge the pull request with **Rebase and merge** (GitHub setting: only "Allow rebase merging" enabled).
   Delete the branch after merging.

Rules:
- Never rewrite history on `main` and never force-push to `main`.
- Rebase only your own feature branches.
- Recommended config: `git config --global pull.rebase true` and `git config --global rebase.autoStash true`.

### Commit messages: Conventional Commits

All commits follow [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/):

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

**Types**

| Type | When |
|---|---|
| `feat` | a new feature for the user |
| `fix` | a bug fix |
| `docs` | documentation only |
| `style` | formatting, no code meaning change |
| `refactor` | code change that neither fixes a bug nor adds a feature |
| `perf` | performance improvement |
| `test` | adding or fixing tests |
| `build` | build system, dependencies (npm, NuGet) |
| `ci` | CI configuration |
| `chore` | other maintenance |
| `revert` | reverts a previous commit |

**Scopes** (optional): `frontend`, `backend`, `domain`, `api`, `db`, `docs`, `ci`.

**Rules**
- Description in English, imperative mood, lowercase, no trailing period, max ~72 characters:
  `feat(domain): calculate remaining installments per card`
- Body explains *why*, wrapped at 72 characters.
- Breaking changes: add `!` after the type/scope and a `BREAKING CHANGE:` footer:
  ```
  feat(api)!: rename period endpoints

  BREAKING CHANGE: /api/months is now /api/periods
  ```
- Reference issues in the footer: `Refs: #12` or `Closes: #12`.

**Examples**
```
feat(frontend): add month overview page
fix(domain): stop counting finished installment plans
docs: describe rebase workflow
test(backend): cover card statement reconciliation
build(frontend): bump vite to 7.1
```

### Pull requests

- Title follows the Conventional Commits format (it becomes the reference for the change).
- The PR links its plan in `docs/plans/`.
- Before requesting review: rebased on `origin/main`, build and tests pass locally.
- Keep pull requests small and focused on one feature.
