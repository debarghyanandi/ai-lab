---
name: check
description: Run the python/ quality gate (ruff format check, ruff lint, pyright strict, pytest) and report each result. Use before any commit, and when asked whether the Python code is clean.
allowed-tools:
  - Bash(uv run --directory python ruff format --check)
  - Bash(uv run --directory python ruff check)
  - Bash(uv run --directory python pyright)
  - Bash(uv run --directory python pytest)
---

Run these four commands from the repository root, in this order, one at a time.
Run all four even if an earlier one fails.

1. `uv run --directory python ruff format --check`
2. `uv run --directory python ruff check`
3. `uv run --directory python pyright`
4. `uv run --directory python pytest`

Then report a table: check | PASS or FAIL | the key line of output (counts, or the first error).
End with one line: `GATE: PASS` only if all four passed, otherwise `GATE: FAIL`.

Do not fix anything and do not edit files. Never add `# noqa`, `# pyright: ignore`,
skip or xfail markers to make a check pass. Reporting is the whole job.
