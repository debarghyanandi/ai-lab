# ai-lab / python

The AI Engineering hands-ons, AI 7 onward. One uv project; each topic gets its own
subpackage under `src/ai_lab/` and its tests under `tests/`.

## Run

Everything goes through `uv run`, never a bare `python` or `pip`.

```powershell
cd python
uv sync                          # create .venv from uv.lock (first time, and after pulls)
uv run ruff format --check       # 1. formatting
uv run ruff check                # 2. lint
uv run pyright                   # 3. types, strict
uv run pytest                    # 4. tests
```

Add a package: `uv add <name>` (runtime) or `uv add --dev <name>` (tools only).
Commit `pyproject.toml` and `uv.lock` together. Never commit `.venv/`.

## Layout

| Path | What |
|---|---|
| `.python-version` | Python 3.13 (uv installs it if missing) |
| `pyproject.toml` | project, dependencies, and the Ruff / pyright / pytest settings |
| `uv.lock` | exact versions and hashes for every platform |
| `src/ai_lab/` | the code: `pricing.py` (AI 7) |
| `tests/` | pytest tests |

## Items

| Item | Where | What |
|---|---|---|
| AI 7 | `src/ai_lab/pricing.py`, `tests/test_pricing.py` | uv project setup · Ruff, pyright strict, pytest · token cost function |
