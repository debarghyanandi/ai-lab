"""PreToolUse hook: block Edit/Write on files that must never be hand-edited.

Claude Code sends the pending tool call as JSON on stdin.
Exit 0 = allow. Exit 2 = block, and stderr is shown to Claude as the reason.
Exit 1 would NOT block, so every failure path here exits 2 (fail closed).
"""

import json
import re
import sys

# file name -> why it is protected (this text goes back to Claude)
PROTECTED: dict[str, str] = {
    ".env": "it holds secrets; ask the user to edit it",
    "uv.lock": "it changes only through `uv add` or `uv lock`, never by hand",
}


def main() -> int:
    try:
        event = json.load(sys.stdin)
        path: str = event["tool_input"]["file_path"]
    except (json.JSONDecodeError, KeyError, TypeError) as exc:
        # Can't tell what is being edited -> refuse rather than guess.
        print(f"protect_files: unreadable input ({exc}); blocking.", file=sys.stderr)
        return 2

    name = re.split(r"[\\/]", path)[-1]  # Windows paths use \, others use /
    if name in PROTECTED:
        print(f"Blocked: {name} is protected because {PROTECTED[name]}.", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
