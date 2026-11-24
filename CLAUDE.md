# Working in this repository

This file is loaded by every Claude Code session here. It only points at the rules; it does not copy them.

- **You are the implementer.** Follow [docs/testing/README.md → The implementer](docs/testing/README.md#the-implementer):
  what you may do, what only a person decides (Questions, risk ratings and statuses, merges, repository settings),
  verbatim hand-offs, one step at a time, results reported as they are.
- **The workflow** for a new feature, endpoint or fix is the ten steps in
  [docs/testing/README.md → How the work flows](docs/testing/README.md#how-the-work-flows): `qe-analyst` →
  test owner → `qe-test-designer` → implement → `qe-reviewer` → CI gates → merge.
- **The gates** every pull request must pass are listed once, in
  [docs/testing/QUALITY_GATES.md §1](docs/testing/QUALITY_GATES.md#1-gates). Run `scripts/verify.sh` (or
  `scripts/verify.ps1`) before pushing; a new line of service, access-policy or frontend code needs a test that
  would fail if the line broke (G9).
- **Never weaken a test** to make it pass: no skips without `Known gap R-xxx` or `#issue`, no retries, no broader
  assertions (AI rule 4).
- **Branches:** work on a feature branch from `origin/master`, never on `master`; a person merges.
