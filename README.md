# ai-lab

Hands-ons for the AI Engineering track: `python/` by default, `dotnet/` for the .NET twins.
Models run locally on Ollama (`http://localhost:11434`) until the Azure window.

| Item | Folder | What |
|---|---|---|
| AI 4 | `dotnet/src/Bench` | Ollama setup · benchmark three models: tokens/s and quality on 10 prompts |
| AI 7 | `python/` | uv project · Ruff, pyright strict, pytest · token cost function (`src/ai_lab/pricing.py`) |
| AI 9 | `.claude/` | Claude Code setup: `/check` skill · `py-reviewer` subagent · hooks protecting `.env` and `uv.lock`, ruff format after edits |
