"""PostToolUse hook: run `ruff format` on a .py file under python/ that Claude just edited.

It runs AFTER the edit succeeded, so it cannot prevent anything; it tidies up.
Exit 2 here does not undo the edit: it shows stderr to Claude so it can fix the file.
"""

import json
import os
import subprocess
import sys
from pathlib import Path


def main() -> int:
    event = json.load(sys.stdin)
    path = Path(event["tool_input"]["file_path"]).resolve()
    project = (Path(os.environ["CLAUDE_PROJECT_DIR"]) / "python").resolve()

    # Only Python files inside python/; everything else is not our business.
    if path.suffix != ".py" or not path.is_relative_to(project):
        return 0

    result = subprocess.run(
        ["uv", "run", "--directory", str(project), "ruff", "format", str(path)],
        capture_output=True,
        text=True,
        timeout=60,  # a hung formatter must not hang the session
    )
    if result.returncode != 0:
        # ruff format fails on a syntax error: tell Claude instead of hiding it.
        print(result.stderr or result.stdout, file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
