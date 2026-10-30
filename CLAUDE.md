# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo is

Hands-on exercises for an AI Engineering track, numbered "AI N". Two independent projects with no shared code:

- `python/`: the default for AI 7 onward. A single uv project. Each topic gets its own module or subpackage under `src/ai_lab/`, with tests under `tests/`.
- `dotnet/`: .NET "twins" of exercises. Currently only `src/Bench` (AI 4).

This is learning code. Short comments explain *why* a line exists (a trap it avoids, a unit, a design choice), and docs like `HOW-IT-WORKS.md` walk through the code. Keep that style in new code.

Models run locally on Ollama at `http://localhost:11434` (no cloud provider yet; Azure comes later). When an exercise is added, update the item table in the root `README.md` and, for Python work, the tables in `python/README.md`.

## Python (`python/`)

Run everything from `python/` through `uv run`. Never use a bare `python` or `pip`.

```powershell
uv sync                                  # create .venv from uv.lock
uv run ruff format --check               # formatting
uv run ruff check                        # lint (E, F, I, B, UP, SIM, RUF; line length 100)
uv run pyright                           # strict type checking over src/ and tests/
uv run pytest                            # all tests
uv run pytest tests/test_pricing.py::test_local_model_costs_nothing   # a single test
```

- Python 3.13. pyright runs in **strict** mode, so all code needs full type annotations, including test functions (`-> None`).
- pytest runs with `--strict-markers --strict-config` and `xfail_strict`.
- Add dependencies with `uv add <pkg>` or `uv add --dev <pkg>`. Commit `pyproject.toml` and `uv.lock` together.
- The package is `ai_lab` (src layout, `py.typed`). Tests import it as `from ai_lab.<module> import ...`.

## .NET (`dotnet/`)

`net10.0` with SDK pinned in `global.json`. `Directory.Build.props` enables nullable and implicit usings and sets **`TreatWarningsAsErrors`**, so any warning breaks the build. The solution file is `AiLab.slnx`. There is no test project.

```powershell
cd dotnet
dotnet build
dotnet run -c Release --project src/Bench   # needs Ollama running with the models pulled
```

### Bench (AI 4)

Bench sends the 10 prompts in `prompt.json` to each model listed in `Program.cs`, using fixed settings (temperature 0, seed 42, num_predict 512, num_ctx 4096). It then runs a 4K vs 32K context experiment and writes `results/<date>.md`. `src/Bench/HOW-IT-WORKS.md` explains the flow, where to change things and the C# idioms used. Read it before changing Bench.

Bench calls Ollama's HTTP API directly (`/api/generate`, `/api/ps`) instead of going through Microsoft.Extensions.AI, because that abstraction hides Ollama's timing fields. Keep it that way.

Invariants the code deliberately guards against, which must survive edits:
- Ollama JSON is snake_case. Deserialize with `JsonNamingPolicy.SnakeCaseLower`, otherwise every number silently reads as 0.
- Send `stream: false`. Ollama streams by default.
- The HttpClient timeout is 10 min, because the default 100 s is too short for cold loads or CPU spill.
- A warm-up `LoadAsync` must use the same `num_ctx` as the measured calls, otherwise Ollama reloads inside the first timed call.
- Unload each model (`keep_alive: 0`) before the next one, so two models never share the 8 GB VRAM.
- `EvalCount == 0` aborts the run rather than writing fake numbers.
- Timing comes from Ollama's own nanosecond durations, not a stopwatch.

Answer checks in `prompt.json` (`contains`, `number`, `json`, `bullets`, `manual`) are implemented in `PromptCase.cs`. `manual` returns `null` and means the answer is scored by hand.

The `results/` path is relative to the current directory, so run Bench from `dotnet/`, which is where the committed results live. Committed result files have hand-added manual scores and conclusions. Don't overwrite them.

## Claude Code setup (`.claude/`)

- Run `/check` before every commit; it must end with `GATE: PASS`.
- Hooks block edits to `.env` and `uv.lock`, and run `ruff format` after any `.py` edit under `python/`.
- After changing Python, use the `py-reviewer` subagent on the changed files.
